using System;
using System.IO;
using System.Windows;
using MarvelHeroes.DpsMeter.Services;
using MarvelHeroesComporator.NetworkSniffer;

namespace MarvelHeroes.DpsMeter;

/// <summary>
/// Standalone DPS-meter application entry point.
///
/// <para>
/// Lifecycle is intentionally tiny: on Startup we (1) construct + start the passive packet
/// sniffer that listens on the configured game TCP port (default 4306 in <c>dps-overlay.json</c>)
/// for Marvel Heroes <c>NetMessagePowerResult</c> traffic,
/// (2) hand the sniffer to a <see cref="DpsOverlayPresenter"/> that owns the meter logic and
/// the floating overlay window, (3) call <see cref="DpsOverlayPresenter.Start"/> which shows
/// the overlay without stealing focus from the game.  On Exit we tear everything down.
/// </para>
///
/// <para>
/// AppData paths are deliberately kept identical to the legacy comporator-bundled version
/// (<c>%LocalAppData%\MarvelHeroesComporator\</c> for <c>dps-overlay.json</c> — window position,
/// boss-only toggle, and optional <c>GameTcpPort</c> / <c>AdditionalTcpPorts</c> /
/// <c>NpcapAdapterFilter</c> for non-default servers — plus <c>dps-max-hits.json</c>,
/// <c>dps-player-index.json</c>, <c>dps-meter.log</c>) so users
/// upgrading from the integrated overlay to this standalone build keep their max-hit records
/// and learned nickname index without manual migration.
/// </para>
///
/// <para>
/// No tray icon, no settings window, no main MainWindow — by design (per user-selected
/// "minimal" scope).  The overlay's right-click menu is the entire surface area; "Exit" in
/// that menu is the only quit path because <c>App.ShutdownMode</c> is OnExplicitShutdown.
/// </para>
/// </summary>
public partial class App : Application
{
    /// <summary>Path the sniffer + presenter both append to.  Created once at startup so
    /// boot-time errors (Npcap missing, no admin, no NIC matched) get persisted before the
    /// presenter even spins up its own writer.</summary>
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator", "dps-meter.log");

    /// <summary>Maximum size of <see cref="LogPath"/> before rotation triggers.  When exceeded,
    /// the current log is renamed to <c>.log.1</c> (overwriting any previous backup) and a
    /// fresh log starts.  Total disk footprint capped at ~2x this value.  50 MB covers
    /// roughly a full day of light play or a few hours of verbose-on heavy combat.</summary>
    private const long LogSizeCapBytes = 50L * 1024 * 1024;

    /// <summary>How often, in successful <see cref="AppendLog"/> calls, we re-check the
    /// file size for rotation.  Setting this too low taxes the FS (one <c>FileInfo</c> stat
    /// per check); too high lets the file overshoot the cap during high-volume bursts.  At
    /// 10k writes / heavy-verbose ~1k-2k lines per minute, the check fires every 5-10 min.</summary>
    private const int LogRotationCheckInterval = 10_000;

    /// <summary>Per-write counter used to trigger periodic rotation checks.  Incremented
    /// via <c>Interlocked</c> so capture-thread writes don't drop ticks against UI-thread
    /// writes.  Wraps cleanly at int.MaxValue; the modulo check is what matters.</summary>
    private static int s_logWriteCount;

    /// <summary>Serializes rotation operations so two threads can't race on the
    /// File.Move(current -> .1).  Per-line append writes don't share this lock --
    /// <c>File.AppendAllText</c> serializes its own opens, and rotation is rare enough that
    /// the lock contention is invisible.</summary>
    private static readonly object s_logRotationLock = new();

    private MhMissionSniffer? _sniffer;
    private DpsOverlayPresenter? _presenter;
    private TestModeDataFeed? _testFeed;

