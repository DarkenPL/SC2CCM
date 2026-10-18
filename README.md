# SC2CCM for macOS

macOS port of the **StarCraft II Custom Campaign Manager** (SC2CCM 1.03).
The original Windows version lives at [7thAce/SC2CCM](https://github.com/7thAce/SC2CCM).
Drop a campaign `.zip` (or folder) onto the window, pick it from the
dropdown and hit **Set Active**. "Restore Default" brings back the Blizzard campaign.

Built with C# / .NET 8 and [Avalonia](https://avaloniaui.net/) instead of WinForms.

## Download / build

**Without building:** the GitHub Actions workflow (`.github/workflows/macos.yml`) builds the app (Apple Silicon) on every push.
Open *Actions → macOS build → latest run → Artifacts → SC2CCM-macOS*.

**Build yourself** (needs the .NET 8 SDK: `brew install --cask dotnet-sdk`):

```bash
./build-mac.sh          # → dist/SC2CCM.app (Apple Silicon)
```

Move `SC2CCM.app` to `/Applications`. The app is not notarized, so on first launch right-click it → **Open** → **Open**
(or run `xattr -dr com.apple.quarantine /Applications/SC2CCM.app`).

Run the logic tests: `dotnet run --project tests/SC2CCM.Core.Tests`

## Where things go

| | Path |
|---|---|
| StarCraft II (auto-detected) | `/Applications/StarCraft II` – otherwise the app asks you to pick the folder |
| Installed custom campaigns | `StarCraft II/Maps/CustomCampaigns` |
| Active campaign files | `StarCraft II/Maps/Campaign` (`swarm`, `swarm/evolution`, `void`, `voidprologue`, `nova`) |
| Dependencies (`*.SC2Mod`) | `StarCraft II/Mods` |
| App settings | `~/Library/Application Support/SC2CCM/sc2path.txt` |

## Project layout

```
  src/SC2CCM.Core/   all mod-management logic, no UI, no NuGet dependencies
  src/SC2CCM.Mac/    Avalonia app (MainWindow, CampaignPanel, MessageDialog)
  tests/             console smoke tests for the core logic
  build-mac.sh       publish + assemble .app + ad-hoc sign + zip
```

## Plans

- [DMG installer and self-updating app](docs/plan-dmg-i-aktualizacje.md) (planned, Polish)
- [Tracking updates of installed mods](docs/pomysl-aktualizacje-modow.md) (idea, Polish)

## License

This is an unofficial port and not the official CCM distribution.

Copyright (c) 2022 by 7thAce and GiantGrantGames

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction, including without limitation the
rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit
persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the
Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
