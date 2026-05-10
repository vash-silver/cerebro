# MarvelHeroes.DpsMeter — Claude Context

This file exists for Claude AI continuity. Paste its contents at the start of a new session
to restore context quickly.

---

## What this is

A standalone .NET 8 WPF overlay application that reads live Marvel Heroes Omega (MHO) game
traffic via Npcap and displays real-time DPS numbers. It replaces the built-in comporator DPS
panel and works as a floating overlay on top of the game.

---

## Solution layout

```
MarvelHeroes.DpsMeter.sln
├── MarvelHeroes.DpsMeter/           ← Main WPF app (OutputType=WinExe)
│   ├── App.xaml / App.xaml.cs
│   ├── Services/
│   │   ├── DpsMeter.cs              ← Core aggregation engine
│   │   ├── DpsOverlayPresenter.cs   ← Owns the two DpsMeter instances, overlay window, 4 Hz tick
│   │   ├── TestModeDataFeed.cs      ← Synthetic data feed for --test-mode
│   │   ├── BossPrototypes.cs        ← HashSet of boss prototypeEnumIndex values
│   │   ├── CombatantPrototypes.cs   ← HashSet of normal-mode combatant prototypes
│   │   ├── HeroPrototypes.cs        ← entityPrototypeEnumIndex → hero display name
│   │   ├── HeroPowers.cs            ← powerPrototypeEnumIndex → hero display name
│   │   └── PowerNames.cs            ← powerPrototypeEnumIndex → power display name
│   ├── Models/
│   │   └── DpsSnapshot.cs           ← DpsSnapshot, DpsReportStore, PersonalBestStore
│   └── Windows/
│       ├── DpsLiveWindow.xaml/.cs   ← Floating overlay (the "HUD" window)
│       ├── DpsOverlayWindow.xaml/.cs← Compact floating widget
│       └── ReportViewerWindow.xaml/.cs ← "View Reports" full-screen breakdown window
├── MarvelHeroes.DpsMeter.Tests/     ← xUnit test project
│   ├── DpsMeterCoreTests.cs         ← Engine tests (no sniffer, no game needed)
│   └── SnapshotStoreTests.cs        ← DpsReportStore + PersonalBestStore smoke tests
└── NetworkSniffer/                  ← Vendored from MarvelHeroesComporator
    └── MhMissionSniffer.cs          ← Packet capture → typed events
```

---

## Key data flow

```
Npcap (raw TCP)
  → MhMissionSniffer (parses NetMessagePowerResult etc.)
    → DamageDealtEvent / EntityKillEvent / EntityCreatedEvent / ...
      → DpsMeter._meter (normal mode: all damage)
      → DpsMeter._bossMeter (boss-only mode: boss targets only)
        → DpsOverlayPresenter.OnDecayTick (4 Hz)
          → overlay window update
          → auto-save DpsSnapshot on encounter end
```

---

## DpsMeter internals

### Two operating modes

| Mode        | Source dict                  | Reset trigger                          |
|-------------|------------------------------|----------------------------------------|
| Normal      | `_sessionTotalsPerOwner`     | Region change                          |
| Boss-only   | `_encounterTotalsPerOwner`   | Boss kill / region change / mode flip  |

### Self-detection

