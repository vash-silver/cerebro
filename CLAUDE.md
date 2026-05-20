# Cerebro — notes for Claude

Project-scoped memory for working on this repo. Read at the start of every
session.

## Release workflow (mandatory)

Before publishing **any** new release (v2.X), in this order:

1. **Update `CHANGELOG.md`** — promote the `[Unreleased]` section to a new
   `[2.X] — YYYY-MM-DD` block. Group entries under `Added` / `Changed` /
   `Fixed` / `Removed`. Capture every user-visible behavior change since the
   prior tag — fish git log if in doubt. Always re-open `[Unreleased]` as
   an empty section under the latest version so future commits have a place
   to land.
2. **Update `README.md`** — sweep the feature sections for anything new
   shipped since the last tag. The current canonical sections that need to
   stay in sync are: top intro, *Main app + three overlays*, *Live DPS*,
   *Live dashboard*, *Buff Tracker*, *Cooldown Tracker*, *Cosmic Loot
   Scanner*, *Eternity Splinter tracker*, *In-app updater*, *Settings tab*,
   *Persistence* (table of files in `%LocalAppData%\MarvelHeroesComporator\`).
   Add a new section if a major feature lands.
3. **Bump `<Version>` in `MarvelHeroes.DpsMeter/MarvelHeroes.DpsMeter.csproj`**
   to the target tag (e.g. `2.13.0`). publish.ps1 normalizes 2-component
   tags to 3 for AssemblyVersion compatibility.
4. **Build the release** — `powershell.exe -NoLogo -NoProfile -ExecutionPolicy
   Bypass -File scripts/publish.ps1 -Version 2.X`. The script runs the
   PII scan; verify it ends with `[ok] No personal-info patterns found.`
5. **Commit** with a multi-paragraph message that mirrors the changelog's
   per-version block. Stage files explicitly (no `git add -A`) to avoid
   accidentally including transient WPF temp files or unrelated cruft.
6. **Tag + push**: `git tag vX.Y -m "Cerebro vX.Y — <subject>"` then
   `git push origin main && git push origin vX.Y`.
7. **Create the GitHub release** via the REST API (credentials live in the
   git credential manager for `https://github.com`). Use a `.release-notes-vX.Y.json`
   tempfile for the body to avoid JSON-escaping hell. After the release
   is created, POST the zip to the release's `upload_url` (replace `{?name,label}`
   with `?name=Cerebro-vX.Y.zip`). Delete the tempfile after.
8. **Smoke-test** — launch the published `publish/Cerebro/Cerebro.exe`
   and confirm Settings → About reads `vX.Y.0`. If the user is running an
   older version, they should see the in-app updater banner within a few
   seconds; v2.10+ users can click "Update now" to swap in place.

The repo is `vash-silver/cerebro` on GitHub. Default branch is `main`.

## Architecture cheat-sheet

- `MarvelHeroes.DpsMeter/` — WPF app. Main window + three independent
  overlay windows (DPS, Buff, Cooldown), presenter, hero/boss tables,
  trackers (Buff, Cooldown, Splinter, Loot scanner).
- `NetworkSniffer/` — PCAP capture, TCP reassembly, mux demux, protobuf
  parsing. Hosts the `MhMissionSniffer` event surface. `DpsOverlaySettingsFile`
  lives here even though it's used app-wide (legacy from comporator extract).
- `Gazillion/` — Marvel Heroes wire-format protobuf classes sourced from
  MHServerEmu. Do not hand-edit; they're generated.
- `lib/` — Vendored Google.ProtocolBuffers.dll (proto2-era C# port).
- `scripts/` — `publish.ps1` (release builder), `generate_appicon.ps1`.

Per-user runtime state lives at `%LocalAppData%\MarvelHeroesComporator\`
(folder name kept stable from the comporator era for cross-app continuity).
See README's *Persistence* table for the file list.

## XAML gotchas

- **`--` is illegal in XAML XML comments** (it's the XML standard's "double
  hyphen ends the comment" rule). The build error is MC3000. Use single
  hyphens or rephrase. This bites recurrently when writing/refreshing
  comment blocks; always check before claiming a clean build.

## CHANGELOG conventions

The in-app changelog viewer reads `CHANGELOG.md` from the repo root,
bundled as a WPF resource (`<Resource Include="..\CHANGELOG.md"
Link="CHANGELOG.md" />` in the csproj). It's rendered as plain text in a
scrollable monospace TextBox — no Markdown rendering — so write the file
to be readable raw. Follow Keep a Changelog conventions.
