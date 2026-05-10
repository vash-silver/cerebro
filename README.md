# Marvel Heroes — Standalone DPS Meter

Always-on-top WPF overlay that displays real-time DPS for Marvel Heroes
(Tahiti / MHServerEmu builds). Runs as its own process — no patching,
no DLL injection, no game-side hooks — by passively sniffing the client's
TCP/4306 traffic with Npcap.

```
┌───────────────────────────────┐
│  BOSS DPS - Blade             │
│           2.14M               │
│  Max hit: 487k                │
│  live · Fight: 27.3M          │
│ ─────────────────────────────│
│ ▶ Blade  (ace42)   2.14M 100%│
│   Storm  (xguy)    1.32M  62%│
│   Rogue  (ace99)    879k  41%│
├ ── Boss Fight ───────────────┤
│           1.87M               │
│  live · Fight: 24.1M          │
│ ─────────────────────────────│
│ ▶ Blade             100%     │
│   Storm              61%     │
├ ── My Abilities ─────────────┤
│  Vorpal Slash   891k avg 42% │
│  Charge         512k avg 24% │
│  Bloodbath      309k avg 14% │
└───────────────────────────────┘
```

Right-click the overlay for the full settings menu. Left-drag to reposition.
Position, mode, and all settings persist across restarts.

---

## Features

### Live DPS overlay

- **Real-time DPS** computed over a rolling 60-second window, with instant
  live rate during active damage and smooth 60s-average fallback when idle.
- **Max single-hit** display alongside the live DPS number.
- **Detail line** shows current mode context: `live`, `60s avg`, `fight ended`,
  or `waiting for boss…`

### Two display modes

Switch between modes any time via the right-click menu:

| Mode | Description |
|---|---|
| **Overlay** | Borderless transparent window, always on top, click-through when idle |
| **Window** | Standard titled WPF window with taskbar presence and normal focus |

In overlay mode the window is non-activating so it never steals keyboard focus.
Clicking the ✕ on window mode switches back to the overlay rather than closing
the app.

### Party leaderboard

Top 5 heroes ranked by share of session or encounter damage with:
- Hero avatar portrait, hero name, and player nickname
- Live DPS and 60-second total per row
- Proportional bar visualization

### Boss encounter tracking (Boss-Only mode)

Dedicated encounter accumulator that tracks only damage dealt to boss entities:

- Resets automatically on each new boss encounter
- `Fight:` total shows running encounter damage separate from the 60s window
- Leaderboard switches to encounter-share ranking while a boss is alive and
  shows the frozen final breakdown after all bosses die
- Optional second section in the overlay shows boss-fight stats alongside the
  main DPS numbers — useful in non-boss-only mode

### Ability / power breakdown

Bottom section of the overlay shows the top 8 abilities ranked by damage:

- **Avg hit** per ability (total damage ÷ hits), right-sized in the overlay column
- **Total damage** and **% of total**
- Stacked color-coded segment bar above the rows showing relative contribution
- Individual bar behind each row

### Fight history & auto-save

Boss fights are saved automatically when the encounter ends (all bosses dead):

- Rolling cap of **50 auto-saves** — oldest are pruned automatically
- Manual save also available from the right-click menu at any time
- `AUTO` badge distinguishes auto-saves from manual snapshots in the list

### Report viewer

Browse saved fights via the right-click menu → **View fight history**:

**List panel (left)**
- Sort by: newest first · highest DPS · hero A→Z
- Filter by hero (dynamically populated from saved fights)
- `PB` (green) and `AUTO` (orange) badges per row
- Fight duration shown on each list item

**Detail panel (right)**
- Editable fight title — click to rename in-place, Enter to commit, Escape to cancel
- `★ PB` badge in the header when the fight set a personal best
- Mode, hero, DPS, duration, and max-hit stat badges
- **DPS sparkline** — bar chart of DPS sampled every 5 seconds over the fight
- Party leaderboard with DPS · Total · % columns
- Ability breakdown table: ABILITY · HITS · AVG HIT · MAX HIT · TOTAL · %
- **Totals row** below the ability table: combined hits, weighted avg hit, highest
  single hit, and grand total
- **Copy to clipboard** — formats the full fight summary for pasting to Discord
- **Auto-refreshes** when a new fight is auto-saved (FileSystemWatcher + 400 ms debounce)

### Personal bests

- Best DPS per hero is tracked across all sessions in `personal_bests.json`
- Auto-saved fights that beat the previous record are flagged `IsPersonalBest`
- `PB` badge appears in the fight list and `★ PB` badge in the detail header

### Overlay scale

Scale the overlay from 25 % to 200 % via the right-click menu (25 / 50 / 75 /
100 / 125 / 150 / 175 / 200 %). Scale persists across restarts.

---

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
MarvelHeroes.DpsMeter/   WPF app — overlay/live window, presenter, hero/boss tables,
│                         power breakdown, fight history, report viewer, costume PNGs
│  Controls/              DpsDisplayPanel UserControl (shared between both window modes)
│  Models/                DpsSnapshot, DpsReportStore, PersonalBestStore
│  Services/              DpsMeter aggregator, DpsOverlayPresenter, settings
│  Windows/               DpsOverlayWindow, DpsLiveWindow, ReportViewerWindow
NetworkSniffer/          PCAP capture, TCP reassembly, mux demux, NetMessagePowerResult parsing
Gazillion/               Marvel Heroes protobuf wire schema (sourced from MHServerEmu)
lib/                     Vendored Google.ProtocolBuffers.dll (proto2-era C# port required by Gazillion)
```

## Persistence

All per-user state lives under `%LocalAppData%\MarvelHeroesComporator\`:

| Path | Purpose |
|---|---|
| `dps-overlay.json` | Window position, mode (overlay/window), scale, visible sections |
| `dps-max-hits.json` | Personal-best single hit per hero (sniffer-level) |
| `dps-player-index.json` | Learned dbId → nickname / current-hero map |
| `personal_bests.json` | Best DPS per hero across all sessions |
| `reports/dps-*.json` | Individual fight snapshots (auto + manual saves) |
| `dps-meter.log` | Diagnostic log (sniffer + meter + presenter) |

The folder name is intentionally shared with the upstream comporator app
so a user upgrading from the integrated overlay keeps their records.

## Provenance

Extracted from the larger
[MarvelHeroesComporator](https://github.com/) tool.  The `NetworkSniffer/`
files retain their original `MarvelHeroesComporator.NetworkSniffer`
namespace so the sniffer code can be re-shared with that project verbatim.
The `Gazillion/` and `lib/` contents are vendored from
[MHServerEmu](https://github.com/Crypto137/MHServerEmu) (`EmuSource`).

## License

See `lib/Google.ProtocolBuffers.License` for the bundled protobuf DLL.
The rest of the source is unlicensed pending upstream decision — please
ask before redistributing.
