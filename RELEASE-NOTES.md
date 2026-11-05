# v0.1.0 · Preview / 预览版

First public preview of Deep Space Music Terminal for Probably Stolen, Windows x64.

- Original cassette-player controls for desktop music through Windows SMTC.
- Draggable dual-line pixel lyrics, 10 continuous effects, 6 size presets and 6 colors.
- Game-style F10 settings with centered values, instant application and automatic saving.
- Default reveal: 150 ms; default lyric timing: 100 ms early.
- Explicit QQ song-picker entry; regular polling never opens QQ.
- Local LRC priority, background LRCLIB matching and Simplified Chinese lyric display.
- Connection/timing persistence, cancellation and stale-response handling; clearer missing-lyrics/error feedback.

Validation: 78 core checks, 17 isolated IPC checks, official scanner 143 methods with no findings, plus focused in-game UI/connection verification. Preview status is intentional; compatibility with every client/game/save scenario is not claimed.

**Installation:** extract the Windows ZIP, copy only `Mods/DeepSpaceMusic` into the game, keep MusicBridge outside Mods, and run MusicBridge separately. See README for activation and cassette placement.

**Online use:** MusicBridge queries LRCLIB by song title/artist. No credentials, queue databases, copyrighted lyric collection, or music files are bundled. Full credits and third-party licenses are included.

Original code: copyright reserved. Source is available for inspection. Third-party licenses are unchanged.
