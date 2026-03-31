# Multiplayer Launcher

This launcher checks the update feed, installs the latest game build locally into a managed `Game` folder when needed, and launches the game. The detailed source/version settings are not exposed in the UI.

## Configure the launcher

The launcher reads `launcher.settings.json` beside the exe. For this project the default feed is:

```json
{
  "updateSourceDirectory": "\\\\DESKTOP-UABBC1H\\MultiplayerPrototypeBuilds\\Latest",
  "manifestFileName": "manifest.json",
  "packageDirectoryName": "payload",
  "gameDirectoryName": "Game",
  "gameExecutableRelativePath": "",
  "localVersionFileName": "version.txt"
}
```

Clients on other machines should use the UNC share path `\\DESKTOP-UABBC1H\MultiplayerPrototypeBuilds\Latest`.
Leave `gameExecutableRelativePath` blank to auto-detect the single non-crash-handler `.exe` in the local `Game` folder, or set it explicitly if your payload contains multiple launchable executables.

## Dev host directory layout

The configured source directory should look like this:

```text
Latest/
  manifest.json
  payload/
    My project.exe
    My project_Data/
    UnityPlayer.dll
    version.txt
```

Example `manifest.json`:

```json
{
  "version": "0.1.3",
  "packageDirectory": "payload",
  "launchExecutable": "My project.exe"
}
```

The launcher compares the remote `version` against the installed `Game/version.txt`. If they differ, it stages a fresh copy from `payload/`, swaps the local `Game` directory, writes the new version file, and launches the game automatically. If the version is already current, it just launches the installed build.

## Usage

1. Publish a Windows build to the dev host `payload` folder.
2. Update `manifest.json` with the new version string.
3. Give users the launcher build output.
4. Users run `MultiplayerLauncher.exe`.

You can automate step 1 and step 2 with [Publish-LatestBuild.ps1](c:/Users/Corey/Documents/MultiplayerPrototype/BuildTools/Publish-LatestBuild.ps1).
