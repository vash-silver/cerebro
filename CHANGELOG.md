# Cerebro Changelog

All notable changes per release. Follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
conventions; this file is bundled into the app and viewable from **Settings → About → View changelog**.

The in-app updater (introduced in v2.10) pulls release notes from the GitHub release page
verbatim; this file is the authoritative running log.

---

## [Unreleased]

Nothing yet. Add entries here under `Added` / `Changed` / `Fixed` / `Removed`.

---

## [2.13] — 2026-05-19

### Added
- **"Hide all overlays" global hotkey** (boss-key). Default `Ctrl+Shift+H`.
  System-wide keypress that toggles the DPS, Buff, and Cooldown overlays all
  at once. Rebindable / disable from Settings. Stashes the pre-hide state in
  memory so pressing again restores exactly what was visible before (a
  "1-of-3 was enabled" preference round-trips faithfully).
- **CHANGELOG.md** bundled into the app and surfaced via the new
  **Settings → About → View changelog** button. Same file you're reading
  right now; the viewer renders it as plain text with an "Open on GitHub"
  fallback button.
- **`CLAUDE.md`** project-memory file at the repo root. Codifies the
  required pre-release workflow (CHANGELOG + README before publish), XAML
  comment gotchas, and a quick architecture cheat-sheet so future
  contributors don't have to re-explore.

### Changed
- **Header layout compacted to a single row.** The four checkboxes that
  used to stack vertically (Show overlay / Persist overlay / Show buff
  overlay / Show cooldown overlay) now sit horizontally under an
  *Overlays:* group label with shortened labels (`DPS`, `Buff`,
  `Cooldown`) and a vertical separator before *Persist when alt-tabbed*.
  Tooltips spell out the full meaning of each. Win: ~80 px of vertical
  header space.
- **Boss-key hotkey now flips the persisted Show flags** instead of
  applying a transient hide-override. Header checkboxes stay in sync
  with what's actually on screen — press the hotkey, the checkboxes
  uncheck; press again, they re-check (restored from the in-memory
  stash). Closing Cerebro while overlays are hotkey-hidden keeps them
  hidden on next launch (the persisted flags reflect what was visible
  at close-time).
- **Overlay scale applies live.** Dragging the slider in Settings →
  Overlay scale now resizes the floating DPS overlay immediately rather
  than waiting for a restart. Wires through a new `OverlayScaleChanged`
  event from the Settings panel up to the presenter and into the
  `DpsDisplayPanel.SetScale` method.
- README refreshed to cover Buff Tracker, Cooldown Tracker, click-through
  lock, in-app updater, the boss-key hotkey, and the new compact header.
  Feature sections that lagged the codebase through v2.9 / v2.10 / v2.12
  are now in sync.

### Fixed
- **Pre-existing inconsistency**: the helpers `SetBuffOverlayVisible` /
  `SetCooldownOverlayVisible` didn't update the header checkbox visual
  when called from non-checkbox paths (only `SetOverlayVisible` did).
  Both now call `SetShowXxxOverlayChecked` to keep the visual in sync.
  Surfaced because the boss-key hotkey needed this; user-facing impact
  starts from v2.13.

---

## [2.12] — 2026-05-19

### Fixed
- **Hero name displayed as the wrong character after long sessions.** Marvel
  Heroes reuses entity-id slots aggressively; in a multi-hour session the same
  id can be an NPC (e.g. Juggernaut) early on and the local player's avatar
  (e.g. Cyclops) later. The proto cache updated correctly on id-reuse, but the
  hero-name cache stayed pinned to the *old* hero because every write site was
  `ContainsKey`-guarded. The id-reuse path now also evicts the stale
  hero-name + local-avatar entries so the next signal repopulates with the
  correct hero.
- **Every diagnostic log line written twice.** `App.xaml.cs` set
  `_sniffer.Diagnostic = AppendLog` at sniffer construction (so the
  device-probe messages have a log target during startup). The presenter then
  chained another `AppendLog` on top. Both implementations wrote to the same
  `dps-meter.log`. The presenter now overwrites the sink outright; one entry
  per event.

### Added
- **Cooldown Tracker now learns multi-charge abilities** (Bamf Bomb,
  Nightcrawler Teleport, etc.). Uses **multi-cast decrement detection**: the
  first cast stashes a small-int candidate; the second cast promotes it to a
  charge signature only if the value is strictly lower. If a cooldown
  signature is learned for the same power in the same window, pending charge
  candidates are discarded (incidental counter, not a real charge). Closes
  the v2.10 known limitation. Charged abilities now render a yellow `xN`
  corner badge with the current charge count.

---

## [2.11] — 2026-05-18

Smoke-test release. Pure version bump (`2.10.0 → 2.11.0`); no functional
changes. Existed so a running v2.10 had something newer to chase, exercising
the end-to-end auto-update flow that v2.10 shipped.

---

## [2.10] — 2026-05-18

