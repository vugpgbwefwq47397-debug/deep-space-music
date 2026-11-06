# Probably Stolen: Immersive Lyrics / 深空当铺沉浸歌词

Pixel lyrics and desktop music controls for **Probably Stolen**. Windows x64 · **v0.2.2 Preview**.

**[Download v0.2.2 / 下载三平台预览版](https://github.com/vugpgbwefwq47397-debug/deep-space-music/releases/tag/v0.2.2)** · [Windows ZIP](https://github.com/vugpgbwefwq47397-debug/deep-space-music/releases/download/v0.2.2/DeepSpaceMusic-v0.2.2-win-x64.zip) · [SHA-256 checksums](https://github.com/vugpgbwefwq47397-debug/deep-space-music/releases/download/v0.2.2/SHA256SUMS.txt)

**[Steam Workshop / 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3834309955)** installs the game mod. Download the Windows ZIP above for the required external MusicBridge, and keep it outside Mods.

Use the original cassette player to control your desktop music client. Display draggable, synchronized lyrics with 10 continuous pixel effects. The F9/F10 panel uses the game's machinery style; settings apply immediately and save automatically.

## 简介

在《深空当铺》原版磁带机中放入云音乐磁带，即可控制桌面音乐客户端，并显示可拖动的双行像素歌词。支持 10 种持续动效、6 档字号和 6 种颜色，所有设置即时应用。默认浮现 150 ms，歌词提前 100 ms。

## Requirements / 运行要求

- Windows x64 and a legitimate installation of Probably Stolen. Tested with game **1.00-REV6**, mod API **0.46D**, Steam build **26337366**.
- QQ Music, NetEase Cloud Music or KuGou with Windows system media controls enabled. Tested desktop versions: QQ 22.05, NetEase 3.1.42.205594 and KuGou 20.1.61.28014. Start a song before connecting.
- Run the included **MusicBridge** separately. Double-click `Start-MusicBridge.cmd` after extracting the entire ZIP. Its Windows x64 .NET runtime is bundled; installing the .NET SDK is unnecessary for players.

## Install / 安装

1. Exit the game. Extract the download. Copy **`Mods/DeepSpaceMusic`** into the game's **`Mods`** folder.
2. Keep **`MusicBridge` outside the game's Mods folder**, then run `MusicBridge/MusicBridge.exe` and leave it running.
3. In the game's Mods menu, enable Probably Stolen: Immersive Lyrics, review the game's permission prompts yourself, then restart the game.
4. Open your official music client, sign in normally, and play a song. Press **F9 (or F10) → 连接与工具**, then select your platform; it connects immediately.
5. Load your save, use **领取云音乐磁带** in the tools page, and place the cassette in the original cassette player's playback compartment.

只将 `Mods/DeepSpaceMusic` 放入游戏 Mods，不要将桥接、ZIP 或源码放进去。首次连接请手动选平台。双击云音乐磁带后，点击选歌按钮才会调出所选客户端；平时的歌词同步不会抢焦点。


## Workshop users / 工坊用户先看这里

Subscribe to the Workshop mod, then download the **[MusicBridge-only ZIP](https://github.com/vugpgbwefwq47397-debug/deep-space-music/releases/download/v0.2.2/MusicBridge-v0.2.2-win-x64.zip)**. Extract the entire ZIP outside Mods and double-click **Start-MusicBridge.cmd**. Keep its window open until you finish playing. The bridge-only ZIP contains no second game mod, avoiding duplicate installations.

**SMTC 正常不等于本地桥接已经启动。** 订阅工坊后仍需下载、完整解压独立桥接包，双击 `Start-MusicBridge.cmd` 并保持窗口开启。看到“本地管道已就绪”再在游戏中连接。不要只复制 EXE，也不要在 ZIP 内直接运行；桥接不能放进游戏 Mods 或工坊目录。

运行 `Diagnose-MusicBridge.cmd` 可只读检查“本地管道”和“三个平台 SMTC”两层连接，不播放、不切歌、不读取账号凭据。游戏与桥接请使用同一 Windows 账号及相同权限，建议均不要以管理员身份启动。

只有歌名时，在 **F9/F10 → 连接与工具** 查看歌词状态、来源/行数和播放进度：

- **歌词获取失败**：网络请求失败或超时，稍后自动重试，也可点“重新匹配歌词”。
- **暂无匹配歌词**：已返回的来源未找到同一录音的时间歌词，可添加本地 LRC。
- **歌词已匹配，等待可靠播放进度**：已拿到歌词，但客户端未提供可验证的进度；请播放或拖动进度一次。不会猜测进度来伪装同步。
- **等待完整歌曲信息**：客户端尚未提供歌名、歌手或时长，先在客户端实际播放歌曲。

## Controls / 操作

- Original cassette player: play/pause, previous/next, and stop when the chosen client reports support. The game volume knob controls game audio; adjust music volume in your music client.
- F9 by default: open settings or close any of the mod's pages. Rebind in **连接与工具 → 设置快捷键** (F1–F12 and selected Ctrl combinations); changes apply immediately. F10 and Ctrl+F9 remain recovery shortcuts. Tools provide a direct return to subtitle settings.
- Drag visible lyric characters to move subtitles; release to save their position.
- Double-click the cloud cassette, then choose **打开所选平台选歌** to open that client. Queue selection stays in the music client, without rebuilding or reordering your queue.
- Effects, size, color, fade duration, and lyric timing apply immediately. Positive timing values show lyrics earlier; negative values show them later.
- **断开** is remembered across restarts. Use **连接 / 重新检测** or the explicit client picker to enable the connection again. Turning automatic reconnect back on resumes an interrupted enabled connection, while preserving a manual disconnect.
- Rapid playback clicks are limited to one pending operation, with a short debounce for state updates. Stop/eject pauses still take priority. Queued requests from an old platform or a closed picker are cancelled before dispatch; delayed picker completion does not close a newer page.

## Lyrics and network use / 歌词来源与联网

Lyrics are matched from your local LRC files first. NetEase uses its current song ID only after checking it against system-media metadata. Otherwise, the public NetEase catalog, KuGou and LRCLIB are searched concurrently; the first validated timed result is used. A service failure does not block other sources. Title, every artist and recording duration must match; Unicode width, whitespace and collaboration separators may differ. Live/remix/version qualifiers are retained. These are separately matched lyrics. Chinese lyric text is converted to Simplified Chinese for display; timestamps are preserved.

Only the external bridge makes online requests: LRCLIB by title/artist, KuGou's public lyrics service by title/artist/duration, and NetEase's public song search/metadata/lyric endpoints by title/artist or the verified song ID. Requests try a direct connection, then the existing system proxy on failure, within an eight-second per-source budget. Certificate validation remains enabled; no proxy settings are changed. NetEase timing reads local playback events from `cloudmusic.elog`; KuGou timing reads the player's clock through Windows accessibility, without showing or clicking its window. Raw client logs are not saved or uploaded. Game/bridge communication uses a current-user Windows named pipe, with no listening TCP port. The mod does not request passwords, cookies, or account tokens. No music, copyrighted lyric collection, account data, or queue database is included. There is no automatic updater.

Local lyrics folder: `%LOCALAPPDATA%\DeepSpaceMusic\Lyrics`. Add `song.lrc` and `song.json`, then choose **重新匹配歌词** in the tools page:

```json
{"Title":"Exact system media title","Artist":"Exact system media artist","DurationMs":210000}
```

The title and artist must match the system metadata, ignoring case and surrounding whitespace. Duration tolerance is two seconds; `DurationMs: 0` removes the duration constraint for your local file only.

No matching lyrics: the song title is shown. Missing/reliable timeline information cannot be replaced with fabricated synchronization. Effects are decorative character motion, not word-by-word singing timestamps.

## Limits and troubleshooting / 已知限制

- QQ may not expose a usable SMTC session until a song actually plays. Check the client's system-media-control setting if connection fails.
- NetEase: enable **设置 → 系统 → 开启 SMTC**. If the client log was cleared during playback, resume or seek once so it records a new timing anchor.
- KuGou: enable **设置 → 常规设置 → 播放 → 支持系统播放控件**. Keep its standard player available; it may be minimized. Its displayed clock has one-second precision. Unsupported skins/modes that remove the accessible clock cannot provide reliable timing.
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

Validation for 0.2.2: **151 core checks**, **29 isolated IPC checks**, and an official mod scan without refusal/findings. The original downloaded Workshop DLL was tested against a clean extraction of the released bridge: three real pipe calls passed after startup, while the same calls timed out without a bridge. Real public NetEase lyrics for two recordings returned 62 and 65 timed lines. These checks do not prove compatibility with every client version or song. Current Workshop in-game verification is recorded separately; no exhaustive coverage is claimed.

In-save verification also passed: real dual-line pixel lyrics from both NetEase and KuGou, original cassette-player play/pause controls, and switching platforms with the same cassette still inserted.

## Credits, permissions and development disclosure

See [CREDITS.md](CREDITS.md) and [COPYRIGHT.txt](COPYRIGHT.txt). Original project code is published for inspection with copyright reserved; no MIT or other open-source license has been granted. Third-party font and runtime materials retain their own licenses.

The implementation and release text were developed with user-directed **OpenAI Codex** assistance. Runtime code is independently implemented; reference projects' code/assets are not included. No AI-generated music, voices, promotional images, or game artwork is bundled.
