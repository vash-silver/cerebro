using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Persisted watchlist for the Buff Tracker tab.  WeakAuras-style: instead of showing every
/// buff the server applies (~20-40 per heavy-combat tick), the user picks a small set of
/// names from the discovery UI and only those names render in the chip strip / focused
/// display.  This file holds that picked set.
///
/// <para>Lives at <c>%LocalAppData%\MarvelHeroesComporator\buff-watchlist.json</c> -- separate
/// from <c>dps-overlay.json</c> and <c>loot-hunt-config.json</c> so feature iteration here
/// doesn't risk corrupting the user's other settings.  Atomic write-temp + rename so a
/// mid-save process kill leaves the previous valid file intact.</para>
///
/// <para><b>Key choice: short names, not long names.</b>  The chip strip displays
/// <c>BuffDisplayClassifier.ShortenForChip(DisplayName)</c> -- e.g. "Empowered" rather than
/// "Team Buff Empowered5 Seconds".  The user clicks "Track" on what they see, so the
/// watchlist stores the short form too.  This keeps the user's mental model consistent:
/// "I told it to track Empowered, it shows me Empowered".  At filter time we compute the
/// short name of each active buff and look up in this set.</para>
///
/// <para><b>Thread-safety:</b> <see cref="Current"/> is replaced atomically (volatile
/// reference swap via <see cref="ReplaceCurrent"/>) so the filter hot path reads a
/// consistent snapshot without locking.  The UI thread mutates a draft copy then publishes
/// via <see cref="ReplaceCurrent"/>.  Same pattern as <see cref="LootHuntConfig"/>.</para>
/// </summary>
public sealed class TrackedBuffsConfig
{
    /// <summary>When true AND <see cref="Tracked"/> is non-empty, the chip strip only
    /// renders buffs whose short name appears in <see cref="Tracked"/>.  When false (the
    /// default), the strip renders everything that passes the existing
    /// <c>BuffDisplayClassifier</c> filter -- legacy behaviour.  An empty watchlist with
    /// this flag on is treated as "show nothing" rather than "show everything" so toggling
    /// the master switch can never silently re-enable the noise the user was trying to
    /// suppress.</summary>
    public bool OnlyShowTracked { get; set; } = false;

    /// <summary>WeakAuras-style free-positioning mode for the floating buff overlay.
    /// When <c>false</c> (default), the overlay renders all chips in a flowing
    /// <c>WrapPanel</c> -- the simple horizontal strip we shipped originally.  When
    /// <c>true</c>, the overlay switches to a Canvas-based layout where each tracked
    /// buff is positioned at its own <see cref="BuffLayout.X"/> / <see cref="BuffLayout.Y"/>
    /// coordinates from <see cref="Layouts"/>, and the user can drag chips around when
    /// unlocked to place them wherever on screen they want.  The inline dashboard
    /// chip strip is unaffected -- free layout applies only to the floating overlay
    /// because that's the surface users keep on top of the game.</summary>
    public bool FreeLayoutMode { get; set; } = false;

    /// <summary>Master lock for the floating buff overlay -- applies to BOTH strip and
    /// free-layout rendering modes.  When <c>true</c> the overlay is click-through
    /// (<c>WS_EX_TRANSPARENT</c>): the game receives all mouse input even though the
    /// overlay paints over it.  When <c>false</c> the overlay is interactive: in strip
    /// mode the user drags the body to reposition + grabs edges to resize; in free-
    /// layout mode each chip shows the yellow edit border / resize grip and can be
    /// dragged or resized individually.
    ///
    /// <para>Defaults to <c>false</c> (unlocked) so first-launch users can position
    /// the overlay before locking it for play.  Persisted so a user who carefully laid
    /// out their auras gets the same locked-click-through experience next session
    /// without re-toggling.</para></summary>
    public bool OverlayLocked { get; set; } = false;

    /// <summary>Per-tracked-buff position + scale in free-layout mode.  Keyed by the
    /// chip short name (same key as <see cref="Tracked"/> / <see cref="IconPaths"/> /
    /// <see cref="Aliases"/>).  Missing entries mean "no saved position" -- the chip
    /// renders at (0, 0) initially and the user drags it into place from there.
    /// Case-insensitive comparer so "empowered" and "Empowered" share one layout.</summary>
    public Dictionary<string, BuffLayout> Layouts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Look up the layout for <paramref name="shortName"/> in free-layout mode,
    /// or return a default-positioned layout when none is saved.  Default position is
    /// (0, 0) -- the chip materializes in the overlay's top-left and the user drags it
    /// from there.</summary>
    public BuffLayout GetLayout(string shortName)
    {
        if (string.IsNullOrEmpty(shortName) || !Layouts.TryGetValue(shortName, out var layout))
            return new BuffLayout();
        return layout;
    }