### Added
- **One-click self-updater** (Phase 2). v2.9's banner opened the GitHub
  release page in your browser; v2.10 closes the loop: click "Update now",
  the new version downloads, verifies SHA-256, swaps the running EXE, and
  relaunches. Works around Windows' "can't replace a running EXE"
  restriction via a small PowerShell bootstrap script that waits for the
  parent process to exit, renames the current EXE to `.old`, moves the new
  one in, and self-cleans.
- **In-place rollback** in the bootstrap — if the swap fails partway, the
  `.old` is restored so you're never stranded.
- **Fallbacks** — on any failure (network, hash mismatch, write-permission),
  the banner reverts to `Update failed: <reason>` and reveals an "Open
  release page" button. The bootstrap logs to
  `%TEMP%\cerebro-update-*.log` for postmortem.

---

## [2.9] — 2026-05-17

### Added
- **WeakAuras-style free-layout Buff Overlay.** Toggle from the Buff Tracker
  tab. Each tracked buff renders as a bare icon at a user-positioned (X, Y)
  with a user-configurable size. Drag chips anywhere on screen (full-screen
  transparent canvas, not bounded by the previous strip rect); resize via
  the corner grip. INPC + ObservableCollection incremental sync so fast
  drag/resize gestures don't lose mouse capture mid-stroke.
- **Click-through lock for both overlays.** Buff Tracker tab → "Lock overlay
  (click-through)" applies to the buff overlay. Settings tab → "Lock overlay
  (click-through)" applies to the floating DPS overlay. Locked =
  `WS_EX_TRANSPARENT`, game gets all mouse input. Unlocked = drag/resize as
  before. Persisted across launches.
- **In-app updater banner** (Phase 1, soft). Auto-checks GitHub on startup;
  shows a dismissible banner when a newer release is published. "Download"
  button opened the release page in a browser (Phase 2 in v2.10 makes this
  one-click). Dismissal per-version sticks across launches.
- **Cooldown Tracker** (Phase 1). New tab + separate floating
  WeakAuras-style overlay. Server-authoritative cooldown durations via
  `NetMessageSetProperty` deltas (CDR-aware). Empirical signature learning
  for MH 2.16's property-enum layout. Per-row icon override; auto-resolved
  power names + icons via `PowerNamesByProto` / `PowerIconByProto`.
- **Sniffer additions**: `LocalPowerActivated` event (carries
  `PowerPrototypeId`) and `PropertyChanged` event (raw
  `NetMessageSetProperty` / `NetMessageRemoveProperty` deltas).

### Known limitations (closed in v2.12)
- Multi-charge abilities (Bamf Bomb, Teleport) didn't track per-charge —
  cooldown only registered after all charges were spent. *Fixed in v2.12*
  via multi-cast decrement detection.

---

## [2.8] — 2026-05-17

### Added
- **Buff Tracker tab.** WeakAuras-style watchlist for tracked buffs.
  Discover any buff from "Currently active" / "Recently seen", click Track,
  the chip strip filters down to your picks. Per-buff custom display name
  (rename); per-buff icon override.
- **Per-buff icons from a bundled in-game pack** (~2,300 icons extracted
  from the MH client's `ICO__MarvelUIIcons_SF.upk` + `Talents_SF.upk` via
  the `scripts/IconExtractor` UE3 Texture2D decoder). Searchable grid
  picker via the Browse… button. `PowerIconByProto` auto-suggests an icon
  at Track time from the buff's source power.
- **Derived "Stealthed / Invisible" state pill** above the buff strip when
  active conditions apply `PropertyEnum` 899 / 993 deltas (Nightcrawler's
  Surprise Attack visibility, etc.). Opt-in.
- **Event-driven UI updates** via `BuffTracker.BuffChanged`: sub-tick
  latency on add/remove so short-window buffs (e.g. 1.5 s Bamf stealth)
  flash up reliably instead of being missed by the 4 Hz periodic poll.
- **Floating Buff Overlay window** (separate from the DPS overlay).
- **Splinter cooldown persistence** — the `~6 minute` countdown survives
  app restarts via `LastSplinterDropUtc` in the settings file.

---

## [1.1] — earlier May 2026

### Added
- **Cosmic Loot Scanner tab.** Hunt mode: curated affix checklist
  (25 grouped affixes across Offensive/Defensive/Sustain/Mobility/
  Attributes/Specialized), minimum-hits slider, rarity gate (Any /
  Cosmic-only), self-filter (Only items for my current hero), alert
  sound. Fires a `*** HUNT MATCH ***` line in the diagnostic log and
  optionally plays an alert sound when a drop matches.
- **Persist overlay** header checkbox. When on, the floating overlay stays
  visible even when Marvel Heroes isn't the foreground window — for
  multi-monitor users who park the overlay on a secondary screen.
- **Buff Stats panel** on the live dashboard ("Damage +140 %" tiles
  summarizing damage / crit / brutal bonuses from active buffs).

### Changed
- **Diagnostics tab** moved from a hidden right-click menu to a first-class
  tab with a live log viewer (auto-tail, substring filter, copy-to-clipboard
  formatted for Discord, Open log folder button).

---

## Earlier releases

Pre-1.0 versions tracked the Eternity Splinter integration and the meter's
move from the original integrated overlay into the standalone app shell.
See the git log for granular detail.
