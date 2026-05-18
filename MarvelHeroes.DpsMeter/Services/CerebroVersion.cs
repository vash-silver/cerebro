using System;
using System.Reflection;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Single source of truth for the current Cerebro version string.  Reads the
/// <c>AssemblyInformationalVersion</c> attribute baked in at build time by the
/// <c>&lt;Version&gt;</c> property in the .csproj (and overridden by publish.ps1 via
/// <c>-p:Version=&lt;tag&gt;</c> for release builds).
///
/// <para>Surface area is intentionally tiny -- this is used by the in-app updater to
/// compare against the latest GitHub release tag, by the Settings tab's About panel, and
/// by any diagnostic banner that wants to surface "you're running v2.9 not v2.8".</para>
/// </summary>
internal static class CerebroVersion
{
    /// <summary>Free-form version string from <c>AssemblyInformationalVersionAttribute</c>,
    /// or the fixed-width <c>AssemblyVersionAttribute</c> if the informational one is
    /// missing.  Typical values: <c>"2.8.0"</c> (release build), <c>"2.8.0+commitsha"</c>
    /// (some CI configurations), <c>"1.0.0.0"</c> (fallback when nothing was baked in).</summary>
    public static string DisplayVersion { get; } = ResolveDisplayVersion();

    /// <summary>Parsed semantic-version triplet for ordered comparison.  Pre-release
    /// suffixes (e.g. <c>-rc1</c>, <c>+sha</c>) are stripped before parsing -- two
    /// versions with the same triplet are considered equal for update-check purposes,
    /// which is fine because we only ever advertise full tags as releases.</summary>
    public static Version Parsed { get; } = ResolveParsedVersion();

    private static string ResolveDisplayVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (info != null && !string.IsNullOrWhiteSpace(info.InformationalVersion))
            return info.InformationalVersion;
        var fallback = asm.GetName().Version;
        return fallback?.ToString() ?? "0.0.0";
    }

    private static Version ResolveParsedVersion()
    {
        // Strip anything from the first '-' or '+' (pre-release / build metadata) so
        // System.Version.Parse accepts what's left.  "2.8.0-rc1+abcdef" -> "2.8.0".
        string s = DisplayVersion;
        int cut = s.IndexOfAny(new[] { '-', '+' });
        if (cut > 0) s = s.Substring(0, cut);
        return Version.TryParse(s, out var v) ? v : new Version(0, 0, 0);
    }
}