    /// <summary>
    /// Wired up via <c>Startup="OnStartup"</c> in App.xaml.  Runs on the WPF UI thread before
    /// any window is created, so we have a valid <c>Application.Current.Dispatcher</c> to hand
    /// to the presenter for marshalling capture-thread events back into XAML.
    /// </summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Pre-rotate the log if a previous session left a fat file behind.  This is the
        // common case for users hitting our growth cap: long verbose-on sessions push the
        // log past 50 MB, and without this they'd keep growing across restarts.  We rotate
        // BEFORE anything else writes to the log so the first session-start banner lands
        // in the fresh file, not the old one.
        RotateLogIfTooLarge();

        // Top-level catch-all: an unhandled exception during sniffer init or window creation
        // would tear down the WPF dispatcher silently (no taskbar = no obvious error). Log it
        // somewhere the user can find before the process dies.
        DispatcherUnhandledException += (_, ex) =>
        {
            AppendLog($"FATAL (dispatcher): {ex.Exception.GetType().Name}: {ex.Exception.Message}");
            AppendLog(ex.Exception.StackTrace ?? "");
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            if (ex.ExceptionObject is Exception inner)
            {
                AppendLog($"FATAL (appdomain): {inner.GetType().Name}: {inner.Message}");
                AppendLog(inner.StackTrace ?? "");
            }
        };

        bool testMode = Array.IndexOf(e.Args, "--test-mode") >= 0;
        if (testMode) ForceAppendLog("--test-mode active: sniffer not started, synthetic data feed will run.");

        AppendLog($"────── MarvelHeroes.DpsMeter standalone start ({DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z) ──────");

