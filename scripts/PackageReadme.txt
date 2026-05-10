Marvel Heroes — Standalone DPS Meter
====================================

A real-time DPS overlay for Marvel Heroes Omega (Tahiti / MHServerEmu
community servers).  Runs as its own process — no patching, no DLL
injection — by passively sniffing the client's TCP traffic.


Requirements
------------

  1.  Windows 10 (build 19041 / version 2004) or newer, 64-bit.
  2.  Npcap installed in "WinPcap API-compatible Mode".
        Download: https://npcap.com/#download
        During installation, leave the default checkboxes alone — the
        "WinPcap API-compatible Mode" option is what this app uses.
  3.  Marvel Heroes Omega running on the Tahiti community server
        (default port 4306).  No game-side changes required.

No .NET runtime install needed — the program is fully self-contained.


How to run
----------

  1.  Install Npcap (link above) if you haven't already.
  2.  Start Marvel Heroes and log into the game world.
  3.  Double-click MarvelHeroes.DpsMeter.exe.
  4.  A small overlay appears in the top-left corner of your screen.
        - Left-drag to reposition.
        - Right-click for the full settings menu (boss-only toggle,
          window-vs-overlay mode, scale, save snapshot, view reports,
          exit).
        - The ✕ in the top-right of the overlay closes the app.

The overlay auto-hides when Marvel Heroes is not the foreground window,
so it won't get in the way when you alt-tab.


Where the app stores its data
-----------------------------

All runtime state lives under your own user folder — nothing is written
inside the program directory, so you can move the EXE freely:

  %LocalAppData%\MarvelHeroesComporator\
    dps-overlay.json       window position, scale, mode preferences
    reports\dps-*.json     saved fight snapshots
    personal_bests.json    per-hero high-score records
    dps-max-hits.json      per-power max-hit history
    dps-meter.log          diagnostics (only when logging is enabled)


Antivirus note
--------------

Self-contained .NET single-file executables are sometimes flagged as
suspicious by Windows Defender / SmartScreen on first launch because
the EXE is unsigned.  The source code is open and you can compile it
yourself if you'd prefer not to trust a binary from a friend — ask
the sender for the repo URL.


Troubleshooting
---------------

  • "Network sniffer failed to start" on launch — Npcap is missing
    or installed in a mode this app can't use.  Reinstall Npcap and
    make sure "WinPcap API-compatible Mode" stays checked.

  • Overlay says "waiting for boss…" forever — boss-only mode is on
    and you're not currently fighting a [Boss] / [GroupBoss] target.
    Right-click → uncheck "Boss DPS only" to see all damage.

  • Overlay doesn't appear — it may be hidden because Marvel Heroes
    isn't detected as the foreground process.  Bring the game forward
    (or right-click the system tray) and the overlay reappears.

  • Multiple network adapters / VPN — the app will auto-pick the
    adapter carrying game traffic.  If it picks the wrong one, edit
    %LocalAppData%\MarvelHeroesComporator\dps-overlay.json and set
    "NpcapAdapterFilter" to a substring of the correct adapter name.


Have fun!
