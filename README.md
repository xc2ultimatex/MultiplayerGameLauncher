# Multiplayer Launcher

This launcher checks the update feed, installs the latest game build locally into a managed `Game` folder when needed, and launches the game. The detailed source/version settings are not exposed in the UI.

## Configure the launcher

The launcher reads `launcher.settings.json` beside the exe. The update source can be either:

- a local folder on the same PC, such as `C:\MultiplayerPrototypeBuilds\Latest`
- a LAN/SMB share, such as `\\DESKTOP-UABBC1H\MultiplayerPrototypeBuilds\Latest`
- an internet URL, such as `http://203.0.113.25:8080/Latest`

For this project the current default feed is:

```json
{
  "updateSourceDirectory": "http://74.128.161.157:8080/Latest",
  "manifestFileName": "manifest.json",
  "packageDirectoryName": "payload",
  "gameDirectoryName": "Game",
  "gameExecutableRelativePath": "",
  "localVersionFileName": "version.txt"
}
```

Clients on other machines should use either a reachable UNC share path or a reachable `http://` / `https://` URL. The current hardcoded internet feed is `http://74.128.161.157:8080/Latest`.
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
  "packageArchive": "payload.zip",
  "launchExecutable": "My project.exe"
}
```

For local folders and UNC shares, the launcher stages a fresh copy from `payload/`.
For internet sources, host a zip archive such as `payload.zip` and reference it through `packageArchive` in the manifest. The archive should contain the game files at its root.
The launcher compares the remote `version` against the installed `Game/version.txt`. If they differ, it installs the new payload, writes the new version file, and launches the game automatically. If the version is already current, it just launches the installed build.

## Internet hosting

If you want any computer on the internet to update from the build host, do not expose Windows file sharing directly.
Instead:

1. Serve `C:\MultiplayerPrototypeBuilds\Latest` from a web server on the host PC.
2. Port-forward that HTTP port on the router.
3. Set `updateSourceDirectory` to `http://<public-ip>:<port>/Latest`.
4. Make sure `manifest.json` points at a zip payload through `packageArchive`.

For this host, a helper script is included:

```powershell
.\Start-UpdateFeedServer.ps1
```

It serves `C:\MultiplayerPrototypeBuilds` on port `8080`, so clients can fetch `http://74.128.161.157:8080/Latest`.

## Usage

1. Publish a Windows build to the dev host `payload` folder.
2. Update `manifest.json` with the new version string.
3. Publish the launcher for distribution.
4. Give users the published `MultiplayerLauncher.exe`.
5. Users run `MultiplayerLauncher.exe`.

## Publishing a user-facing exe

From the repo root, run:

```powershell
.\Publish-Launcher.ps1
```

This creates a distributable Windows application in `dist\MultiplayerLauncher\`.
The file you hand to users is:

```text
dist\MultiplayerLauncher\MultiplayerLauncher.exe
```

That published `.exe` is the correct user-facing entry point. Users should not run the source checkout or a shortcut that points into `bin\Release\...` on a machine that has never built the project.
The publish script also refreshes a convenience copy at the repo root, `MultiplayerLauncher.exe`, and recreates `MultiplayerLauncher.lnk` so both point at the published single-file build rather than the fragile `bin` app host.
On first run, the launcher creates its support files beside the exe and marks them hidden so the visible item in the folder remains the application.

## Running from a fresh source checkout

For developers working from source on a fresh machine, use `MultiplayerLauncher.cmd` from the repo root instead.

The script:

- creates `bin\Release\net8.0-windows\win-x64` if it does not exist
- builds the launcher automatically when `MultiplayerLauncher.exe` is missing
- starts the built launcher

You can automate step 1 and step 2 with [Publish-LatestBuild.ps1](c:/Users/Corey/Documents/MultiplayerPrototype/BuildTools/Publish-LatestBuild.ps1).