    /// <summary>When true, a derived "Stealthed / Invisible / Stealthed + Invisible" state
    /// pill is rendered above the buff strip whenever any active condition has property
    /// deltas that set <c>PropertyEnum.Stealth</c> non-zero or <c>PropertyEnum.Visible</c>
    /// to zero.  Default <b>off</b> -- the pill is only useful for heroes whose talents
    /// gate on stealth/invisibility (Nightcrawler's Surprise Attack, etc.); for everyone
    /// else it's dead pixels.  Toggle on from the Buff Tracker tab when playing one of
    /// those heroes.</summary>
    public bool ShowStealthStatePill { get; set; } = false;

    /// <summary>The user's watchlist -- short-form display names (post-ShortenForChip) of
    /// buffs they want to see in the focused chip strip.  Case-insensitive comparer so
    /// "empowered" and "Empowered" map to the same entry; the on-disk representation
    /// preserves the casing the user clicked.</summary>
    public HashSet<string> Tracked { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional per-buff icon override -- maps a watchlist entry's name to an
    /// absolute file path of an image to render as that buff's chip icon.  Empty / absent
    /// entries fall back to "no icon" (text-only chip, same as before).  Stored as a
    /// separate map rather than promoting <see cref="Tracked"/> to a list-of-objects so
    /// existing on-disk JSON files continue to deserialize cleanly -- this field defaults
    /// to an empty dictionary when missing.
    ///
    /// <para>Case-insensitive key comparison so an icon set for "empowered" still resolves
    /// when the chip strip shows "Empowered".  Values are absolute paths; we don't try to
    /// validate file existence here -- the chip-render path handles missing / unreadable
    /// files by falling back to the text-only chip.</para></summary>
    public Dictionary<string, string> IconPaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Convenience helper: returns the user-configured icon path for the buff
    /// whose chip-short-name is <paramref name="shortName"/>, or <c>null</c> when no icon
    /// is set.  Empty / whitespace paths are treated as "no icon" so a partially-cleared
    /// entry doesn't confuse the renderer.</summary>
    public string? GetIconPath(string shortName)
    {
        if (string.IsNullOrEmpty(shortName)) return null;
        if (!IconPaths.TryGetValue(shortName, out var path)) return null;
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    /// <summary>Optional per-buff display-name override.  Stored separately from
    /// <see cref="Tracked"/> (which keys on the original chip-short-name -- that's how
    /// the chip strip groups multi-condId stacks) so the user can rename a buff for
    /// display without breaking the underlying tracking.  Example: rename
    /// <c>"Teleport Stealth Combo"</c> to <c>"Stealth"</c> -- both the chip strip and the
    /// Buff Tracker watchlist show "Stealth" but the tracker still aggregates the same
    /// server-side conditions.
    ///
    /// <para>Key = original chip-short-name (the value in <see cref="Tracked"/>);
    /// value = user-typed alias.  Empty alias = no override.  Removing an entry restores
    /// the original name.</para></summary>
    public Dictionary<string, string> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolve the display name for a buff: returns the user's alias if set,
    /// otherwise the original <paramref name="shortName"/>.  Whitespace-only aliases
    /// fall back to the original (treat as "the user cleared the rename").</summary>
    public string GetDisplayName(string shortName)
    {
        if (string.IsNullOrEmpty(shortName)) return shortName;
        if (!Aliases.TryGetValue(shortName, out var alias)) return shortName;
        return string.IsNullOrWhiteSpace(alias) ? shortName : alias;
    }

    // ── Static current-config publish/subscribe ─────────────────────────────────────

    /// <summary>The currently-active config.  Replaced atomically by
    /// <see cref="ReplaceCurrent"/>; readable from any thread with no locking.</summary>
    public static TrackedBuffsConfig Current { get; private set; } = new();

    /// <summary>Back-compat shim for the old runtime-only edit-mode flag.  Edit mode
    /// is now the inverse of the persisted <see cref="OverlayLocked"/> -- when the
    /// overlay is unlocked the user can edit (drag/resize), when locked it's click-
    /// through.  Kept as a writable alias so any lingering callers compile and behave
    /// correctly; new code should read / write <see cref="OverlayLocked"/> directly.</summary>
    public static bool EditLayoutMode
    {
        get => !Current.OverlayLocked;
        set
        {
            // Mutating through the legacy property creates a fresh published config
            // so subscribers (BuffOverlayWindow, BuffTrackerPanel) see the change on
            // their next tick / Changed event.
            if (Current.OverlayLocked == !value) return;
            var src = Current;
            var next = new TrackedBuffsConfig
            {
                OnlyShowTracked      = src.OnlyShowTracked,
                ShowStealthStatePill = src.ShowStealthStatePill,
                FreeLayoutMode       = src.FreeLayoutMode,
                OverlayLocked        = !value,
                Tracked   = new HashSet<string>(src.Tracked, StringComparer.OrdinalIgnoreCase),
                IconPaths = new Dictionary<string, string>(src.IconPaths, StringComparer.OrdinalIgnoreCase),
                Aliases   = new Dictionary<string, string>(src.Aliases,   StringComparer.OrdinalIgnoreCase),
                Layouts   = new Dictionary<string, BuffLayout>(src.Layouts, StringComparer.OrdinalIgnoreCase),
            };
            Save(next);
            ReplaceCurrent(next);
        }
    }

    /// <summary>Publishes a new config as the current one and fires the change event so the
    /// chip strip / panel can react.  Caller is responsible for having loaded / mutated a
    /// valid instance.</summary>
    public static void ReplaceCurrent(TrackedBuffsConfig next)
    {
        Current = next;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Fired after <see cref="ReplaceCurrent"/> swaps <see cref="Current"/> --
    /// lets the BuffStripPanel re-render with the new filter and the BuffTrackerPanel
    /// refresh its tracked list.</summary>
    public static event EventHandler? Changed;

    // ── Persistence ───────────────────────────────────────────────────────────────

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator", "buff-watchlist.json");

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
    };

    /// <summary>Load from disk if present, else return a default-populated config (empty
    /// watchlist, filter off -- preserves legacy "show everything" behaviour for users who
    /// haven't opted in).  Never throws -- corrupt file falls back to defaults silently;
    /// the user can re-save from the UI to overwrite.</summary>
    public static TrackedBuffsConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                using var stream = File.OpenRead(ConfigPath);
                var loaded = JsonSerializer.Deserialize<TrackedBuffsConfig>(stream, s_jsonOpts);
                if (loaded != null)
                {
                    // Re-create the HashSet with OrdinalIgnoreCase comparer -- JSON
                    // deserialization gives us a default-comparer set which would
                    // false-mismatch on "empowered" vs "Empowered".
                    loaded.Tracked = new HashSet<string>(
                        loaded.Tracked ?? new HashSet<string>(),
                        StringComparer.OrdinalIgnoreCase);
                    // Same fixup for the icon-path map.  Defaults to empty dict for old
                    // JSON files that don't contain the field.
                    loaded.IconPaths = new Dictionary<string, string>(
                        loaded.IconPaths ?? new Dictionary<string, string>(),
                        StringComparer.OrdinalIgnoreCase);
                    loaded.Aliases = new Dictionary<string, string>(
                        loaded.Aliases ?? new Dictionary<string, string>(),
                        StringComparer.OrdinalIgnoreCase);
                    loaded.Layouts = new Dictionary<string, BuffLayout>(
                        loaded.Layouts ?? new Dictionary<string, BuffLayout>(),
                        StringComparer.OrdinalIgnoreCase);
                    return loaded;
                }
            }
        }
        catch { /* fall through to defaults */ }

