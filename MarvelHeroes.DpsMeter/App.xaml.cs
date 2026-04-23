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
/// sniffer that listens on TCP/4306 for Marvel Heroes <c>NetMessagePowerResult</c> traffic,
/// (2) hand the sniffer to a <see cref="DpsOverlayPresenter"/> that owns the meter logic and
/// the floating overlay window, (3) call <see cref="DpsOverlayPresenter.Start"/> which shows
/// the overlay without stealing focus from the game.  On Exit we tear everything down.
/// </para>
///
/// <para>
/// AppData paths are deliberately kept identical to the legacy comporator-bundled version
/// (<c>%LocalAppData%\MarvelHeroesComporator\</c> for <c>dps-overlay.json</c>,
/// <c>dps-max-hits.json</c>, <c>dps-player-index.json</c>, <c>dps-meter.log</c>) so users
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

    private MhMissionSniffer? _sniffer;
    private DpsOverlayPresenter? _presenter;

    /// <summary>
    /// Wired up via <c>Startup="OnStartup"</c> in App.xaml.  Runs on the WPF UI thread before
    /// any window is created, so we have a valid <c>Application.Current.Dispatcher</c> to hand
    /// to the presenter for marshalling capture-thread events back into XAML.
    /// </summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
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

        AppendLog($"────── MarvelHeroes.DpsMeter standalone start ({DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z) ──────");

        try
        {
            // Sniffer construction is cheap (no PCAP handles opened yet); Start is what actually
            // probes Npcap + opens devices.  Diagnostic sink is set BEFORE Start so the device-
            // enumeration log lines reach disk if startup fails.
            _sniffer = new MhMissionSniffer
            {
                Diagnostic = AppendLog,
            };
            // TryStart() is the soft-fail variant: returns false if Npcap isn't installed,
            // no NIC could be opened, or another permissions issue blocks capture.  Throwing
            // here gives us a uniform code path for "show the user a dialog and exit cleanly".
            if (!_sniffer.TryStart())
                throw new InvalidOperationException(
                    $"Network sniffer failed to start: {_sniffer.StartFailureReason ?? "unknown reason"}");

            // Presenter takes over from here: creates the meter, wires diagnostics, builds the
            // overlay window, starts the 4 Hz decay tick.  Its Start() is synchronous (Invoke,
            // not BeginInvoke) so by the time we return the overlay is already on screen.
            _presenter = new DpsOverlayPresenter(_sniffer, Dispatcher);
            _presenter.Start();
        }
        catch (Exception ex)
        {
            AppendLog($"Startup failed: {ex.GetType().Name}: {ex.Message}");
            AppendLog(ex.StackTrace ?? "");
            // Surface the failure visually since there's no main window to fall back to.
            // Most common cause is "Npcap not installed" — a friendly message points the user
            // at the right download instead of leaving them staring at an empty desktop.
            MessageBox.Show(
                $"Failed to start the DPS meter:\n\n{ex.Message}\n\n" +
                "Common causes:\n" +
                "  • Npcap is not installed (download from https://npcap.com/)\n" +
                "  • Marvel Heroes traffic isn't on TCP/4306 (proxy / VPN routing changes)\n\n" +
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
        try { _presenter?.Stop(); } catch (Exception ex) { AppendLog($"Stop(presenter): {ex.Message}"); }
        try { _sniffer?.Stop(); }    catch (Exception ex) { AppendLog($"Stop(sniffer): {ex.Message}"); }
        AppendLog($"────── MarvelHeroes.DpsMeter standalone exit (code={e.ApplicationExitCode}) ──────");
    }

    /// <summary>Same log file as the presenter writes into — having both append here means
    /// the boot-time / shutdown-time bracket records appear inline with the per-event meter
    /// diagnostics, making "what was the app doing at 17:42?" trivial to answer.</summary>
    private static void AppendLog(string line)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {line}{Environment.NewLine}");
        }
        catch { /* logging is best-effort; never let it crash the host */ }
    }
}
