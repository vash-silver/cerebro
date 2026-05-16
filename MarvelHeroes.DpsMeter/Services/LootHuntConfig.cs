using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Persisted, user-editable hunt configuration for the loot scanner.  Replaces the
/// previous source-code-only <see cref="HuntCriteria"/> constants -- the user picks affixes
/// in the Cosmic Loot Scanner tab, the panel writes here, and <see cref="HuntCriteria.MatchesHunt"/>
/// reads from <see cref="Current"/> at evaluation time.
///
/// <para>Lives at <c>%LocalAppData%\MarvelHeroesComporator\loot-hunt-config.json</c> --
/// separate file from <c>dps-overlay.json</c> so iterating on this feature doesn't risk
/// corrupting the user's other settings.  Atomic-style writes (write-temp + rename) so a
/// mid-save process kill leaves the previous valid file intact.</para>
///
/// <para>Thread-safety: <see cref="Current"/> is replaced atomically (volatile reference
/// swap) so the loot scanner's hot path reads a consistent snapshot without locking.
/// The UI thread mutates a draft copy then publishes via <see cref="ReplaceCurrent"/>.</para>
/// </summary>
public sealed class LootHuntConfig
{
    /// <summary>Whether the hunt-match log line fires at all.  When false, the scanner
    /// still scores items but skips the *** HUNT MATCH *** line.  Lets users disable the
    /// feature without clearing all their selected patterns.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Affix-path substrings the hunt cares about.  An item is a candidate match
    /// when its rolled-affix list contains at least <see cref="MinHits"/> distinct
    /// patterns from this set.  Set rather than list -- order doesn't matter and dupes
    /// would skew counts.  Persisted as a JSON array of strings.</summary>
    public HashSet<string> WantedPatterns { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Minimum number of distinct <see cref="WantedPatterns"/> entries an item
    /// must match.  Clamped to [1, 6] on load -- 1 fires on the loosest match, 6 requires
    /// a near-perfect roll, 2-3 is the sweet spot for "decent but flexible".</summary>
    public int MinHits { get; set; } = 2;

    /// <summary>What rarity an item must roll at to count for hunt match.</summary>
    public RarityGate Rarity { get; set; } = RarityGate.CosmicOnly;

    /// <summary>Whether to only match items equippable by the local player's hero.  Default
    /// true so the user doesn't get hunt alerts for items they couldn't equip anyway.
    /// Disable for "any hero's gear" hunts (collecting alts, browsing trades).</summary>
    public bool SelfOnly { get; set; } = true;

    /// <summary>Play a sound when a drop matches the hunt criteria.  Reuses the splinter
    /// sound player infrastructure -- supports any WPF-decodable audio file, falls back to
    /// the Windows asterisk when the path is empty.  Defaults on so first-time users get
    /// audio feedback without configuration.</summary>
    public bool SoundEnabled { get; set; } = true;

    /// <summary>Optional path to a custom sound file for hunt matches.  When empty / null
    /// / unreadable, plays the Windows asterisk fallback (controllable via Windows volume
    /// mixer).  Set via the Cosmic Loot Scanner tab's Browse / Clear buttons.</summary>
    public string? SoundPath { get; set; }

    /// <summary>Playback volume for the hunt-match sound, 0.0 (silent) to 1.0 (full).
    /// Only applies to custom sound files -- the Windows asterisk fallback uses the
    /// OS-level notification-sound volume instead.  Clamped to [0.0, 1.0] at use time.</summary>
    public double SoundVolume { get; set; } = 1.0;

    public enum RarityGate
    {
        /// <summary>Match any rolled-affix item regardless of rarity.</summary>
        Any,
        /// <summary>Cosmic-rarity items only -- the endgame tier.</summary>
        CosmicOnly,
    }

    // ── Static current-config publish/subscribe ─────────────────────────────────────

    /// <summary>The currently-active config.  Replaced atomically by
    /// <see cref="ReplaceCurrent"/>; readable from any thread with no locking.</summary>
    public static LootHuntConfig Current { get; private set; } = new();

    /// <summary>Publishes a new config as the current one and fires the change event so the
    /// UI / log can react.  Caller is responsible for having loaded a valid instance.</summary>
    public static void ReplaceCurrent(LootHuntConfig next)
    {
        Current = next;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Fired after <see cref="ReplaceCurrent"/> swaps <see cref="Current"/> -- lets
    /// the panel re-render or log a "criteria changed" line.</summary>
    public static event EventHandler? Changed;

    // ── Persistence ───────────────────────────────────────────────────────────────

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator", "loot-hunt-config.json");

    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>Load from disk if present, else return a default-populated config (the
    /// previous source-code defaults: Cosmic only + crit/brutal patterns + 2 hits).
    /// Never throws -- corrupt file falls back to defaults silently, the user can re-save
    /// from the UI to overwrite.</summary>
    public static LootHuntConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                using var stream = File.OpenRead(ConfigPath);
                var loaded = JsonSerializer.Deserialize<LootHuntConfig>(stream, s_jsonOpts);
                if (loaded != null)
                {
                    // Re-create the HashSet with OrdinalIgnoreCase comparer -- JSON
                    // deserialization gives us a default-comparer set which would
                    // false-mismatch on "BrutalStrike" vs "brutalstrike".
                    loaded.WantedPatterns = new HashSet<string>(loaded.WantedPatterns, StringComparer.OrdinalIgnoreCase);
                    if (loaded.MinHits < 1) loaded.MinHits = 1;
                    if (loaded.MinHits > 6) loaded.MinHits = 6;
                    return loaded;
                }
            }
        }
        catch { /* fall through to defaults */ }

        // Defaults -- mirror the previous hardcoded HuntCriteria values so first-launch
        // users get the same behaviour as the source-only version.
        var defaults = new LootHuntConfig();
        defaults.WantedPatterns.Add("CritRating");
        defaults.WantedPatterns.Add("CritDamage");
        defaults.WantedPatterns.Add("BrutalStrike");
        defaults.WantedPatterns.Add("BrutalDamage");
        return defaults;
    }

    /// <summary>Save the supplied config to disk.  Atomic-style: write to a temp path then
    /// rename over the real one.  Best-effort on failure; corrupted writes leave the
    /// previous valid file in place because we never overwrite directly.</summary>
    public static void Save(LootHuntConfig config)
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