        return new TrackedBuffsConfig();
    }

    /// <summary>Save the supplied config to disk.  Atomic-style: write to a temp path then
    /// rename over the real one.  Best-effort on failure; corrupted writes leave the
    /// previous valid file in place because we never overwrite directly.</summary>
    public static void Save(TrackedBuffsConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            string tempPath = ConfigPath + ".tmp";
            using (var stream = File.Create(tempPath))
                JsonSerializer.Serialize(stream, config, s_jsonOpts);
            if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
            File.Move(tempPath, ConfigPath);
        }
        catch { /* swallow; persistence is best-effort */ }
    }
}

/// <summary>Per-buff icon position + size in free-layout (WeakAuras-style) mode.  Each
/// tracked buff renders as a bare icon (no chrome, no name label) positioned at
/// (<see cref="X"/>, <see cref="Y"/>) inside the floating buff overlay window, sized to
/// <see cref="Size"/> px square.  The user drags icons around the overlay in edit mode
/// and the new coordinates round-trip to JSON via this object.
///
/// <para>Coordinates are in WPF device-independent pixels relative to the buff overlay
/// window's client area.  Defaults to (0, 0) with a 64px icon -- new tracked buffs
/// materialize in the overlay's top-left at a comfortable starting size, the user moves
/// them to taste.</para></summary>
public sealed class BuffLayout
{
    /// <summary>Horizontal position in DIPs from the buff-overlay window's left edge.</summary>
    public double X { get; set; }

    /// <summary>Vertical position in DIPs from the buff-overlay window's top edge.</summary>
    public double Y { get; set; }

    /// <summary>Side length of the icon's bounding square in DIPs.  Stored as a single
    /// dimension because icons are always square (the underlying PNGs are 40 px squares;
    /// non-square scaling would just distort them).  Default 64 px is comfortable on a
    /// 1080p display; users on 4K often bump to 96 or 128.</summary>
    public double Size { get; set; } = 64;
}
