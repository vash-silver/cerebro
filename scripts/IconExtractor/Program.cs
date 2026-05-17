// One-shot UE3 .upk texture extractor.  Pulls every Texture2D export out of an MH
// MarvelUIIcons.upk-style asset pack and writes each as a PNG named by the texture's
// object name (e.g. "Power_Storm_Typhoon.png").  Leverages AlexBond2/MHUpkManager's
// UpkManager + DDSLib libraries so we don't have to roll a UE3 parser ourselves.
//
// Usage:
//   IconExtractor <input.upk> [<output-dir>]
//   IconExtractor <input-dir> [<output-dir>]   (extracts every .upk in the dir)
//
// Designed to run ONCE during build-prep when regenerating the bundled icon set after
// a fresh MH client install / data update.  Output goes into the standard Cerebro
// Images/powers/ folder so the .csproj's <Resource Include="Images\powers\*.png"/>
// glob picks it up automatically.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using DDSLib;
using UpkManager.Models;
using UpkManager.Models.UpkFile.Engine.Texture;
using UpkManager.Models.UpkFile.Objects;
using UpkManager.Repository;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: IconExtractor <input.upk OR input-dir> [<output-dir>]");
    Console.Error.WriteLine("  Output defaults to: power-icons/ next to the EXE");
    return 1;
}

string input  = args[0];
string output = args.Length > 1 ? args[1] : "power-icons";
Directory.CreateDirectory(output);

// Resolve the input -- either a single .upk or a directory of them.  The MH client's
// CookedPCConsole folder has 15k .upk files; we only want the ICO__MarvelUIIcons_* ones
// for power icons, so when a directory is passed we filter to that prefix.
string[] upkFiles;
if (File.Exists(input))
{
    upkFiles = new[] { input };
}
else if (Directory.Exists(input))
{
    upkFiles = Directory.GetFiles(input, "ICO__MarvelUIIcons*_SF.upk", SearchOption.TopDirectoryOnly);
    Console.WriteLine($"Directory mode: found {upkFiles.Length} ICO__MarvelUIIcons*_SF.upk files in {input}");
}
else
{
    Console.Error.WriteLine($"Input not found (not a file or directory): {input}");
    return 1;
}

var repo = new UpkFileRepository();
int totalTextures = 0, totalWritten = 0, totalSkipped = 0, totalFailed = 0;
var manifest = new System.Collections.Generic.List<(string Name, string Path, string SourceUpk)>();

foreach (var upkPath in upkFiles)
{
    Console.WriteLine($"\n== {Path.GetFileName(upkPath)} ==");
    try
    {
        var header = await repo.LoadUpkFile(upkPath);
        // ReadHeaderAsync parses the name / import / export tables.  The progress
        // callback is fired in chunks; we ignore it -- this is a batch one-shot tool.
        await header.ReadHeaderAsync(progress => { /* noop */ });

        // Diagnostic: dump the class-name histogram so we can see what's in the pack.
        var classHist = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var ex in header.ExportTable)
        {
            string cn = ex.ClassReferenceNameIndex?.Name ?? "<null>";
            classHist[cn] = (classHist.TryGetValue(cn, out int existing) ? existing : 0) + 1;
        }
        Console.WriteLine($"  Export table: {header.ExportTable.Count} entries, classes:");
        foreach (var kvp in classHist.OrderByDescending(k => k.Value).Take(15))
            Console.WriteLine($"    {kvp.Value,5}x  {kvp.Key}");

        // Iterate the export table; lazy-parse each entry's UnrealObject when we touch
        // it.  ParseUnrealObject(skipProperties=false, skipParse=false) reads the full
        // object including its property block and untagged data (the texture mips).
        foreach (var export in header.ExportTable)
        {
            // Quickly filter by class-name string so we don't pay the parse cost for the
            // ~thousands of non-Texture2D exports in the larger packs.  The class name
            // is "Texture2D" verbatim on the wire.  ClassReferenceNameIndex is an FName;
            // its .Name property is the string we want.
            string className = export.ClassReferenceNameIndex?.Name ?? "";
            if (!className.Equals("Texture2D", StringComparison.OrdinalIgnoreCase)) continue;

            totalTextures++;
            string texName = export.ObjectNameIndex?.Name ?? $"unnamed_{totalTextures}";

            try
            {
                if (export.UnrealObject == null)
                    await export.ParseUnrealObject(skipProperties: false, skipParse: false);

                if (export.UnrealObject is not IUnrealObject u || u.UObject is not UTexture2D tex)
                {
                    totalSkipped++;
                    continue;
                }

                // GetObjectStream picks the largest mip with non-empty data and wraps it
                // in a DDS-header + raw mip stream.  Returns null when ALL mips are
                // in an external Texture File Cache (.tfc) -- which doesn't apply to UI
                // icons (those are small and inlined), so we treat null as "skip".
                var ddsStream = tex.GetObjectStream();
                if (ddsStream == null || ddsStream.Length == 0)
                {
                    totalSkipped++;
                    Console.Error.WriteLine($"  ! {texName}: no inline mip data (probably TFC-only)");
                    continue;
                }

                // Decode DDS -> BitmapSource via DDSLib, then encode PNG with WPF.  PNG
                // output is the lingua franca for WPF Resources and Avalonia / Skia for
                // future cross-platform work.
                var dds = new DdsFile();
                ddsStream.Position = 0;
                dds.Load(ddsStream);

                string outPath = Path.Combine(output, texName + ".png");
                using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(dds.BitmapSource));
                    encoder.Save(fs);
                }
                totalWritten++;
                manifest.Add((texName, Path.GetFileName(outPath), Path.GetFileName(upkPath)));

                if (totalWritten % 100 == 0)
                    Console.WriteLine($"  ... {totalWritten} icons written");
            }
            catch (Exception ex)
            {
                totalFailed++;
                Console.Error.WriteLine($"  ! {texName}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAILED to load {upkPath}: {ex.GetType().Name}: {ex.Message}");
    }
}

// Write a manifest mapping (textureName -> filename, sourceUpk) so the downstream
// PowerIconByProto.cs generator can correlate the AssetName from MHServerEmu's
// PowerPrototype.IconPath against the actual file we extracted.
string manifestPath = Path.Combine(output, "icons-manifest.txt");
using (var w = new StreamWriter(manifestPath))
{
    w.WriteLine($"// Auto-generated by IconExtractor.  {manifest.Count} icons.");
    w.WriteLine("// Format: <texture-name> | <output-png-filename> | <source-upk>");
    foreach (var (n, p, s) in manifest.OrderBy(t => t.Name))
        w.WriteLine($"{n} | {p} | {s}");
}

Console.WriteLine();
Console.WriteLine($"=== Done ===");
Console.WriteLine($"  Texture2D exports seen:  {totalTextures}");
Console.WriteLine($"  Written as PNG:          {totalWritten}");
Console.WriteLine($"  Skipped (no inline data): {totalSkipped}");
Console.WriteLine($"  Failed:                  {totalFailed}");
Console.WriteLine($"  Output:                  {Path.GetFullPath(output)}");
Console.WriteLine($"  Manifest:                {Path.GetFullPath(manifestPath)}");
return 0;
