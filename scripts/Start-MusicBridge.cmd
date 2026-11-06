@echo off
setlocal
cd /d "%~dp0"
if not exist "MusicBridge\MusicBridge.exe" (
  echo MusicBridge files not found. Extract the WHOLE Windows ZIP first.
  pause
  exit /b 1
)
echo Keep this window open while playing. In game press F9 or F10.
"MusicBridge\MusicBridge.exe"
if errorlevel 1 echo Bridge failed. Run Diagnose-MusicBridge.cmd and read the message above.
pause
