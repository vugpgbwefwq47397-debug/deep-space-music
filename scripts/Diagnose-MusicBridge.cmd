@echo off
setlocal
cd /d "%~dp0"
if not exist "MusicBridge\MusicBridge.exe" (
  echo MusicBridge files not found. Extract the WHOLE Windows ZIP first.
  pause
  exit /b 1
)
"MusicBridge\MusicBridge.exe" --diagnose
pause