- Primary: `_localAvatarEntityIds` populated from `InventoryMoved` (server pushes the
  local player's avatar entity IDs). When non-empty this replaces the heuristic.
- Fallback: top-damage owner in the 60 s sliding window (`_totalsPerOwner`).

### Leaderboard pipeline (boss mode)

1. Collect rows from `_encounterTotalsPerOwner`
2. `CoalesceRowsByPetChainRoot` — fold pet/summon entity IDs into their avatar owner
3. `CoalesceRowsByPlayerName` — merge same-player rows (hero swap, proxy entities)
4. `CoalesceAnonymousRowsByHeroName` — 2-tier-summon fallback
5. Sort + truncate
6. **Post-coalesce DPS computation** — `row.Total60s / (fight_end - owner_first_hit)`
   (active-time DPS: fairer than fight_duration when players join at different times)

**Critical invariant**: DPS is computed AFTER all coalescing. Computing before coalescing
caused pet-owning heroes (Deadpool, Storm) to show zero DPS after their pet rows were merged.

### Active-time DPS formula

```
dps = encounter_total / max(1, fight_effective_end - owner_first_hit)
```

`_encounterFirstHitByOwner` tracks when each scoring owner first dealt boss damage.
Fallback denominator = fight duration (when OwnerId shifted to a summon after coalescing).

---

## MhMissionSniffer events

```csharp
event EventHandler<DamageDealtEvent>?      DamageDealt;       // every hit
event EventHandler<EntityKillEvent>?       EntityKilled;      // mob/boss death
event EventHandler<EntityDestroyEvent>?    EntityDestroyed;   // entity removed from world
event EventHandler<RegionChangedEvent>?    RegionChanged;     // zone transition
event EventHandler<EntityCreatedEvent>?    EntityCreated;     // entity spawned → prototype cached
event EventHandler<LocalPlayerIdentifiedEvent>? LocalPlayerIdentified;
event EventHandler<InventoryMovedEvent>?   InventoryMoved;    // avatar entity ID → local player mapping
event EventHandler<LocalAvatarObservedEvent>?   LocalAvatarObserved;
event EventHandler<CommunityMemberUpdatedEvent>? CommunityMemberUpdated; // player names
```

`DamageDealtEvent.TotalDamage` = `DamagePhysical + DamageEnergy + DamageMental`.
`UltimateOwnerEntityId` is who gets credit (avatar, not pet).

---

## DpsSnapshot persistence

- **Reports**: `%LocalAppData%\MarvelHeroesComporator\reports\dps-{id}.json`
- **Personal bests**: `%LocalAppData%\MarvelHeroesComporator\personal_bests.json`
- **Player index**: `%LocalAppData%\MarvelHeroesComporator\player-index.json`
- **Max hits**: `%LocalAppData%\MarvelHeroesComporator\dps-max-hits.json`
- **Settings**: `%LocalAppData%\MarvelHeroesComporator\dps-overlay.json`
- **Log**: `%LocalAppData%\MarvelHeroesComporator\dps-meter.log`

---

## Build and run

```powershell
# Normal run (requires Npcap + game)
dotnet run --project MarvelHeroes.DpsMeter

# Test mode (no Npcap needed — synthetic data feed runs)
dotnet run --project MarvelHeroes.DpsMeter -- --test-mode

# Run unit tests
dotnet test MarvelHeroes.DpsMeter.Tests
```

Requirements: .NET 8 SDK, Npcap (for normal mode), Windows 10 19041+ (WPF).

---

## Unit tests

Test project: `MarvelHeroes.DpsMeter.Tests` (xUnit, runs on build).

`DpsMeter` exposes an internal test constructor `DpsMeter(bool forTestingOnly)` — no sniffer
subscription, no disk I/O. Internal `TestInject*` / `TestRegister*` / `TestSet*` methods
simulate sniffer events. The main project grants access via `InternalsVisibleTo`.

Key test classes:
- `DpsMeterCoreTests` — encounter lifecycle, active-time DPS, pet coalescing regression
- `SnapshotStoreTests` — DpsReportStore round-trip, PersonalBestStore logic

---

## Test mode (`--test-mode`)

`TestModeDataFeed` injects synthetic damage events directly into the live meter instances.
Simulates: 7.5 s boss fight (Iron Man + Cyclops) → kill → 2.5 s idle → repeat.
No Npcap needed. Useful for UI testing without the game running.

Activated by: `dotnet run --project MarvelHeroes.DpsMeter -- --test-mode`
Or in Visual Studio debug launch profile: add `--test-mode` to application arguments.

---

## Important design decisions

### Why `_bossOnlyMode` has two separate DpsMeter instances

`DpsOverlayPresenter` creates `_meter` (all damage) and `_bossMeter` (boss filter on).
Both run simultaneously so the presenter can switch between them instantly without data loss.

### Why entity prototype cache is invalidated on kill/destroy

Lingering DOT ticks / late projectiles landing on a dead mob's entity ID would be admitted
by the combatant filter (the stale prototype is still in the cache). Fixed by removing the
cache entry in `OnEntityKilled` / `OnEntityDestroyed`.

### Why 60 s window is kept alongside encounter accumulator

Self-owner election (who is "me"?) still uses the 60 s sliding window in the heuristic-
fallback path (no `_localAvatarEntityIds`). Ripping it out would destabilise identification
on mid-session attach. Both windows coexist — the encounter accumulator drives boss-mode
leaderboard, the 60 s window drives normal-mode leaderboard and self-election.

### Why DPS is post-coalesce (added session: May 2025)

Pre-fix: DPS computed per raw entity before coalescing. Pet-owning heroes (Deadpool, Storm)
had their entity rows merged into one row by the coalescing passes; the new merged row had
`Dps = 0` (struct default). Fix: move DPS computation after all coalescing passes.

---

## Recent significant changes (May 2025)

- **Active-time DPS**: leaderboard now shows `total / (fight_end - player_first_hit)` instead
  of `total / fight_duration`. Fairer when players join at different points in the fight.
- **Per-player ability breakdown**: clicking any row in the Report Viewer shows that player's
  power breakdown (not just self). Data tracked via `_powerHitsByOwnerEncounter`.
- **DPS post-coalesce fix**: Deadpool/Storm no longer show zero DPS after pet merging.
- **Unit test project + test mode**: xUnit test project, `--test-mode` CLI flag.

---

## Known issues / TODOs

- `BossPrototypes` uses a dumper-based index with a known off-by-one bug for indices > 10000.
  Workaround: probe `idx - 1` at lookup time. Fix: regenerate when dumper is corrected.
- Player nickname resolution fails for players whose `EntityCreate` was missed (app launched
  mid-region). The `player-index.json` cache partially mitigates this.
- `--test-mode` boss entity ID is hardcoded (59u). If BossPrototypes changes, update the
  constant in `TestModeDataFeed.cs` and `DpsMeterCoreTests.cs`.
