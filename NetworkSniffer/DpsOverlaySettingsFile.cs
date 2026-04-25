using System.Text.Json;

namespace MarvelHeroesComporator.NetworkSniffer;

/// <summary>
/// Shared JSON at <see cref="SettingsFilePath"/> — window position, boss-only toggle, and
/// passive-capture options for <see cref="MhMissionSniffer"/>.  Edited by the overlay when the
/// user moves the window or toggles the menu; sniffer fields are preserved on save so users can
/// set <c>GameTcpPort</c> / <c>AdditionalTcpPorts</c> / <c>NpcapAdapterFilter</c> by hand for
/// non-default community servers or split frontend / game-instance sockets.
///
/// <para><b>Release defaults are tuned for the Tahiti community server</b> (the most common
/// MH endpoint these days — <c>162.249.174.3:4306</c>).  Tahiti uses the stock MH port
/// <c>4306</c>, so the default <see cref="GameTcpPort"/> = 4306 covers it as-is — the
/// community-server / split-port / adapter-pinning hooks are still here for users on
/// non-standard configs.  <see cref="BossDpsOnly"/> ships <c>true</c> because the meter
/// is primarily used to compare DPS in boss / terminal fights (right-click → "Boss DPS only"
/// to flip live).  <see cref="LoggingEnabled"/> ships disabled in release builds to keep
/// the meter quiet on disk for the typical user; flip it to <c>true</c> only when you're
/// actively debugging.</para>
///
/// <para>Example (merge into existing file or create before first run):</para>
/// <code>
/// {
///   "Left": 40,
///   "Top": 40,
///   "BossDpsOnly": true,
///   "GameTcpPort": 4306,
///   "AdditionalTcpPorts": [],
///   "NpcapAdapterFilter": null,
///   "LoggingEnabled": false
/// }
/// </code>
///
/// <para>The standalone <c>MarvelHeroes.DpsMeter</c> app materializes a file with these
/// exact defaults at <see cref="SettingsFilePath"/> on its very first run if no file
/// exists yet, so new users have every knob visible in one place without spelunking docs.</para>
/// </summary>
public sealed class DpsOverlaySettingsFile
{
    private const int DefaultGameTcpPort = 4306;

