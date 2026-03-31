# Godot CLI Setup

## Install

Install Godot 4.x and ensure the executable is available.

## Configure

Set `GODOT_PATH`:

```powershell
$env:GODOT_PATH = "C:\\Tools\\Godot_v4.3-stable_win64_console.exe"
```

Persist for user:

```powershell
setx GODOT_PATH "C:\\Tools\\Godot_v4.3-stable_win64_console.exe"
```

## Verify

```powershell
& $env:GODOT_PATH --version
```
