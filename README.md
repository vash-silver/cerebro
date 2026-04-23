# Marvel Heroes — Standalone DPS Meter

Always-on-top WPF overlay that displays real-time DPS for Marvel Heroes
(Tahiti / MHServerEmu builds). Runs as its own process — no patching,
no DLL injection, no game-side hooks — by passively sniffing the client's
TCP/4306 traffic with Npcap.

```
┌──────────────────┐
│  BOSS DPS - Blade│
│        2.1M      │
│  Max hit: 487k   │
│  60s: 14.2M      │
│  ─────────────── │
│ ▶ Blade  100%   │
│   Storm   62%   │
│   Rogue   41%   │
└──────────────────┘
```

Right-click the overlay for the runtime menu (Boss-DPS-only filter, Exit).
Drag with the left mouse button to reposition; the overlay remembers its
location across restarts.

## Requirements

- **Windows 10 (1809+) or Windows 11** — uses PerMonitorV2 DPI awareness
- **[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)** for end users running prebuilt binaries
- **[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** for building from source
- **[Npcap](https://npcap.com/)** — install with the *"WinPcap API-compatible mode"* checkbox; loopback support is required if the game and server run on the same machine

## Build

```powershell
dotnet build MarvelHeroes.DpsMeter.sln -c Release
```

The exe lands in `MarvelHeroes.DpsMeter/bin/Release/net8.0-windows10.0.19041.0/`.

## Publish a portable build

Framework-dependent (small, requires .NET 8 runtime on target machine):

```powershell
dotnet publish MarvelHeroes.DpsMeter/MarvelHeroes.DpsMeter.csproj -c Release
```

Self-contained single-file (~80 MB, no runtime install needed):

```powershell
dotnet publish MarvelHeroes.DpsMeter/MarvelHeroes.DpsMeter.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Run

```powershell
dotnet run --project MarvelHeroes.DpsMeter/MarvelHeroes.DpsMeter.csproj
```

Start the meter **before** logging into the game so the sniffer captures
the initial `EntityCreate` burst. If you start mid-region the meter still
works — it has fallback heuristics — but nicknames may take longer to
resolve until peers move within AOI.

## Repo layout

```
MarvelHeroes.DpsMeter/   WPF app — overlay window, presenter, hero/boss tables, costume PNGs
NetworkSniffer/          PCAP capture, TCP reassembly, mux demux, NetMessagePowerResult parsing
Gazillion/               Marvel Heroes protobuf wire schema (sourced from MHServerEmu)
lib/                     Vendored Google.ProtocolBuffers.dll (proto2-era C# port required by Gazillion)
```

## Provenance

Extracted from the larger
[MarvelHeroesComporator](https://github.com/) tool.  The `NetworkSniffer/`
files retain their original `MarvelHeroesComporator.NetworkSniffer`
namespace so the sniffer code can be re-shared with that project verbatim.
The `Gazillion/` and `lib/` contents are vendored from
[MHServerEmu](https://github.com/Crypto137/MHServerEmu) (`EmuSource`).

## Persistence

The meter writes per-user state under
`%LocalAppData%\MarvelHeroesComporator\`:

| File                       | Purpose                                       |
| -------------------------- | --------------------------------------------- |
| `dps-overlay.json`         | Last window position                          |
| `dps-max-hits.json`        | Personal-best single hit per hero             |
| `dps-player-index.json`    | Learned dbId → nickname / current-hero map    |
| `dps-meter.log`            | Diagnostic log (sniffer + meter + presenter)  |

The folder name is intentionally shared with the upstream comporator app
so a user upgrading from the integrated overlay keeps their records.

## License

See `lib/Google.ProtocolBuffers.License` for the bundled protobuf DLL.
The rest of the source is unlicensed pending upstream decision — please
ask before redistributing.
