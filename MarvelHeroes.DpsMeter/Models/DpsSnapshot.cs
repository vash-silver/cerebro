using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarvelHeroes.DpsMeter.Models;

public sealed class DpsSnapshot
{
    public string   Id            { get; set; } = "";
    public DateTime SavedUtc      { get; set; }
    public string   Label         { get; set; } = "";
    public string   Mode          { get; set; } = "";
    public string   HeroName      { get; set; } = "";
    public double   Dps           { get; set; }
    public long     TotalDamage   { get; set; }
    public uint     MaxSingleHit  { get; set; }
    public bool     EncounterEnded { get; set; }
    public long     EncounterSelfTotal { get; set; }
    public List<HeroEntry>  Leaderboard     { get; set; } = new();
    public List<PowerEntry> PowerBreakdown  { get; set; } = new();

    public sealed class HeroEntry
    {
        public string Name       { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public bool   IsSelf     { get; set; }
        public double Dps        { get; set; }
        public long   Total      { get; set; }
        public double Percent    { get; set; }
    }

    public sealed class PowerEntry
    {
        public string Name        { get; set; } = "";
        public int    Hits        { get; set; }
        public long   TotalDamage { get; set; }
        public double Percent     { get; set; }
    }
}

public static class DpsReportStore
{
    public static string ReportsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarvelHeroesComporator", "reports");

    private static readonly JsonSerializerOptions s_opts = new() { WriteIndented = true };

    public static void Save(DpsSnapshot snap)
    {
        try
        {
            Directory.CreateDirectory(ReportsDirectory);
            var file = Path.Combine(ReportsDirectory, $"dps-{snap.Id}.json");
            File.WriteAllText(file, JsonSerializer.Serialize(snap, s_opts));
        }
        catch { /* best-effort */ }
    }

    public static List<DpsSnapshot> LoadAll()
    {
        var list = new List<DpsSnapshot>();
        try
        {
            if (!Directory.Exists(ReportsDirectory)) return list;
            foreach (var file in Directory.GetFiles(ReportsDirectory, "dps-*.json")
                                          .OrderByDescending(f => f))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var snap = JsonSerializer.Deserialize<DpsSnapshot>(json);
                    if (snap != null) list.Add(snap);
                }
                catch { /* skip corrupt file */ }
            }
        }
        catch { }
        return list;
    }

    public static void Delete(string id)
    {
        try
        {
            var file = Path.Combine(ReportsDirectory, $"dps-{id}.json");
            if (File.Exists(file)) File.Delete(file);
        }
        catch { }
    }
}
