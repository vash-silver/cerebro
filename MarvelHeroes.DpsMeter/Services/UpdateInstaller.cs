using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Phase-2 self-update: download the latest release zip, verify it, extract the
/// new <c>Cerebro.exe</c> to a temp location, write a tiny PowerShell bootstrap
/// script, launch the script, and let the caller quit the app.  The bootstrap
/// waits for the running EXE's file lock to release, swaps the binary in place,
/// and relaunches Cerebro.
///
/// <para>This works around Windows' "can't replace a running EXE" restriction:
/// the script outlives the parent Cerebro process and does the swap once we're
/// out of the way.  The new binary is identical to what a user would get from
/// the GitHub release page -- same publish.ps1 artifact, same SHA-256.</para>
///
/// <para><b>Failure modes are reported via <see cref="InstallOutcome"/></b> so the
/// banner can show a user-readable message and offer to open the release page as
/// a fallback.  Every step is best-effort; partial-failure cleanup runs in a
/// finally so leftover temp files don't accumulate.</para>
///
/// <para><b>Edge case: write-protected install location.</b>  If Cerebro lives
/// under <c>C:\Program Files</c> or any path the current user can't write to,
/// the bootstrap's <c>Move-Item</c> will fail and the user is stuck with the
/// old binary.  We don't try to UAC-elevate here -- it's not the typical
/// install location for this tool (testers receive the zip and run it from
/// their Downloads / a portable folder).  A future iteration could surface a
/// clearer error in that case; for now the bootstrap script writes a small
/// log to <c>%TEMP%\cerebro-update-*.log</c> for triage.</para>
/// </summary>
internal static class UpdateInstaller
{
    /// <summary>Outcome of an <see cref="InstallAsync"/> call.  On success the
    /// caller MUST immediately call <c>Application.Current.Shutdown()</c> so the
    /// bootstrap script can grab the file lock and complete the swap.  On
    /// failure the app keeps running and the banner shows
    /// <see cref="ErrorMessage"/> with a "Open release page" fallback.</summary>
    public sealed class InstallOutcome
    {
        public bool   Success      { get; init; }
        public string ErrorMessage { get; init; } = "";
        public static InstallOutcome Ok() => new() { Success = true };
        public static InstallOutcome Fail(string msg) => new() { Success = false, ErrorMessage = msg };
    }

    /// <summary>Progress callback shape: bytes downloaded, total bytes
    /// (or 0 when unknown), and a short status label for the UI.</summary>
    public readonly record struct DownloadProgress(long BytesReceived, long TotalBytes, string Status);

    /// <summary>Download the release zip from <paramref name="result"/>, verify
    /// it, extract <c>Cerebro.exe</c>, and launch the bootstrap script.  Returns
    /// before the app has quit -- the caller is responsible for shutting down.
    /// All paths under <c>%TEMP%\cerebro-update-{guid}\</c> are cleaned up by
    /// the bootstrap script after the swap completes.</summary>
    public static async Task<InstallOutcome> InstallAsync(
        UpdateChecker.Result result,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(result.AssetDownloadUrl))
            return InstallOutcome.Fail("No release asset URL on this update result -- the GitHub release may not have a zip attached.");

