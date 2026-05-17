using System.Collections.Generic;

namespace MarvelHeroes.DpsMeter.Services;

/// <summary>
/// Maps a hero <b>root</b> prototype-enum index (the wire form used by item
/// <c>EquippableBy</c> fields and any other <c>Serializer.Transfer(ref PrototypeId)</c>
/// surface) to the hero's basename -- e.g. <c>91663u -&gt; "Nightcrawler"</c>.  The companion
/// to <see cref="HeroPrototypes"/> which is keyed by the <c>AvatarPrototype</c>-specific
/// enum used in <c>EntityCreate.baseData.protoIdx</c>.
///
/// <para><b>Why two tables?</b>  MHServerEmu has two parallel prototype enums in flight on
/// the wire (see the doc on <see cref="PowerNamesByProto"/> for the long version):
/// <list type="bullet">
///   <item>The type-specific <c>AvatarPrototype</c> enum (compact, ~62 entries; one per
///         shipped hero) -- used in <c>EntityCreate</c> for avatars.</item>
///   <item>The global root <c>Prototype</c> enum (sparse, ~93k entries; every prototype
///         shares one numbering) -- used in item archives' <c>EquippableBy</c> field.</item>
/// </list>
/// Same hero, two different numbers depending on which message it appears in.  The loot
/// scanner's SelfOnly filter needs both -- the EntityCreate to learn "the player is
/// avatar-enum X (== Nightcrawler)" and a translation to root-enum Y to compare against
/// the dropped item's <c>EquippableBy</c>.  This table bridges the gap by giving us a
/// string the two sides can both resolve to.</para>
///
/// <para><b>Source of truth:</b> generated from the full prototype dump produced by
/// <c>scripts/PrototypeEnumDumper</c> (<c>all-prototypes.txt</c>), filtered to
/// <c>Entity/Characters/Avatars/Shipping/*.prototype</c>.  The 62 entries below are the
/// shipped-hero list at the MHServerEmu 1.0.1 Calligraphy version; re-run the dumper if a
/// future build adds heroes.  No re-generation is needed for server-merge enum drift on the
/// USER side -- these indices live in the client-side prototype enum, which is determined by
/// the client's data files, not by the live server.  A user whose private/merged server
/// reshuffled their backend won't be affected because items still ship the root-enum index
/// the client expects to receive.</para>
/// </summary>
internal static class AvatarNamesByProto
{
    /// <summary>Returns the hero basename for <paramref name="rootProtoEnumIndex"/>, or
    /// <c>null</c> when the index isn't a shipped-hero avatar (item bound to nobody, item
    /// for a non-shipping prototype, drift between client and server, etc.).  Compare
    /// case-insensitively in callers -- the returned string preserves the canonical
    /// CamelCase form ("Nightcrawler", "MoonKnight").</summary>
    public static string? Get(uint rootProtoEnumIndex)
        => s_names.TryGetValue(rootProtoEnumIndex, out var n) ? n : null;

    /// <summary>Reverse lookup: hero basename -> root-enum index.  Case-insensitive.  Used
    /// by features that know the player's hero name (e.g. via the
    /// <c>AvatarPrototype</c>-side <see cref="HeroPrototypes"/> lookup) and need to compare
    /// against a root-enum value like <c>spec.EquippableByEnumIndex</c>.  Returns 0 when
    /// the name isn't in the catalog.</summary>
    public static uint GetProtoByName(string heroBasename)
    {
        if (string.IsNullOrEmpty(heroBasename)) return 0;
        return s_protoByName.TryGetValue(heroBasename, out uint p) ? p : 0;
    }

    /// <summary>True when the given root-enum index is one of the 62 shipped heroes.
    /// Convenience for "is this drop hero-bound at all" gating.</summary>
    public static bool IsKnownHero(uint rootProtoEnumIndex)
        => s_names.ContainsKey(rootProtoEnumIndex);

    private static readonly Dictionary<uint, string> s_names = new()
    {
        {   364u, "Nova" },
        {  2163u, "EmmaFrost" },
        {  2203u, "IronMan" },
        {  2380u, "BlackPanther" },
        {  3779u, "Psylocke" },
        {  6020u, "KittyPryde" },
        {  8416u, "Carnage" },
        {  8492u, "Deadpool" },
        { 10103u, "Ultron" },
        { 11198u, "NickFury" },
        { 18305u, "Hawkeye" },
        { 20227u, "Vision" },
        { 21341u, "MoonKnight" },
        { 23478u, "Punisher" },
        { 27398u, "Starlord" },
        { 31182u, "Blade" },
        { 31615u, "BlackBolt" },
        { 32696u, "Gambit" },
        { 33021u, "Rogue" },
        { 33296u, "Cyclops" },
        { 34378u, "Storm" },
        { 35436u, "HumanTorch" },
        { 35526u, "GreenGoblin" },
        { 38662u, "X23" },
        { 40091u, "Elektra" },
        { 40154u, "Thor" },
        { 41904u, "Hulk" },
        { 43768u, "WarMachine" },
        { 44320u, "Magneto" },
        { 44767u, "RocketRaccoon" },
        { 46821u, "GhostRider" },
        { 47432u, "Spiderman" },
        { 49088u, "DoctorStrange" },
        { 51288u, "BlackWidow" },
        { 52156u, "Iceman" },
        { 53754u, "CaptainAmerica" },
        { 56181u, "SilverSurfer" },
        { 56251u, "Colossus" },
        { 58754u, "Daredevil" },
        { 61987u, "InvisibleWoman" },
        { 62653u, "SheHulk" },
        { 62958u, "JeanGrey" },
        { 63322u, "BlackCat" },
        { 66024u, "Juggernaut" },
        { 66287u, "SquirrelGirl" },
        { 66347u, "Angela" },
        { 66799u, "IronFist" },
        { 66925u, "Wolverine" },
        { 68516u, "Taskmaster" },
        { 69826u, "ScarletWitch" },
        { 70656u, "Thing" },
        { 72381u, "LukeCage" },
        { 72742u, "Loki" },
        { 76210u, "WinterSoldier" },
        { 78988u, "Venom" },
        { 79421u, "Magik" },
        { 81727u, "Cable" },
        { 86024u, "Beast" },
        { 88349u, "MsMarvel" },
        { 88773u, "MrFantastic" },
        { 89591u, "DrDoom" },
        { 91548u, "AntMan" },
        { 91663u, "Nightcrawler" },
    };

    /// <summary>Reverse map built from <see cref="s_names"/> at type-init.  Same data, the
    /// other direction.  Case-insensitive comparer so callers don't have to remember the
    /// canonical casing for each hero ("nightcrawler", "Nightcrawler", "NIGHTCRAWLER" all
    /// resolve to the same proto).</summary>
    private static readonly Dictionary<string, uint> s_protoByName = BuildProtoByName();

    private static Dictionary<string, uint> BuildProtoByName()
    {
        var d = new Dictionary<string, uint>(s_names.Count, System.StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in s_names) d[kvp.Value] = kvp.Key;
        return d;
    }
}