    public static string SettingsFilePath { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator", "dps-overlay.json");

    public double Left { get; set; } = 40;
    public double Top { get; set; } = 40;

    /// <summary>
    /// Boss-only filter — when <c>true</c> the leaderboard only credits damage against
    /// Boss / GroupBoss prototypes (MiniBoss is excluded by design — see
    /// <c>BossPrototypes.MiniBossIndices</c> and the diagnostic-rule notes).
    /// <b>Default <c>true</c> in release</b> — the meter exists primarily to compare DPS
    /// in boss / terminal fights; trash farming numbers are noisy and not what most users
    /// open the overlay for.  The right-click menu can flip this live without an app
    /// restart, and the toggle persists back to <c>dps-overlay.json</c> via <see cref="Save"/>.
    /// </summary>
    public bool BossDpsOnly { get; set; } = true;

    /// <summary>Primary game mux / frontend TCP port (default 4306 when missing or invalid).</summary>
    public int GameTcpPort { get; set; }

    /// <summary>
    /// Optional extra ports OR'd into the capture BPF (e.g. Tahiti-style split where entity
    /// traffic uses a second ephemeral listener). Duplicates of <see cref="GameTcpPort"/> are ignored.
    /// </summary>
    public int[]? AdditionalTcpPorts { get; set; }

    /// <summary>
    /// If set, only Npcap devices whose name or description contains this substring (case-insensitive)
    /// are opened — useful when multiple VPN / virtual adapters would otherwise match.
    /// </summary>
    public string? NpcapAdapterFilter { get; set; }

    /// <summary>
    /// Master switch for <c>%LocalAppData%\MarvelHeroesComporator\dps-meter.log</c>.
    /// <b>Default <c>false</c> in release</b> — the typical Tahiti player wants a quiet
    /// meter that doesn't grow a log file in the background; the diagnostic log is opt-in
    /// for users actively debugging an issue.
    ///
    /// <para>Flip to <c>true</c> when you need the per-event triage stream documented in
    /// <c>.cursor/rules/dps-meter-diagnostics.mdc</c> — PowerResultStats heartbeat, encounter
    /// lifecycle, peer-pet folds, hero-resolution failures, boss-filter drops, etc. Always
    /// re-enable for at least one repro session before opening an issue, otherwise there's
    /// nothing on disk for post-hoc analysis.</para>
    ///
    /// <para>Steady-state cost when ON is ~1–5 MB / hour during active play (mostly the
    /// 5 s heartbeat + per-encounter lines); cost when OFF is zero — the gate is checked
    /// as a single static field read in the <c>AppendLog</c> hot path.</para>
    ///
    /// <para>Reads via the static <see cref="IsLoggingEnabled"/> gate (mirrored from the
    /// loaded instance in <see cref="Load"/>) so per-event call sites don't need to
    /// re-touch the settings file on every line.</para>
    /// </summary>
    public bool LoggingEnabled { get; set; } = false;

    /// <summary>
    /// Process-wide gate consulted by every <c>AppendLog</c> call site. Defaults to
    /// <c>true</c> on purpose — the very first log lines fire before <see cref="Load"/>
    /// has had a chance to override the gate (boot banner, Npcap probe, settings echo)
    /// and we always want those recorded so users debugging "the app didn't even start"
    /// have something to read. After <see cref="Load"/> runs this mirrors the loaded
    /// instance's <see cref="LoggingEnabled"/> property — which defaults to <c>false</c>
    /// in release, so the gate flips closed within the first ~50 ms of startup and
    /// per-event traffic gets suppressed unless the user opted in.
    ///
    /// <para>Hosts that bypass <see cref="Load"/> entirely (tests, embedded usage) can
    /// set this directly. Setter is exposed instead of being computed from a private
    /// field so runtime UI toggles ("disable logging" menu item) can flip it without
    /// round-tripping through the JSON file.</para>
    /// </summary>
    public static bool IsLoggingEnabled { get; set; } = true;

    private static readonly JsonSerializerOptions s_jsonWrite = new()
    {
        WriteIndented = true,
    };

    /// <summary>Load from disk or return normalized defaults. Never throws. Always syncs
    /// <see cref="IsLoggingEnabled"/> from the resulting instance so the static log gate
    /// reflects the user's setting from this point forward.
    ///
    /// <para><b>Legacy upgrade behavior</b> — property defaults that changed in release
    /// would silently retroactively change behavior for users whose <c>dps-overlay.json</c>
    /// pre-dates the change (deserialization fills missing keys with the C# default).  We
    /// pre-parse the file with <see cref="JsonDocument"/> to detect missing keys and
    /// preserve the previous behavior on a per-key basis:</para>
    /// <list type="bullet">
    ///   <item><see cref="LoggingEnabled"/> default flipped <c>true → false</c>.  Missing
    ///         key on disk → restore <c>true</c> (existing community users keep their logs
    ///         until they explicitly opt out).</item>
    ///   <item><see cref="BossDpsOnly"/> default flipped <c>false → true</c>.  Missing
    ///         key on disk → restore <c>false</c> (existing users who never used the menu
    ///         toggle keep the trash-included view they were used to).</item>
    /// </list>
    /// <para>Present → honor whatever value the user wrote.  Brand-new installs (file
    /// doesn't exist at all) hit the no-file branch below and pick up the C# property
    /// defaults — same path the first-run save in <c>App.OnStartup</c> uses to materialize
    /// the file.</para>
    /// </summary>
    public static DpsOverlaySettingsFile Load()
    {
        try
        {
            if (System.IO.File.Exists(SettingsFilePath))
            {
                var json = System.IO.File.ReadAllText(SettingsFilePath);
                var s = JsonSerializer.Deserialize<DpsOverlaySettingsFile>(json);
                if (s is not null)
                {
                    if (!JsonContainsTopLevelProperty(json, nameof(LoggingEnabled)))
                    {
                        s.LoggingEnabled = true;
                    }
                    if (!JsonContainsTopLevelProperty(json, nameof(BossDpsOnly)))
                    {
                        s.BossDpsOnly = false;
                    }
                    Normalize(s);
                    IsLoggingEnabled = s.LoggingEnabled;
                    return s;
                }
            }
        }
        catch
        {
            /* corrupted / locked — fall through */
        }

        var fresh = new DpsOverlaySettingsFile();
        Normalize(fresh);
        IsLoggingEnabled = fresh.LoggingEnabled;
        return fresh;
    }

    /// <summary>Cheap "does this top-level property exist in the JSON file?" probe used by
    /// <see cref="Load"/> to distinguish "user wrote <c>LoggingEnabled: false</c> on purpose"
    /// from "user's file pre-dates the feature".  We can't infer this from the deserialized
    /// instance alone because both cases produce the same C# field value.  Swallows malformed
    /// JSON and returns <c>false</c> — Load's outer try/catch will fall through to defaults.</summary>
    private static bool JsonContainsTopLevelProperty(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(propertyName, out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Persist to <see cref="SettingsFilePath"/> (indented JSON). Never throws.</summary>
    public static void Save(DpsOverlaySettingsFile settings)
    {
        try
        {
            Normalize(settings);
            var dir = System.IO.Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(dir))
                System.IO.Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, s_jsonWrite);
            System.IO.File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            /* best-effort */
        }
    }

    /// <summary>Clamp ports and trim adapter filter so capture and serialization stay consistent.</summary>
    public static void Normalize(DpsOverlaySettingsFile s)
    {
        if (s.GameTcpPort < 1 || s.GameTcpPort > 65535)
            s.GameTcpPort = DefaultGameTcpPort;

        if (s.AdditionalTcpPorts is { Length: > 0 })
        {
            var list = new List<int>();
            foreach (var p in s.AdditionalTcpPorts)
            {
                if (p < 1 || p > 65535 || p == s.GameTcpPort) continue;
                if (!list.Contains(p)) list.Add(p);
            }

            s.AdditionalTcpPorts = list.Count > 0 ? list.ToArray() : null;
        }
        else
            s.AdditionalTcpPorts = null;

        if (string.IsNullOrWhiteSpace(s.NpcapAdapterFilter))
            s.NpcapAdapterFilter = null;
        else
            s.NpcapAdapterFilter = s.NpcapAdapterFilter.Trim();
    }

    /// <summary>Build libpcap BPF for <see cref="MhMissionSniffer"/> from primary + additional ports.</summary>
    public static string BuildTcpPortBpf(int primaryPort, int[]? additionalPorts)
    {
        if (primaryPort < 1 || primaryPort > 65535)
            primaryPort = DefaultGameTcpPort;

        var ports = new List<int> { primaryPort };
        if (additionalPorts is not null)
        {
            foreach (var p in additionalPorts)
            {
                if (p < 1 || p > 65535) continue;
                if (!ports.Contains(p)) ports.Add(p);
            }
        }

        if (ports.Count == 1)
            return $"tcp port {ports[0]}";

        return string.Join(" or ", ports.Select(p => $"tcp port {p}"));
    }
}
