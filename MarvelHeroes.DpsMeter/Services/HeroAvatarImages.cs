using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Maps a hero display name (as produced by <see cref="HeroPrototypes"/> / <see cref="HeroPowers"/>)
/// to the costume portrait bundled under <c>Images/costumes/</c>.
///
/// <para>
/// The costume PNGs ship as <c>Resource</c> entries (see the <c>&lt;Resource Include="Images\**\*.png"/&gt;</c>
/// line in <c>MarvelHeroesComporator.csproj</c>), so they are embedded in the assembly and resolvable
/// through a <c>pack://application:,,,/Images/costumes/…</c> URI at runtime — no file-system fallback
/// required.  That keeps the overlay render path allocation-free after first use: each image is
/// created once, frozen, and cached in <see cref="_cache"/> for the lifetime of the process.
/// </para>
///
/// <para>
/// The name→file map is hand-curated because the costume file-names don't follow a purely algorithmic
/// transform (compound hero names drop the space, a few use non-obvious costume suffixes, and one
/// filename — Invisible Woman — has a ship-level typo as <c>p_inivisiblewoman_classic.png</c>).  When
/// a new costume ships, extend the dictionary below; an unrecognised hero just falls back to the
/// textual name in the UI, it does not throw.
/// </para>
/// </summary>
internal static class HeroAvatarImages
{
    // File stems only (no ".png", no directory) so the runtime can build the pack URI in one spot.
    // Keys match HeroPrototypes.Names exactly — case, punctuation and hyphenation preserved.
    private static readonly IReadOnlyDictionary<string, string> FileStemByHero =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Angela"]           = "p_angela_aa",
            ["Ant-Man"]          = "p_antman_avengersnow",
            ["Beast"]            = "p_beast_uncanny",
            ["Black Bolt"]       = "p_blackbolt_anad",
            ["Black Cat"]        = "p_blackcat_classic",
            ["Black Panther"]    = "p_blackpanther_classic_vu",
            ["Black Widow"]      = "p_blackwidow_avengers_vu",
            ["Blade"]             = "p_blade_undeadagain",
            ["Cable"]            = "p_cable_modern",
            ["Captain America"]  = "p_captainamerica_classicvu",
            ["Carnage"]          = "p_carnage",
            ["Colossus"]         = "p_colossus_modern_vu",
            ["Cyclops"]          = "p_cyclops_astonishing_vu",
            ["Daredevil"]        = "p_daredevil_classic_vu",
            ["Deadpool"]         = "p_deadpool_classic_vu",
            ["Doctor Strange"]   = "p_drstrange_classic_vu",
            ["Dr. Doom"]         = "p_drdoom_ff",
            ["Elektra"]          = "p_elektra_classic",
            ["Emma Frost"]       = "p_emmafrost_modern_vu",
            ["Gambit"]           = "p_gambit_default",
            ["Ghost Rider"]      = "p_ghostrider_modern_vu",
            ["Green Goblin"]     = "p_greengoblin_classic",
            ["Hawkeye"]          = "p_hawkeye_classic_vu",
            ["Hulk"]             = "p_hulk_classic_vu",
            ["Human Torch"]      = "p_humantorch_modern",
            ["Iceman"]           = "p_iceman_classic",
            // Ship-level filename typo intentionally preserved ("inivisible" not "invisible").
            ["Invisible Woman"]  = "p_inivisiblewoman_classic",
            ["Iron Fist"]        = "p_ironfist_weaponofagamotto",
            ["Iron Man"]         = "p_ironman_extremis_vu",
            ["Jean Grey"]        = "p_jeangrey_phoenix_vu",
            ["Juggernaut"]       = "p_juggernaut_unstoppable",
            ["Kitty Pryde"]      = "p_kittypryde_astonishing",
            ["Loki"]             = "p_loki_traveling",
            ["Luke Cage"]        = "p_lukecage_modern_vu",
            ["Magik"]            = "p_magik_marvelnow",
            ["Magneto"]          = "p_magneto_marvelnow_white",
            ["Moon Knight"]      = "p_moonknight",
            ["Mr. Fantastic"]    = "p_mrfantastic_classic",
            // Ms. Marvel and Captain Marvel share the same costume file (same character, different
            // aliases across comic eras). The single p_captainmarvel_anad portrait is the shipping
            // art asset for both naming variants in the game's own UI.
            ["Ms. Marvel"]       = "p_captainmarvel_anad",
            ["Nick Fury"]        = "p_nickfury_shield",
            ["Nightcrawler"]     = "p_nightcrawler_modern",
            ["Nova"]             = "p_nova_prime_vu",
            ["Psylocke"]         = "p_psylocke_classic_vu",
            ["Punisher"]         = "p_punisher_modern_vu",
            ["Rocket Raccoon"]   = "p_rocketraccoon_modern",
            ["Rogue"]            = "p_rogue_modern",
            // Ship-level filename typo: "scarletwitc" is missing the trailing 'h'.
            ["Scarlet Witch"]    = "p_scarletwitcmodern_vu",
            ["She-Hulk"]         = "p_shehulk_sgf",
            ["Silver Surfer"]    = "p_silversurfer_classic",
            ["Spider-Man"]       = "p_spiderman_modern_vu",
            ["Squirrel Girl"]    = "p_squirrelgirl_classic_vu",
            ["Star-Lord"]        = "p_starlord_conquest",
            ["Storm"]            = "p_storm_modern_vu",
            ["Taskmaster"]       = "p_taskmaster",
            ["Thing"]            = "p_thing_classic",
            ["Thor"]             = "p_thor_modern_vu",
            ["Ultron"]           = "p_ultron_aou",
            ["Venom"]            = "p_venom_classic",
            ["Vision"]           = "p_vision_classic",
            ["War Machine"]      = "p_warmachine_initiative_vu",
            ["Winter Soldier"]   = "p_wintersoldier_classic",
            ["Wolverine"]        = "p_wolverine_modern_vu",
            ["X-23"]             = "p_x23_classic",
        };

    // Caches the decoded bitmap per hero so the overlay's 4 Hz refresh tick doesn't re-read the
    // embedded resource stream on every render. BitmapImage instances are frozen before caching so
    // they can be safely handed to any thread (WPF requires frozen Freezables for cross-thread use).
    private static readonly Dictionary<string, ImageSource?> _cache =
        new(StringComparer.Ordinal);

    private static readonly object _lock = new();

    /// <summary>
    /// Returns a cached, frozen <see cref="ImageSource"/> for the given hero display name, or
    /// <c>null</c> when no portrait is mapped (unknown hero, or a future addition not yet in the
    /// dictionary).  Callers should treat <c>null</c> as "hide the image slot and fall back to the
    /// textual hero name" — never throw.
    /// </summary>
    public static ImageSource? TryGet(string heroName)
    {
        if (string.IsNullOrEmpty(heroName)) return null;

        lock (_lock)
        {
            if (_cache.TryGetValue(heroName, out var cached))
                return cached;

            ImageSource? src = null;
            if (FileStemByHero.TryGetValue(heroName, out string? stem))
            {
                try
                {
                    // BitmapCacheOption.OnLoad + CacheOnDemand lets us close the resource stream
                    // immediately after decode; otherwise the BitmapImage would keep a weak
                    // reference to the (pack-URI) stream and incur a lookup on every render pass.
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(
                        $"pack://application:,,,/Images/costumes/{stem}.png",
                        UriKind.Absolute);
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    if (bi.CanFreeze) bi.Freeze();
                    src = bi;
                }
                catch
                {
                    // Unknown resource URI, missing asset on this build, or pack:// scheme not yet
                    // registered (very early startup): swallow and cache the null so we don't
                    // retry the expensive resolve on every tick.  Overlay degrades to text-only.
                    src = null;
                }
            }

            _cache[heroName] = src;
            return src;
        }
    }
}
