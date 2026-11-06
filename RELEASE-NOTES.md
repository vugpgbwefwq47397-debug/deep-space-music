# v0.2.2 · Lyrics coverage and bridge diagnostics / 用户反馈修复

- Add validated NetEase song-ID lyrics and public catalog fallback for other clients. Query independent lyric sources concurrently so a failed source does not hide a valid result.
- Normalize Unicode width/spacing and collaboration separators while retaining artist membership, recording duration and live/remix/version checks. Never replace lyrics with an arbitrary same-name song.
- Show lyric source/line count, network failures, missing metadata and missing reliable timing separately. Refresh local lyric overrides without restarting the bridge.
- Default settings shortcut is now F9, configurable immediately in Tools or the official mod configuration page. Legacy F10 and Ctrl+F9 remain available. Ignore shortcuts while editing a text field.
- Explain the two connection layers: Workshop + SMTC does not launch the external bridge. Add Start-MusicBridge.cmd, read-only Diagnose-MusicBridge.cmd, explicit ready/startup failure messages, and Windows account/privilege guidance.
- Add a bridge-only ZIP for Workshop users; do not place its runtime in Mods. Update both the Workshop mod and external bridge for all fixes.
- Requests use validated TLS, bounded responses/timeouts, direct access with fallback to the existing system proxy. No proxy/security setting is changed. No credentials/cookies, songs, queues or raw logs are uploaded.
- Automated validation: 151 core checks, 29 isolated IPC checks, official scan without refusal/findings. Clean old-release pipe roundtrip with the original downloaded Workshop DLL passed after startup. Real NetEase IDs returned 62/65 timed lines; cross-platform lookup also returned timed lyrics. Unknown user song/client examples cannot be declared reproduced or universally fixed.
- A reported recording was reproduced: C!naH's "Sweet Dream (비비노스 - 에이스테 Original Soundtrack Extended ver.)" returned no match in the old released bridge and 49 timed lines in the candidate. The reported "ANEMONE" recording remains unidentified; one candidate was checked without assuming it is the user's version.
- Fixed a bridge-restart recovery case where the bridge restored a different platform from the game's selection. Automatic recovery now reconciles that selection while respecting manual disconnect and disabled auto-reconnect.
- Follow-up fixes: reconcile a same-platform bridge that restores a disabled connection; ignore out-of-order NetEase load events; reject entirely blank timed lyrics; continue past an empty local LRC to another valid match. Nine regression checks added; the same-platform recovery also passed in the running game.
- Another reported recording, "アンダーグラウンド" by 柿崎ユウタ, returned no timed match in the old release; the candidate retrieved 46 timed lines from NetEase and 44 through the KuGou fallback. This verifies lookup, not playback of that song in a live NetEase client.

# v0.2.1 · Probably Stolen: Immersive Lyrics / 深空当铺沉浸歌词

- New display name: Probably Stolen: Immersive Lyrics / 深空当铺沉浸歌词. Existing mod IDs, save bindings and configuration paths remain compatible.
- Remember manual Disconnect across game/bridge restarts and setting changes.
- Turning automatic reconnect back on resumes interrupted connections without overriding manual Disconnect.
- Deduplicate pending playback clicks and add a short debounce for media state updates. Stop/eject pauses retain priority.
- Cancel queued requests after a platform change or picker navigation. A late open-client reply cannot close a newly opened page or unlock another request.
- Reconnect after a bridge outage only when the user's connection is enabled. Polling never opens a music window.
- Ignore late disconnect requests for another platform and repeated selection of the same platform.
- Validation: 114 core checks, 29 isolated IPC checks, official scan of 167 methods without findings. Installed 0.2.1 loaded successfully and manual disconnect persistence was checked in game. Full in-game coverage of the new operation fixes remains incomplete; this is a preview release.
- Supports QQ, NetEase and KuGou. Upgrading from 0.1.0: exit the game, replace both Mods/DeepSpaceMusic and the external MusicBridge, restart the bridge, then start the game. Keep MusicBridge outside Mods; your local settings and lyrics are retained.

# v0.2.0 · Platform adapter test build / 三平台适配测试版

- NetEase 3.1.42: real local playback events supply the missing SMTC timeline; public song ID metadata verifies the recording and duration.
- KuGou 20.1.61: background player-clock reading supplies timing, with public KuGou lyric matching and LRCLIB fallback.
- All picker buttons follow the selected platform; no forced return to QQ.
- KuGou launcher/versioned processes resolve as one installation. Connection selection survives restart.
- Regular polling never shows a music window; queues are not rebuilt or reordered.
- Validation: 99 core checks, 23 isolated IPC checks, official scanner 151 methods without findings, plus minimized live playback/pause/lyrics and foreground-preservation checks on both new clients.
- In-save verification: both clients render real changing dual-line lyrics; the original cassette player controls play/pause; the inserted cloud cassette keeps working after a platform switch.

These adapters are included in the 0.2.1 public preview. The historical v0.1.0 release remains available unchanged.

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
