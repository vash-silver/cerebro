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
    public double Scale { get; set; } = 1.0;

    /// <summary>Last user-set overlay width / height in WPF device-independent units.  Default
    /// 0 means "no saved size" — the window auto-fits to its content on first launch via
    /// <c>SizeToContent=WidthAndHeight</c>, then captures the resulting dimensions so subsequent
    /// launches restore the same size.  Non-zero values are applied verbatim at startup,
    /// overriding auto-fit, so a user who manually resized to e.g. 420x540 gets that exact
    /// layout back next session.  Persisted independently of <see cref="Scale"/> — Scale
    /// affects the panel's RenderTransform; Width/Height affect the window frame size.</summary>
    public double OverlayWidth { get; set; } = 0;

    /// <summary>See <see cref="OverlayWidth"/>.</summary>
    public double OverlayHeight { get; set; } = 0;

    /// <summary>When <c>true</c>, a separate always-on-top floating "buff overlay" window
    /// is shown alongside the main DPS overlay.  The buff overlay only renders buffs on
    /// the user's <see cref="TrackedBuffsConfig.Tracked"/> watchlist (WeakAuras-style
    /// focused display), independent of whether the DPS overlay is visible.  Defaults to
    /// off -- the buff strip on the live dashboard remains the primary surface for users
    /// who haven't opted into the focused floating view.</summary>
    public bool ShowBuffOverlay { get; set; } = false;

    /// <summary>When <c>true</c>, a separate always-on-top floating "cooldown overlay"
    /// window is shown.  Renders user-tracked power cooldowns as WeakAuras-style
    /// icons: full opacity when ready, dimmed + countdown text + fill-from-bottom
    /// progress overlay while on cooldown.  Independent of <see cref="ShowBuffOverlay"/>
    /// and <see cref="ShowOverlay"/> so users can pick any combination.</summary>
    public bool ShowCooldownOverlay { get; set; } = false;

    /// <summary>Persisted geometry for the floating buff overlay.  Modeled on the DPS
    /// overlay's Left/Top/OverlayWidth/OverlayHeight pair so user-resized positions
    /// survive across launches.  Default positions place the window in the upper-right
    /// quadrant (offset from origin so it doesn't open on top of the DPS overlay's default
    /// upper-left position).  Zero <c>Width</c> / <c>Height</c> mean "auto-fit to content
    /// on first launch, then capture the resulting dimensions" -- same first-run pattern
    /// as the DPS overlay.</summary>
    public double BuffOverlayLeft { get; set; } = 400;
    public double BuffOverlayTop { get; set; } = 40;
    public double BuffOverlayWidth { get; set; } = 0;
    public double BuffOverlayHeight { get; set; } = 0;

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

    /// <summary>When <c>true</c> a second "BOSS FIGHT" panel is shown below the main leaderboard,
    /// always tracking boss-only encounter damage independently of the main section's filter.</summary>
    public bool ShowBossSection { get; set; } = false;

    /// <summary>When <c>true</c> the power breakdown panel is shown, listing the local player's
    /// top powers by damage with hit counts for proc identification.</summary>
    public bool ShowPowerBreakdown { get; set; } = false;

    /// <summary>When <c>true</c> the live dashboard renders the buff-tracking UI: the
    /// summed-stats panel ("Damage +140%" tiles) plus the two-tier buff strip (player-facing
    /// buffs on top, gear procs underneath).  Default <c>true</c> -- the feature is the
    /// primary surface of the buff-tracking work.  Users who find the chip strip noisy (a
    /// well-geared rotation can produce 15+ active buffs) can disable it from Settings; both
    /// rows then collapse and the leaderboard moves up to fill the space.</summary>
    public bool ShowBuffPanels { get; set; } = true;

    /// <summary>When <c>true</c> the floating overlay shows its DPS summary block (the "DPS"
    /// title, the large DPS number, max-hit triplet, and "idle / waiting for damage" status
    /// text).  When <c>false</c> only the leaderboard rows / Eternity Splinter badge / boss
    /// fight section render -- useful when the user wants a compact peer leaderboard without
    /// the big personal-DPS number stealing attention.  ES badge and boss section have their
    /// own toggles and are unaffected.  Default <c>true</c>.</summary>
    public bool ShowOverlayDpsSummary { get; set; } = true;

    /// <summary>When <c>true</c> the Eternity Splinter tracker status line is shown beneath the
    /// main DPS number.  Tracks the cooldown countdown between splinter drops so the
    /// user knows when killing another mob has a chance of yielding a splinter.  Default
    /// <c>true</c> in release -- the tracker is small, useful, and inactive (no visible state)
    /// until a splinter drops, so there's no downside to having it on out of the box.</summary>
    public bool ShowEternitySplinterTracker { get; set; } = true;

    /// <summary>When <c>true</c> a short sound is played once the moment the
    /// splinter cooldown expires.  When <see cref="SplinterCooldownSoundPath"/> is set to a
    /// valid file path, that file is played; otherwise we fall back to the Windows
    /// notification sound via <c>System.Media.SystemSounds.Asterisk</c>, which respects the
    /// user's OS-level sound mute / volume.  Default <c>true</c>.</summary>
    public bool SplinterCooldownSoundEnabled { get; set; } = true;

    /// <summary>Optional absolute path to a custom sound file played when the splinter
    /// cooldown expires.  Empty / null / nonexistent file falls back to the system
    /// notification sound.  Supports any format WPF's <c>MediaPlayer</c> can decode --
    /// WAV / MP3 / WMA / AAC are the practical set.  Set / cleared via the Settings tab's
    /// Browse / Clear buttons; persists across launches.</summary>
    public string? SplinterCooldownSoundPath { get; set; }

    /// <summary>Playback volume for the splinter alert sound, range 0.0 (silent) to 1.0
    /// (full).  Default 1.0.  Applied to <c>MediaPlayer.Volume</c> for custom sound files
    /// only -- the system <c>SystemSounds.Asterisk</c> fallback has no programmatic volume
    /// control and uses whatever the user has configured at the Windows level.  Values
    /// outside [0.0, 1.0] are clamped at use time.</summary>
    public double SplinterCooldownSoundVolume { get; set; } = 1.0;

    /// <summary>Optional separate sound file for the "Splinter dropped" event.  When set,
    /// fires INSTEAD of <see cref="SplinterCooldownSoundPath"/> at drop time -- the cooldown
    /// path still fires when the 6-minute cooldown expires.  Lets the user distinguish
    /// "I just got loot" from "I'm eligible for another drop" by ear.  When null/empty,
    /// the drop event falls back to <see cref="SplinterCooldownSoundPath"/> so existing
    /// single-sound configurations keep working unchanged.</summary>
    public string? SplinterDropSoundPath { get; set; }

    /// <summary>Playback volume for the drop-specific sound.  Independent of
    /// <see cref="SplinterCooldownSoundVolume"/> so users can have the drop sound louder
    /// (more urgent: "go grab it") and the cooldown-ready sound quieter (less urgent:
    /// "next drop is available").  Default 1.0; clamped to [0.0, 1.0].</summary>
    public double SplinterDropSoundVolume { get; set; } = 1.0;

    /// <summary>When <c>true</c> a global system-wide hotkey starts a fresh splinter cooldown
    /// from the moment it's pressed -- same effect as clicking "Arm Splinter cooldown now" in
    /// Settings, but accessible without leaving the game.  Critical workaround for crowded
    /// open-world maps where the proto-index-based auto-detection picks up every nearby
    /// player's drop indiscriminately: when you know YOU got a splinter, press the hotkey to
    /// peg the timer to your own pickup.  Default <c>true</c> with a Ctrl+Shift+E binding.</summary>
    public bool SplinterArmHotkeyEnabled { get; set; } = true;

    /// <summary>Win32 hotkey modifier mask (combination of MOD_ALT=1 / MOD_CONTROL=2 /
    /// MOD_SHIFT=4 / MOD_WIN=8).  Stored as the raw bitmask so registration is a direct
    /// pass-through to <c>RegisterHotKey</c>.  Default = MOD_CONTROL | MOD_SHIFT (6) so the
    /// stock binding is Ctrl+Shift+&lt;key&gt;.</summary>
    public uint SplinterArmHotkeyModifiers { get; set; } = 0x02 | 0x04;  // MOD_CONTROL | MOD_SHIFT

    /// <summary>Win32 virtual-key code (e.g. <c>VK_E</c> = 0x45).  Stored as the raw VK so
    /// registration is a direct pass-through to <c>RegisterHotKey</c>.  Default = 'E' (0x45)
    /// for "Eternity" -- combined with the modifier default gives Ctrl+Shift+E, which is
    /// unbound by Marvel Heroes' default keymap and by Windows itself.</summary>
    public uint SplinterArmHotkeyVk { get; set; } = 0x45;  // VK_E

    /// <summary>When <c>true</c>, a global system-wide hotkey toggles the visibility of
    /// ALL Cerebro overlay windows (DPS, Buff, Cooldown) at once.  Functions as a
    /// "boss key" -- one keypress hides everything, second press restores exactly what
    /// was showing before.  IMPORTANT: this toggle does NOT mutate
    /// <see cref="ShowOverlay"/> / <see cref="ShowBuffOverlay"/> /
    /// <see cref="ShowCooldownOverlay"/>; it applies a transient override on top so the
    /// user's persisted layout choices come back unchanged on the next press.
    /// Default <c>true</c> with a Ctrl+Shift+H binding.</summary>
    public bool ToggleOverlaysHotkeyEnabled { get; set; } = true;

    /// <summary>Win32 hotkey modifier mask for the toggle-all-overlays hotkey.  Stored
    /// as the raw bitmask so registration is a direct pass-through to
    /// <c>RegisterHotKey</c>.  Default = MOD_CONTROL | MOD_SHIFT (6) so the stock
    /// binding is Ctrl+Shift+&lt;key&gt;.</summary>
    public uint ToggleOverlaysHotkeyModifiers { get; set; } = 0x02 | 0x04;  // MOD_CONTROL | MOD_SHIFT

    /// <summary>Win32 virtual-key code for the toggle-all-overlays hotkey.  Default =
    /// 'H' (0x48) for "Hide" -- combined with the modifier default gives Ctrl+Shift+H,
    /// which is unbound by Marvel Heroes' default keymap and by Windows itself.</summary>
    public uint ToggleOverlaysHotkeyVk { get; set; } = 0x48;  // VK_H

    /// <summary>Last update-banner version the user dismissed via the "✕" close button.
    /// The in-app updater suppresses the banner whenever this matches the latest GitHub
    /// release tag -- so a one-click dismiss sticks across launches until a NEWER release
    /// is published.  Empty string means "never dismissed".  Stored as the raw tag
    /// string ("v2.9") rather than parsed components so comparison is a single string
    /// equality.</summary>
    public string DismissedUpdateVersion { get; set; } = "";

    /// <summary>UTC timestamp of the most recently observed (or manually armed) splinter
    /// drop.  Persisted so the ~6-minute cooldown countdown survives a Cerebro restart:
    /// if you got a splinter, quit the app, and relaunch within the cooldown window, the
    /// in-app timer continues where it left off instead of resetting to "ready".
    /// <see cref="DateTime.MinValue"/> means "no drop ever observed" -- treated as
    /// already-eligible by <c>EternitySplinterTracker</c>.
    ///
    /// <para>Serialized as ISO-8601 (System.Text.Json's default for DateTime) so the JSON
    /// is human-readable and time-zone-safe.</para></summary>
    public DateTime LastSplinterDropUtc { get; set; } = DateTime.MinValue;

    /// <summary>Legacy setting from the overlay-first era of the app.  Previously meant:
    /// <c>true</c> = show DPS in the regular titled <c>DpsLiveWindow</c>, <c>false</c> = show
    /// the transparent floating overlay.  As of the main-app GUI rework the app always shows
    /// the new <c>MainAppWindow</c>, so this property is no longer consulted at runtime --
    /// it stays on the type only so older <c>dps-overlay.json</c> files round-trip cleanly
    /// without losing the field.  See <see cref="ShowOverlay"/> for the new equivalent.</summary>
    public bool WindowMode { get; set; } = false;

    /// <summary>When <c>true</c> the floating always-on-top overlay window is visible in
    /// addition to the main app window.  Toggled via the "Show overlay" checkbox in the
    /// header of <c>MainAppWindow</c>.  Defaults to <c>false</c> -- new users get just the
    /// main app on first launch and opt into the overlay when they want it over the game.</summary>
    public bool ShowOverlay { get; set; } = false;

    /// <summary>When <c>true</c>, the floating overlay stays visible even when neither the
    /// game nor Cerebro is the foreground window.  Designed for multi-monitor users who
    /// park the overlay on a secondary screen and want it readable while focused on
    /// Discord / a browser / another app on a different monitor.  Single-monitor users
    /// should leave this off -- the default foreground-aware auto-hide stops the overlay
    /// from covering whatever else they're working on.  No effect when
    /// <see cref="ShowOverlay"/> is false.</summary>
    public bool PersistOverlay { get; set; } = false;

    /// <summary>When <c>true</c>, the floating DPS overlay window is click-through:
    /// <c>WS_EX_TRANSPARENT</c> is applied so mouse input passes through to whatever's
    /// underneath (the game, the desktop, another window).  The user can't drag,
    /// resize, or right-click the overlay until they unlock it.  Same lock concept as
    /// the buff-overlay's <c>TrackedBuffsConfig.OverlayLocked</c> but persisted here so
    /// it's right next to the DPS overlay's other geometry / visibility flags.
    ///
    /// <para>Defaults to <c>false</c> (unlocked) so first-launch users can drag the
    /// overlay into position and use its right-click menu.  Once placed, locking makes
    /// the overlay completely passive: it paints DPS / leaderboard / Splinter status
    /// over the game without ever intercepting clicks.  No effect when
    /// <see cref="ShowOverlay"/> is false.</para></summary>
    public bool OverlayLocked { get; set; } = false;

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
    /// When <c>true</c>, the log captures EVERYTHING the components emit -- including very
    /// chatty patterns like per-community-update <c>ModifyCommunityMember</c> lines, per-mob
    /// <c>boss-filter drop</c> / <c>prototype-cache cleanup</c> blocks, periodic
    /// <c>DpsMeter.State</c> / <c>PowerResultStats</c> dumps, and every
    /// <c>EntityCreate[Avatar]</c> arrival.  Useful while actively debugging a specific
    /// system but the resulting log can easily run 10x larger than the curated default.
    ///
    /// <para>When <c>false</c> (the default), <c>AppendLog</c> filters those high-volume
    /// patterns out at the write point so the log stays focused on high-signal events the
    /// user actually cares about (splinter drops, snapshot saves, app lifecycle, errors).
    /// Flip on via Settings → Diagnostics → "Verbose logging" when you want the full
    /// firehose.</para>
    /// </summary>
    public bool VerboseDiagnostics { get; set; } = false;

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

    /// <summary>Process-wide mirror of <see cref="VerboseDiagnostics"/>.  Same load-time
    /// sync pattern as <see cref="IsLoggingEnabled"/>: synced from the loaded instance
    /// when <see cref="Load"/> completes.  The <c>AppendLog</c> filter consults this on
    /// every line to decide whether to drop known-noisy patterns.</summary>
    public static bool IsVerboseDiagnosticsEnabled { get; set; } = false;

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
                    IsLoggingEnabled            = s.LoggingEnabled;
                    IsVerboseDiagnosticsEnabled = s.VerboseDiagnostics;
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
        IsLoggingEnabled            = fresh.LoggingEnabled;
        IsVerboseDiagnosticsEnabled = fresh.VerboseDiagnostics;
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

    /// <summary>Clamp ports, scale, and trim adapter filter so capture and serialization stay consistent.</summary>
    public static void Normalize(DpsOverlaySettingsFile s)
    {
        if (s.Scale < 0.25 || s.Scale > 3.0 || double.IsNaN(s.Scale))
            s.Scale = 1.0;

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
