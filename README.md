# Display Commander Installer (prototype)

Desktop installer for [Display Commander](https://github.com/pmnoxx/display-commander). It helps install update Display Commander, Reshade, and other addons.

## Supported stores

- Steam
- Epic
- More to be added

## Requirements

- Windows 10/11 (x64 for the default build configuration)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build)

The project is configured as an **unpackaged** app with **`WindowsAppSDKSelfContained`**, so the **built output folder** includes the Windows App SDK runtime DLLs. You do **not** need a separate [Windows App Runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads) install just to run the `.exe` from that folder.

## Build

```bash
dotnet restore DisplayCommanderInstaller.sln
dotnet test DisplayCommanderInstaller.sln -c Release
dotnet build src/DisplayCommanderInstaller/DisplayCommanderInstaller.csproj -c Release -p:Platform=x64
```

## Run / ship the `.exe`

After build, run **`DisplayCommanderInstaller.exe`** from the output directory (do not copy only the `.exe`):

`src/DisplayCommanderInstaller/bin/x64/Release/net8.0-windows10.0.26100.0/win-x64/`

For a self-contained **.NET** + WASDK folder to zip, use **publish** (see earlier discussion):
`dotnet publish src/DisplayCommanderInstaller/DisplayCommanderInstaller.csproj -c Release -p:Platform=x64 -p:PublishProfile=Properties/PublishProfiles/win-x64.pubxml`
and ship the entire **`publish`** folder.

You can also use `dotnet run --project src/DisplayCommanderInstaller/DisplayCommanderInstaller.csproj -p:Platform=x64` (Windows only).


## Community

Join the Display Commander Discord: [https://discord.gg/bzXAdDqtyY](https://discord.gg/bzXAdDqtyY)
