@ECHO OFF
ECHO Patching machine.config files.
ECHO Launching PowerShell as an administrator...
PowerShell.exe -NoProfile -Command "& {Start-Process PowerShell.exe -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File ""%~dpn0.ps1""' -Verb RunAs}"
ECHO Finished.
PAUSE