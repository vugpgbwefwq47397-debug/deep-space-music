# Third-party materials

## Font

- **Fusion Pixel Font**, TakWolf: https://github.com/TakWolf/fusion-pixel-font — SIL Open Font License 1.1.
- Included upstream notices: Ark Pixel Font (TakWolf), Cubic 11 (ACh-K, https://github.com/ACh-K/Cubic-11), and Galmuri (Lee Minseo / quiple).
- The supplied 12px proportional zh_hans glyphs were converted into a point-sampled PNG atlas and binary metrics without redrawing glyphs. The original font notices are kept under the mod's `Fonts/OFL.txt` and `Fonts/LICENSES/` (source: `src/GameMod/Fonts/`).

## Runtime and game references

- Microsoft .NET 8 / Windows SDK / CsWinRT. The self-contained Windows bridge includes Microsoft's runtime LICENSE and ThirdPartyNotices files.
- Unity, TextMeshPro, Harmony, the official mod API, and the game's UI sprites are supplied by the user's installed copy of Probably Stolen. No game assemblies or extracted game artwork are shipped.
- Official mod documentation: https://github.com/dragonpan2/probably-stolen-modding — consulted for interfaces; its example code is not included.

## Lyrics

Local LRC files belong to their respective rights holders. Online lyrics are separately matched from LRCLIB (https://lrclib.net/), retained in memory and excluded from this release. The release contains no music or lyric collection.

## Design references

DangoMusic (Sorihiko, https://github.com/Sorihiko/DangoMusic) and MusicShow (ShuangJinl, https://github.com/ShuangJinl/MusicShow) informed the pixel/floating lyric interaction. Motion formulas and Unity implementation are independently written; no source code or artwork from those projects is bundled.

## Project code

Original code and release text were implemented with user-directed OpenAI Codex assistance. Original project rights are reserved as stated in COPYRIGHT.txt. Third-party licenses remain applicable to their own materials.

## Playback adapters added in 0.2

NetEase elog byte decoding references the MIT-licensed dsh-pet-qqmusic project (https://github.com/344272586-sketch/dsh-pet-qqmusic). Its notice is included under `THIRD-PARTY-LICENSES/dsh-pet-qqmusic.txt`. Only playback events are retained in memory; raw logs are not bundled or uploaded.

KuGou timing uses read-only Windows UI Automation. KuGou lyrics use its public lyric search/download service, matching title, artist and recording duration. Neither client binaries, login data nor fetched lyrics are included.
