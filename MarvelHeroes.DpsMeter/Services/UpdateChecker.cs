using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// One-shot async check against GitHub's releases API for a Cerebro version newer than
/// the one currently running.  Designed for the "soft updater" UX: fetch the latest tag,
/// compare to <see cref="CerebroVersion.Parsed"/>, and return a small <see cref="Result"/>
/// that the UI surfaces as a banner.  No auto-download / auto-replace path -- the banner
/// opens the GitHub release page in the user's browser and they unzip manually.
///
/// <para>Failure modes (network down, GitHub rate-limited, JSON shape changed, repo moved)
/// all return <see cref="Result.None"/> silently.  The whole feature is best-effort
/// background work; a failed check shouldn't disturb the user.</para>
/// </summary>
internal static class UpdateChecker
{
    /// <summary>GitHub repo to query.  Hardcoded here because the binary is tied to this
    /// fork; if you ever rename / move the repo, update this string and the assembly will
    /// keep pointing at the right place for releases.</summary>
    private const string RepoOwner = "vash-silver";
    private const string RepoName  = "cerebro";

    /// <summary>User-Agent header required by GitHub's API.  Any non-empty value is
    /// accepted; conventional to use the application's name.</summary>
    private const string UserAgent = "Cerebro-UpdateChecker";

    /// <summary>Outcome of a single <see cref="CheckAsync"/> call.</summary>
    public sealed class Result
    {
        public static readonly Result None = new() { Available = false };

        /// <summary>True when the GitHub API reported a tag whose parsed version is
        /// strictly greater than <see cref="CerebroVersion.Parsed"/>.  False when the
        /// running version is up-to-date OR the check failed for any reason.</summary>
        public bool Available { get; init; }

        /// <summary>The tag name from GitHub, e.g. <c>"v2.9"</c>.  Used for the banner
        /// label and for the "dismissed version" persistence so repeated dismissals
        /// stick across launches until a newer release comes out.</summary>
        public string TagName { get; init; } = "";

        /// <summary>Same as <see cref="TagName"/> with any leading <c>v</c> stripped,
        /// for cleaner UI display.</summary>
        public string DisplayVersion { get; init; } = "";

        /// <summary>Browser-openable HTML page for the release.  Fallback when the
        /// in-app self-update flow fails -- the banner offers a "Open release page"
        /// link so the user can still grab the new zip manually.</summary>
        public string HtmlUrl { get; init; } = "";

        /// <summary>Direct download URL for the <c>Cerebro-v&lt;N&gt;.zip</c> asset
        /// attached to the release.  Empty when GitHub didn't return a matching
        /// asset (the release was published without uploading the zip, for
        /// example), in which case the auto-update flow falls back to the browser
        /// link.</summary>
        public string AssetDownloadUrl { get; init; } = "";

        /// <summary>Asset size in bytes from the GitHub API.  Surfaced so the
        /// progress UI can show "MB / total MB" without doing a HEAD request.
        /// Zero when no matching asset was found.</summary>
        public long AssetSize { get; init; }

        /// <summary>Hex-encoded SHA-256 of the release asset, as advertised by
        /// GitHub.  Verified against the downloaded bytes before we touch the
        /// running EXE.  Empty when GitHub didn't provide a digest (rare; older
        /// releases may not have one) -- in that case we skip verification.
        /// Stored in lower-case to make the post-download hash compare trivial.</summary>
        public string AssetSha256 { get; init; } = "";
    }

    /// <summary>Fetch the latest release from GitHub and compare its tag against the
    /// running version.  Returns <see cref="Result.None"/> on any error (no exceptions
    /// surface to the caller -- this is fire-and-forget background work).  The optional
    /// <paramref name="ct"/> is honored so the caller can bound the wait time on app
    /// startup (we use ~5 seconds).</summary>
    public static async Task<Result> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return Result.None;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagProp)) return Result.None;
            string tag = tagProp.GetString() ?? "";
            if (string.IsNullOrEmpty(tag)) return Result.None;

            string display = tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tag.Substring(1) : tag;
            // GitHub tags are guaranteed-ish to be semver-shaped on this repo (publish.ps1
            // always tags with vMAJOR.MINOR or vMAJOR.MINOR.PATCH).  If the user has
            // hand-tagged something weird that doesn't parse, treat as "no update" rather
            // than throwing -- silent failure is the right default for a background poll.
            int cut = display.IndexOfAny(new[] { '-', '+' });
            string parseable = cut > 0 ? display.Substring(0, cut) : display;
            if (!Version.TryParse(parseable, out var remote)) return Result.None;

            // Normalize both versions to 3 components for comparison.  Version.Parse will
            // happily produce 2-component results from "2.8" which compare oddly against
            // 3-component "2.8.0".  Pad with zeros so "2.8" == "2.8.0".
            var local = CerebroVersion.Parsed;
            var localNorm  = new Version(local.Major,  local.Minor  >= 0 ? local.Minor  : 0, local.Build  >= 0 ? local.Build  : 0);
            var remoteNorm = new Version(remote.Major, remote.Minor >= 0 ? remote.Minor : 0, remote.Build >= 0 ? remote.Build : 0);

            if (remoteNorm <= localNorm) return Result.None;

            string htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? (urlProp.GetString() ?? "") : "";

            // Walk the assets array looking for the Cerebro release zip.  The
            // publish script always names it Cerebro-v<TAG>.zip; we match by
            // prefix/extension rather than exact tag so a hand-renamed asset
            // (e.g. "Cerebro-v2.9-hotfix.zip") still gets picked up.
            string assetUrl = "";
            long   assetSize = 0;
            string assetSha = "";
            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assetsProp.EnumerateArray())
                {
                    string name = a.TryGetProperty("name", out var nameProp) ? (nameProp.GetString() ?? "") : "";
                    if (!name.StartsWith("Cerebro", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!name.EndsWith(".zip",     StringComparison.OrdinalIgnoreCase)) continue;
                    assetUrl  = a.TryGetProperty("browser_download_url", out var urlP) ? (urlP.GetString() ?? "") : "";
                    assetSize = a.TryGetProperty("size",                  out var szP) ? szP.GetInt64()           : 0;
                    string rawDigest = a.TryGetProperty("digest", out var dP) ? (dP.GetString() ?? "") : "";
                    // GitHub returns the digest as "sha256:HEX..."; strip the prefix so
                    // callers can compare directly against a hex hash they computed.
                    if (rawDigest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        assetSha = rawDigest.Substring("sha256:".Length).ToLowerInvariant();
                    break;
                }
            }

            return new Result
            {
                Available        = true,
                TagName          = tag,
                DisplayVersion   = display,
                HtmlUrl          = htmlUrl,
                AssetDownloadUrl = assetUrl,
                AssetSize        = assetSize,
                AssetSha256      = assetSha,
            };
        }
        catch
        {
            return Result.None;
        }
    }
}
