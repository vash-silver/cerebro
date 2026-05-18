using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Persisted watchlist for the Cooldown Tracker tab.  Modeled on
/// <see cref="TrackedBuffsConfig"/> but keyed on power prototype IDs (the integers we
/// receive in <c>NetMessageTryActivatePower</c>) instead of free-form short names.
///
/// <para>Keying off proto IDs has two consequences:</para>
/// <list type="bullet">
///   <item>The same power on different heroes (rare; powers are usually unique per
///         hero) collapses to one entry by design -- if Cyclops and Beast both had a
///         "Photon Blast" with the same proto, they'd share the tracked-cooldown
///         entry.  Almost never happens in practice; documented for completeness.</item>
///   <item>We don't lose entries when display names change between game patches.  A
///         data dump rename only affects the alias / icon resolution, not the
///         identity of a tracked power.</item>
/// </list>
///
/// <para>Lives at <c>%LocalAppData%\MarvelHeroesComporator\cooldown-watchlist.json</c>
/// -- separate from the buff watchlist / DPS settings file so feature iteration here
/// doesn't risk corrupting other settings.  Atomic write-temp + rename so a mid-save
/// process kill leaves the previous valid file intact.</para>
///
/// <para><b>Thread-safety:</b> same publish/subscribe pattern as
/// <see cref="TrackedBuffsConfig"/> -- <see cref="Current"/> is replaced atomically
/// via <see cref="ReplaceCurrent"/>; readers don't lock.</para>
/// </summary>
public sealed class CooldownTrackerConfig
{
    /// <summary>WeakAuras-style free-positioning mode for the floating cooldown overlay.
    /// When <c>true</c>, the overlay switches to a Canvas-based layout where each
    /// tracked power is positioned at its own <see cref="CooldownLayout.X"/> /
    /// <see cref="CooldownLayout.Y"/> coordinates from <see cref="Layouts"/>.  When
    /// <c>false</c>, the overlay renders a flowing horizontal chip strip (same as the
    /// buff overlay's strip mode).  Default <c>true</c> -- cooldown tracking only makes
    /// sense as a positioned HUD; the strip-mode fallback is here mostly for users who
    /// haven't placed their auras yet.</summary>
    public bool FreeLayoutMode { get; set; } = true;

    /// <summary>Master lock for the floating cooldown overlay -- click-through when
    /// <c>true</c>, mouse-interactive (drag / resize chips) when <c>false</c>.  Mirrors
    /// <see cref="TrackedBuffsConfig.OverlayLocked"/>.  Defaults to <c>false</c> so
    /// first-launch users can position the overlay before locking it for play.</summary>
    public bool OverlayLocked { get; set; } = false;

    /// <summary>The user's watchlist -- root prototype enum indices (the lower 32 bits of
    /// the wire <c>PowerPrototypeId</c> ulong) of powers they want to see on the
    /// cooldown overlay.  We persist as a <c>List&lt;uint&gt;</c> rather than a
    /// <c>HashSet</c> because System.Text.Json round-trips lists more cleanly and
    /// preserves user-curated ordering (handy when the strip-mode flow renders them in
    /// the order the user added them).  Conversion to a set is cheap on read.</summary>
    public List<uint> Tracked { get; set; } = new();

    /// <summary>User-configured cooldown duration in seconds, per tracked power.  No
    /// entry == 0 == "I haven't told Cerebro the cooldown yet" -- the chip renders but
    /// the radial sweep just shows the icon at full opacity all the time (no countdown
    /// to display).  The user adds a duration via a per-row input in the Cooldowns tab.
    ///
    /// <para>Stored as seconds (double) rather than ms because that's what the user
    /// types: a 20-second cooldown ability gets entered as <c>20</c>, not <c>20000</c>.
    /// We don't validate the value; negative or zero entries just suppress the sweep
    /// without affecting tracking.</para></summary>
    public Dictionary<uint, double> Cooldowns { get; set; } = new();

    /// <summary>Optional per-power icon override -- maps a watchlist proto ID to a
    /// pack URI or absolute file path.  Same pattern as
    /// <see cref="TrackedBuffsConfig.IconPaths"/>.  When unset we fall back to the
    /// auto-resolved icon via <see cref="PowerIconByProto"/>.</summary>
    public Dictionary<uint, string> IconPaths { get; set; } = new();

