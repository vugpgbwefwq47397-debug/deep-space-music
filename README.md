# Deep Space Music Terminal / 深空音乐终端

Pixel lyrics and desktop music controls for **Probably Stolen**. Windows x64 · **v0.1.0 preview**.

**[Download v0.1.0 / 下载预览版](https://github.com/vugpgbwefwq47397-debug/deep-space-music/releases/tag/v0.1.0)** · [Windows ZIP](https://github.com/vugpgbwefwq47397-debug/deep-space-music/releases/download/v0.1.0/DeepSpaceMusic-v0.1.0-win-x64.zip) · [SHA-256 checksums](https://github.com/vugpgbwefwq47397-debug/deep-space-music/releases/download/v0.1.0/SHA256SUMS.txt)

Use the original cassette player to control your desktop music client. Display draggable, synchronized lyrics with 10 continuous pixel effects. The F10 panel uses the game's machinery style; settings apply immediately and save automatically.

## 简介

在《深空当铺》原版磁带机中放入云音乐磁带，即可控制桌面音乐客户端，并显示可拖动的双行像素歌词。支持 10 种持续动效、6 档字号和 6 种颜色，所有设置即时应用。默认浮现 150 ms，歌词提前 100 ms。

## Requirements / 运行要求

- Windows x64 and a legitimate installation of Probably Stolen. Tested with game **1.00-REV6**, mod API **0.46D**, Steam build **26337366**.
- QQ Music with Windows system media controls (SMTC) enabled. QQ Music 22.05 was tested. NetEase session detection is implemented but full in-game playback compatibility has not been verified.
- Run the included **MusicBridge** separately. Its Windows x64 .NET runtime is bundled; installing the .NET SDK is unnecessary for players.

## Install / 安装

1. Exit the game. Extract the download. Copy **`Mods/DeepSpaceMusic`** into the game's **`Mods`** folder.
2. Keep **`MusicBridge` outside the game's Mods folder**, then run `MusicBridge/MusicBridge.exe` and leave it running.
3. In the game's Mods menu, enable Deep Space Music Terminal, review the game's permission prompts yourself, then restart the game.
4. Open your official music client, sign in normally, and play a song. Press **F10 → 连接与工具**, then select your platform; it connects immediately.
5. Load your save, use **领取云音乐磁带** in the tools page, and place the cassette in the original cassette player's playback compartment.

只将 `Mods/DeepSpaceMusic` 放入游戏 Mods，不要将桥接、ZIP 或源码放进去。首次连接请手动选平台。双击云音乐磁带后，点击选歌按钮才会调出 QQ；平时的歌词同步不会抢焦点。

## Controls / 操作

- Original cassette player: play/pause, previous/next, and stop. The game volume knob controls game audio; adjust QQ volume in QQ.
- F10: open settings or close any of the mod's pages. Tools provide a direct return to subtitle settings.
- Drag visible lyric characters to move subtitles; release to save their position.
- Double-click the cloud cassette, then choose **打开 QQ 音乐选歌** to open the client. Queue selection stays in QQ.
- Effects, size, color, fade duration, and lyric timing apply immediately. Positive timing values show lyrics earlier; negative values show them later.

## Lyrics and network use / 歌词来源与联网

Lyrics are matched from your local LRC files first; otherwise MusicBridge queries **LRCLIB over HTTPS**, using song title and artist and checking recording duration. These are separately matched lyrics, not a direct copy of the QQ lyric panel. Chinese lyric text is converted to Simplified Chinese for display; timestamps are preserved.

Only the external bridge contacts LRCLIB. Game/bridge communication uses a current-user Windows named pipe, with no listening TCP port. The mod does not request passwords, cookies, or tokens. No music, copyrighted lyric collection, account data, or queue database is included in this download. There is no automatic updater.

Local lyrics folder: `%LOCALAPPDATA%\DeepSpaceMusic\Lyrics`. Add `song.lrc` and `song.json`, then choose **重新匹配歌词** in the tools page:

```json
{"Title":"Exact system media title","Artist":"Exact system media artist","DurationMs":210000}
```

The title and artist must match the system metadata, ignoring case and surrounding whitespace. Duration tolerance is two seconds; `DurationMs: 0` removes the duration constraint for your local file only.

No matching lyrics: the song title is shown. Missing/reliable timeline information cannot be replaced with fabricated synchronization. Effects are decorative character motion, not word-by-word singing timestamps.

## Limits and troubleshooting / 已知限制

- QQ may not expose a usable SMTC session until a song actually plays. Check the client's system-media-control setting if connection fails.
- Exact matching cannot find every song, remix, or alternate recording. Supply a matching local LRC when necessary.
- The first entry in a mixed original/cloud cassette compartment follows the game's original selection rules. Keep the cloud cassette first when using external music.
- The cloud cassette must remain in the playback compartment for the overlay to bind.
- The bundled bitmap font uses the game's available UI shader; the legacy glow setting does not add bitmap glow.
- Preview release: automated tests and focused in-game checks passed; every save-state, client version, and gameplay scenario has not been exhaustively tested.

Game log: `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log`.
Bridge configuration and local LRC files: `%LOCALAPPDATA%\DeepSpaceMusic`.

## Uninstall / 卸载

Remove your cloud cassettes from the save first, follow the game's normal save procedure, then exit. Disable the mod, remove `Mods/DeepSpaceMusic`, and stop/remove MusicBridge. Your local LRC files and configuration are kept unless you remove them yourself.

## Source and development / 源码与开发

Install .NET 8 SDK and build against your own game installation:

```powershell
./scripts/build.ps1 -GameDir 'C:\Path\To\Probably Stolen'
dotnet run --project tests/CoreTests.csproj -c Release
```

The game assemblies are build references only and are not distributed. Prebuilt font atlas/metrics and their licenses are included. Historical queue helper classes remain for regression coverage; current runtime entry points reject queue/point-play requests and no QueueReader executable or QQ native library is shipped.

Validation: **78 core checks**, **17 isolated IPC checks**, and the game's official mod scanner (**143 methods**, no findings/refusal). In-game checks covered settings retention, centered controls, single-page navigation, F10 closing, and live disconnect/reconnect status.

## Credits, permissions and development disclosure

See [CREDITS.md](CREDITS.md) and [COPYRIGHT.txt](COPYRIGHT.txt). Original project code is published for inspection with copyright reserved; no MIT or other open-source license has been granted. Third-party font and runtime materials retain their own licenses.

The implementation and release text were developed with user-directed **OpenAI Codex** assistance. Runtime code is independently implemented; reference projects' code/assets are not included. No AI-generated music, voices, promotional images, or game artwork is bundled.