        // Let Ctrl+C in the terminal shut the app down cleanly instead of crashing the process.
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            Dispatcher.BeginInvoke(() => Shutdown(0));
        };

        try
        {
            // First-run materialization: if the user has never opened this app on this
            // machine, dps-overlay.json doesn't exist yet — Load() returns the C# defaults
            // (Tahiti-tuned: GameTcpPort=4306, LoggingEnabled=false) but the file stays
            // missing until the user moves the overlay or toggles a menu option.  We
            // proactively Save() in that case so users see every available knob in one
            // place and can edit `LoggingEnabled` / community-server `GameTcpPort` etc.
            // immediately after install without spelunking the source.
            //
            // Detect "first run" BEFORE Load() — Load() never creates the file itself, so
            // existence-check on the path is the unambiguous signal.  Existing users see
            // no behavior change here (file already exists → branch skipped).
            bool isFirstRun = !File.Exists(DpsOverlaySettingsFile.SettingsFilePath);
            var userNet = DpsOverlaySettingsFile.Load();
            if (isFirstRun)
            {
                DpsOverlaySettingsFile.Save(userNet);
                ForceAppendLog(
                    $"First run: wrote default settings to {DpsOverlaySettingsFile.SettingsFilePath} " +
                    $"(GameTcpPort={userNet.GameTcpPort}, LoggingEnabled={userNet.LoggingEnabled}). " +
                    $"Edit this file to enable logs or override Tahiti-default capture settings.");
            }
            var extra = userNet.AdditionalTcpPorts is { Length: > 0 } ports
                ? string.Join(", ", ports)
                : null;
            // Force-write the settings echo bypassing DpsOverlaySettingsFile.IsLoggingEnabled.
            // Load() above has already synced the gate, so a regular AppendLog call would be
            // suppressed when the user has set "LoggingEnabled": false — but we ALWAYS want
            // a single one-shot record per session of "yes, your settings were read", so
            // even users who disabled logging get unambiguous feedback that their flag was
            // honored.  After this block, every subsequent AppendLog call obeys the gate.
            ForceAppendLog(
                $"Network settings ({DpsOverlaySettingsFile.SettingsFilePath}): GameTcpPort={userNet.GameTcpPort}" +
                (extra is not null ? $", AdditionalTcpPorts=[{extra}]" : "") +
                (userNet.NpcapAdapterFilter is { } af ? $", NpcapAdapterFilter=\"{af}\"" : "") +
                $", LoggingEnabled={userNet.LoggingEnabled}");
            if (!userNet.LoggingEnabled)
            {
                ForceAppendLog(
                    $"LoggingEnabled=false — suppressing all subsequent log writes. " +
                    $"Re-enable by setting \"LoggingEnabled\": true in {DpsOverlaySettingsFile.SettingsFilePath} and restarting the app.");
            }

            // Sniffer construction is cheap (no PCAP handles opened yet); Start is what actually
            // probes Npcap + opens devices.  Diagnostic sink is set BEFORE Start so the device-
            // enumeration log lines reach disk if startup fails.
            _sniffer = new MhMissionSniffer
            {
                Diagnostic = AppendLog,
                Port = userNet.GameTcpPort,
                AdditionalCapturePorts = userNet.AdditionalTcpPorts,
                AdapterFilter = userNet.NpcapAdapterFilter,
            };
            // In test mode we deliberately skip TryStart() so Npcap is never opened — the
            // TestModeDataFeed injects events directly into the meters instead.
            if (!testMode)
            {
                // TryStart() is the soft-fail variant: returns false if Npcap isn't installed,
                // no NIC could be opened, or another permissions issue blocks capture.  Throwing
                // here gives us a uniform code path for "show the user a dialog and exit cleanly".
                if (!_sniffer.TryStart())
                    throw new InvalidOperationException(
                        $"Network sniffer failed to start: {_sniffer.StartFailureReason ?? "unknown reason"}");
            }

            // Presenter takes over from here: creates the meter, wires diagnostics, builds the
            // overlay window, starts the 4 Hz decay tick.  Its Start() is synchronous (Invoke,
            // not BeginInvoke) so by the time we return the overlay is already on screen.
            _presenter = new DpsOverlayPresenter(_sniffer, Dispatcher);

            // Auto-hide the overlay whenever Marvel Heroes (or our own overlay process) isn't
            // the foreground window.  Without this the overlay sits on top of every desktop
            // app the user Alt+Tabs to (browser, Discord, etc.) which is the legacy comporator's
            // long-standing UX as well — see Helpers/GameWindowLocator.IsForegroundGameOrThisApp.
            // Predicate is polled at 4 Hz on the same DispatcherTimer that decays the DPS number
            // (see DpsOverlayPresenter.OnDecayTick), so latency is < 250 ms.
            _presenter.ShouldBeVisible = GameForegroundWatcher.IsGameOrSelfForeground;

            _presenter.Start();

            if (testMode && _presenter.Meter != null && _presenter.BossMeter != null)
            {
                _testFeed = new TestModeDataFeed(_presenter.Meter, _presenter.BossMeter);
                _testFeed.Start();
            }
        }
        catch (Exception ex)
        {
            // Walk the InnerException chain -- TargetInvocationException (from WPF's BAML
            // loader) and friends wrap the real fault.  Without this the log shows only
            // "Exception has been thrown by the target of an invocation." which is useless
            // for triage.
            var cur = ex;
            int depth = 0;
            while (cur != null)
            {
                AppendLog($"Startup failed [{depth}]: {cur.GetType().Name}: {cur.Message}");
                AppendLog(cur.StackTrace ?? "");
                cur = cur.InnerException;
                depth++;
                if (depth > 8) break;  // pathological loop guard; real chains are 2-3 deep
            }
            // Surface the failure visually since there's no main window to fall back to.
            // Most common cause is "Npcap not installed" — a friendly message points the user
            // at the right download instead of leaving them staring at an empty desktop.
            MessageBox.Show(
                $"Failed to start the DPS meter:\n\n{ex.Message}\n\n" +
                "Common causes:\n" +
                "  • Npcap is not installed (download from https://npcap.com/)\n" +
                "  • Marvel Heroes game traffic isn't on the TCP port you're capturing — edit GameTcpPort " +
                $"(and optional AdditionalTcpPorts) in:\n    {DpsOverlaySettingsFile.SettingsFilePath}\n\n" +
                $"Full diagnostic log: {LogPath}",
                "MarvelHeroes DPS Meter — startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Wired up via <c>Exit="OnExit"</c>.  Runs on the UI thread after the user picks "Exit"
    /// from the overlay menu (or after a programmatic <see cref="Application.Shutdown()"/>).
    /// Every Stop/Dispose call is wrapped in try/catch because we never want shutdown to
    /// throw — the process is leaving anyway and an exception here would only mask the
    /// reason for the exit in post-mortem logs.
    /// </summary>
    private void OnExit(object sender, ExitEventArgs e)
    {
        try { _testFeed?.Dispose(); }   catch (Exception ex) { AppendLog($"Stop(testFeed): {ex.Message}"); }
        try { _presenter?.Stop(); }     catch (Exception ex) { AppendLog($"Stop(presenter): {ex.Message}"); }
        try { _sniffer?.Stop(); }       catch (Exception ex) { AppendLog($"Stop(sniffer): {ex.Message}"); }
        AppendLog($"────── MarvelHeroes.DpsMeter standalone exit (code={e.ApplicationExitCode}) ──────");
    }

    /// <summary>Same log file as the presenter writes into — having both append here means
    /// the boot-time / shutdown-time bracket records appear inline with the per-event meter
    /// diagnostics, making "what was the app doing at 17:42?" trivial to answer.
    ///
    /// <para>Gated by <see cref="DpsOverlaySettingsFile.IsLoggingEnabled"/> so users who set
    /// <c>"LoggingEnabled": false</c> in <c>dps-overlay.json</c> get zero disk writes after
    /// the gate is synced from <see cref="DpsOverlaySettingsFile.Load"/>. The very first few
    /// lines (the startup banner BEFORE Load runs) are still written because the gate
    /// defaults to <c>true</c> — that's intentional, the banner confirms the process started
    /// and is one line per session.</para></summary>
    private static void AppendLog(string line)
    {
        if (!DpsOverlaySettingsFile.IsLoggingEnabled) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {line}{Environment.NewLine}");

            // Periodic rotation check.  Cheap fast-path: a single Interlocked increment +
            // modulo.  The actual FileInfo stat only fires every <see cref="LogRotationCheckInterval"/>
            // writes (a few times per heavy-verbose session, never under light load).
            int n = System.Threading.Interlocked.Increment(ref s_logWriteCount);
            if (n % LogRotationCheckInterval == 0)
                RotateLogIfTooLarge();
        }
        catch { /* logging is best-effort; never let it crash the host */ }
    }

    /// <summary>If the current log file exceeds <see cref="LogSizeCapBytes"/>, rename it
    /// to <c>dps-meter.log.1</c> (overwriting any prior backup) so subsequent writes start
    /// a fresh file.  Bounds total disk footprint at ~2x the cap.  Best-effort and silent
    /// on failure -- if rotation can't proceed (lock held by external viewer, etc.) the
    /// current file just keeps growing past the cap; not great but never crashes the host.
    ///
    /// <para>Called at startup (once, to pre-rotate a huge log from a previous session
    /// before any per-event writes pile on) and periodically from <see cref="AppendLog"/>
    /// during long sessions.</para></summary>
    private static void RotateLogIfTooLarge()
    {
        lock (s_logRotationLock)
        {
            try
            {
                var fi = new FileInfo(LogPath);
                if (!fi.Exists || fi.Length < LogSizeCapBytes) return;

                string backupPath = LogPath + ".1";
                if (File.Exists(backupPath)) File.Delete(backupPath);
                File.Move(LogPath, backupPath);
                // Don't AppendLog from here -- we'd re-enter AppendLog from inside its own
                // call chain, and the very first post-rotation write will be the next
                // regular log line anyway.  ForceAppendLog avoids the gate but is also a
                // recursive write hazard; skip it.
            }
            catch { /* best-effort */ }
        }
    }

    /// <summary>Bypass-the-gate writer for one-shot lines that must always reach disk
    /// regardless of <see cref="DpsOverlaySettingsFile.IsLoggingEnabled"/> — currently the
    /// settings-echo line and the "logging is now disabled" confirmation that fire exactly
    /// once per session right after settings load.  Don't use for per-event traffic; the
    /// whole point of the gate is to give users a way to suppress that.</summary>
    private static void ForceAppendLog(string line)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {line}{Environment.NewLine}");
        }
        catch { /* best-effort */ }
    }
}