    /// <summary>Optional per-power display-name alias.  The Cooldowns tab uses this
    /// for the rename UI; the overlay chip is icon-only so aliases only surface in the
    /// tab.</summary>
    public Dictionary<uint, string> Aliases { get; set; } = new();

    /// <summary>Per-tracked-power position + scale in free-layout mode.  Keyed by the
    /// same proto ID used in <see cref="Tracked"/>.  Missing entries mean "no saved
    /// position" -- the chip renders at the default offset and the user drags it into
    /// place.</summary>
    public Dictionary<uint, CooldownLayout> Layouts { get; set; } = new();

    /// <summary>Resolve the layout for <paramref name="protoId"/> or return a default-
    /// positioned layout when none is saved.  Default position is (0, 0) -- the chip
    /// materializes in the overlay's top-left and the user drags from there.</summary>
    public CooldownLayout GetLayout(uint protoId)
    {
        if (!Layouts.TryGetValue(protoId, out var layout))
            return new CooldownLayout();
        return layout;
    }

    /// <summary>Resolve the cooldown duration (in seconds) for <paramref name="protoId"/>,
    /// or 0 when the user hasn't configured one yet.  A 0 result tells the renderer to
    /// keep the icon at full opacity (no progress sweep, no countdown text).</summary>
    public double GetCooldown(uint protoId)
        => Cooldowns.TryGetValue(protoId, out var sec) ? sec : 0.0;

    /// <summary>Resolve the user's icon-path override for <paramref name="protoId"/>,
    /// or <c>null</c> when no override is set (caller falls back to the auto-resolved
    /// icon from <c>PowerIconByProto</c>).</summary>
    public string? GetIconPath(uint protoId)
    {
        if (!IconPaths.TryGetValue(protoId, out var path)) return null;
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    /// <summary>Resolve the display name for a tracked power: alias if the user set
    /// one, otherwise the auto-resolved name from <c>PowerNamesByProto</c>, otherwise
    /// a stringified proto ID as a last-resort fallback.</summary>
    public string GetDisplayName(uint protoId)
    {
        if (Aliases.TryGetValue(protoId, out var alias) && !string.IsNullOrWhiteSpace(alias))
            return alias;
        var auto = PowerNamesByProto.Get(protoId);
        return !string.IsNullOrWhiteSpace(auto) ? auto : $"Power #{protoId}";
    }

    // ── Static current-config publish/subscribe ─────────────────────────────────────

    public static CooldownTrackerConfig Current { get; private set; } = new();

    public static void ReplaceCurrent(CooldownTrackerConfig next)
    {
        Current = next;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static event EventHandler? Changed;

    // ── Persistence ───────────────────────────────────────────────────────────────

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator", "cooldown-watchlist.json");

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
    };

    /// <summary>Load from disk if present, else return defaults.  Never throws --
    /// a corrupt file silently falls back to defaults.</summary>
    public static CooldownTrackerConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                using var stream = File.OpenRead(ConfigPath);
                var loaded = JsonSerializer.Deserialize<CooldownTrackerConfig>(stream, s_jsonOpts);
                if (loaded != null)
                {
                    // Re-create dictionaries to guarantee non-null (defensive against
                    // older JSON files that pre-date a field).
                    loaded.Tracked   ??= new List<uint>();
                    loaded.Cooldowns ??= new Dictionary<uint, double>();
                    loaded.IconPaths ??= new Dictionary<uint, string>();
                    loaded.Aliases   ??= new Dictionary<uint, string>();
                    loaded.Layouts   ??= new Dictionary<uint, CooldownLayout>();
                    return loaded;
                }
            }
        }
        catch { /* fall through */ }
        return new CooldownTrackerConfig();
    }

    /// <summary>Save the supplied config to disk.  Atomic: write to a temp file, then
    /// rename over the real one so a mid-save crash leaves the previous valid file.</summary>
    public static void Save(CooldownTrackerConfig config)
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

/// <summary>Per-power icon position + size in free-layout (WeakAuras-style) mode.
/// Mirrors <see cref="BuffLayout"/>; defaults to (0, 0) with a 64 px icon.</summary>
public sealed class CooldownLayout
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Size { get; set; } = 64;
}