        // ── Where the current Cerebro lives ─────────────────────────────────
        // Environment.ProcessPath is the right call for single-file self-
        // contained apps (.NET 6+); Assembly.Location returns "" because the
        // executing assembly is hosted inside the single-file extractor.
        string? currentExePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExePath) || !File.Exists(currentExePath))
            return InstallOutcome.Fail("Could not determine the running Cerebro EXE path.  Auto-update isn't supported in this hosting context.");

        // Quick writeability probe: if we can't replace our own EXE, the
        // bootstrap will silently fail -- detect now and bail with a useful
        // error instead.  We try creating a sibling temp file (the EXE itself
        // is locked while we're running, so we can't test it directly).
        try
        {
            string probePath = Path.Combine(Path.GetDirectoryName(currentExePath)!, $".cerebro-write-probe-{Guid.NewGuid():N}");
            File.WriteAllBytes(probePath, Array.Empty<byte>());
            File.Delete(probePath);
        }
        catch (Exception ex)
        {
            return InstallOutcome.Fail($"Cerebro's install folder isn't writeable ({ex.GetType().Name}).  Move the EXE out of a system folder, or download the release manually.");
        }

        // Stage all temp output under one folder we can clean up on failure.
        string stageRoot = Path.Combine(Path.GetTempPath(), $"cerebro-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stageRoot);
        string zipPath        = Path.Combine(stageRoot, "release.zip");
        string extractedExe   = Path.Combine(stageRoot, "Cerebro.exe");
        string bootstrapPath  = Path.Combine(stageRoot, "bootstrap.ps1");

        try
        {
            // ── 1. Download ─────────────────────────────────────────────────
            progress?.Report(new DownloadProgress(0, result.AssetSize, "Connecting…"));
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Cerebro-UpdateInstaller");
                using var resp = await http.GetAsync(result.AssetDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    return InstallOutcome.Fail($"Download failed: HTTP {(int)resp.StatusCode} from {result.AssetDownloadUrl}");
                long total = resp.Content.Headers.ContentLength ?? result.AssetSize;
                await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var dst = File.Create(zipPath);

                // 64 KB buffer is a fine balance: large enough that we're not
                // syscalls-bound on a fast connection, small enough that we
                // report progress smoothly.
                var buf = new byte[64 * 1024];
                long received = 0;
                int n;
                while ((n = await src.ReadAsync(buf.AsMemory(0, buf.Length), ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                    received += n;
                    progress?.Report(new DownloadProgress(received, total, "Downloading…"));
                }
            }

            // ── 2. Verify SHA-256 ───────────────────────────────────────────
            // Skip verification if GitHub didn't provide a digest (rare; older
            // releases pre-dating digest support).  The user has TLS as their
            // integrity guarantee in that case.
            if (!string.IsNullOrEmpty(result.AssetSha256))
            {
                progress?.Report(new DownloadProgress(result.AssetSize, result.AssetSize, "Verifying…"));
                string actual;
                await using (var fs = File.OpenRead(zipPath))
                {
                    using var sha = SHA256.Create();
                    var hash = await sha.ComputeHashAsync(fs, ct).ConfigureAwait(false);
                    actual = Convert.ToHexString(hash).ToLowerInvariant();
                }
                if (!string.Equals(actual, result.AssetSha256, StringComparison.OrdinalIgnoreCase))
                    return InstallOutcome.Fail($"Downloaded zip failed SHA-256 verification (expected {result.AssetSha256}, got {actual}).");
            }

            // ── 3. Extract Cerebro.exe ──────────────────────────────────────
            progress?.Report(new DownloadProgress(result.AssetSize, result.AssetSize, "Extracting…"));
            bool foundExe = false;
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!string.Equals(entry.Name, "Cerebro.exe", StringComparison.OrdinalIgnoreCase)) continue;
                    entry.ExtractToFile(extractedExe, overwrite: true);
                    foundExe = true;
                    break;
                }
            }
            if (!foundExe)
                return InstallOutcome.Fail("The downloaded zip did not contain Cerebro.exe -- release packaging may have changed.");

            // ── 4. Write the bootstrap script ───────────────────────────────
            int parentPid = Environment.ProcessId;
            string script = BuildBootstrapScript(
                parentPid:    parentPid,
                targetExe:    currentExePath,
                newExe:       extractedExe,
                stageRoot:    stageRoot,
                logPath:      Path.Combine(Path.GetTempPath(), $"cerebro-update-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log"));
            File.WriteAllText(bootstrapPath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // ── 5. Launch the bootstrap detached ────────────────────────────
            // Hidden console -- the script self-logs to %TEMP% for debugging
            // but the user shouldn't see a PowerShell window flash.
            var psi = new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{bootstrapPath}\"",
                UseShellExecute = false,
                CreateNoWindow  = true,
                WindowStyle     = ProcessWindowStyle.Hidden,
            };
            Process.Start(psi);

            progress?.Report(new DownloadProgress(result.AssetSize, result.AssetSize, "Restarting Cerebro…"));
            return InstallOutcome.Ok();
        }
        catch (OperationCanceledException)
        {
            // User cancelled or token tripped.  Clean up the stage and return.
            TryCleanup(stageRoot);
            return InstallOutcome.Fail("Update cancelled.");
        }
        catch (Exception ex)
        {
            TryCleanup(stageRoot);
            return InstallOutcome.Fail($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void TryCleanup(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { /* best-effort; %TEMP% gets swept by Windows eventually */ }
    }

    /// <summary>Emit the PowerShell bootstrap that does the actual EXE swap.
    /// Kept inline (rather than shipped as a resource) so any tweak to the swap
    /// flow ships with the updater that wrote the script -- there's no
    /// version-skew risk between "the C# that calls this" and "the script that
    /// runs after the C# is gone".</summary>
    private static string BuildBootstrapScript(int parentPid, string targetExe, string newExe, string stageRoot, string logPath)
    {
        // PowerShell-escape any single quotes by doubling them; otherwise paths
        // with apostrophes (rare but possible in user folders) would break the
        // single-quoted literals below.
        string Esc(string s) => s.Replace("'", "''");

        return $@"# Cerebro auto-update bootstrap
# Replaces a running Cerebro.exe with a freshly-downloaded one, then relaunches.
# Self-deletes the staging folder + this script on success.
$ErrorActionPreference = 'Continue'
$logPath = '{Esc(logPath)}'

function Log($msg) {{
    try {{ Add-Content -Path $logPath -Value ((Get-Date).ToString('s') + '  ' + $msg) -ErrorAction SilentlyContinue }} catch {{}}
}}

Log 'Bootstrap started.'
Log ('Parent PID: ' + {parentPid})
Log ('Target EXE: {Esc(targetExe)}')
Log ('New EXE:    {Esc(newExe)}')

# Wait up to 30s for the parent Cerebro process to exit.  Poll every 200ms; if
# it's still alive at the deadline, force-kill it so the file lock releases.
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {{
    $proc = Get-Process -Id {parentPid} -ErrorAction SilentlyContinue
    if (-not $proc) {{ break }}
    Start-Sleep -Milliseconds 200
}}
$proc = Get-Process -Id {parentPid} -ErrorAction SilentlyContinue
if ($proc) {{
    Log 'Parent did not exit in 30s; force-killing.'
    try {{ Stop-Process -Id {parentPid} -Force -ErrorAction Stop }} catch {{ Log ('Stop-Process failed: ' + $_.Exception.Message) }}
    Start-Sleep -Milliseconds 600
}} else {{
    Log 'Parent exited cleanly.'
}}

# Swap the EXE.  Rename the current one to .old as an in-case-Move-fails
# checkpoint, then move the new one into place.  If the swap fails we restore
# the .old so the user is never stranded without a Cerebro.exe.
$targetExe = '{Esc(targetExe)}'
$newExe    = '{Esc(newExe)}'
$backup    = $targetExe + '.old'

try {{
    if (Test-Path $backup) {{ Remove-Item -Path $backup -Force -ErrorAction SilentlyContinue }}
    Move-Item -Path $targetExe -Destination $backup -Force
    Log 'Backed up current EXE to .old'
    Move-Item -Path $newExe -Destination $targetExe -Force
    Log 'New EXE moved into place.'
}}
catch {{
    Log ('Swap failed: ' + $_.Exception.Message)
    if ((Test-Path $backup) -and -not (Test-Path $targetExe)) {{
        try {{
            Move-Item -Path $backup -Destination $targetExe -Force
            Log 'Rolled back to .old.'
        }} catch {{ Log ('Rollback failed too: ' + $_.Exception.Message) }}
    }}
    exit 1
}}

# Clean up the backup now that the new EXE is in place + verified extant.
try {{ Remove-Item -Path $backup -Force -ErrorAction SilentlyContinue }} catch {{}}

# Relaunch.  WorkingDirectory matches the target so any per-app relative paths
# behave as if the user double-clicked the EXE themselves.
try {{
    Start-Process -FilePath $targetExe -WorkingDirectory (Split-Path -Parent $targetExe)
    Log 'Relaunched.'
}} catch {{
    Log ('Relaunch failed: ' + $_.Exception.Message)
}}

# Sweep the staging folder + this script.
try {{ Remove-Item -Path '{Esc(stageRoot)}' -Recurse -Force -ErrorAction SilentlyContinue }} catch {{}}
try {{ Remove-Item -Path $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue }} catch {{}}
Log 'Done.'
";
    }
}
