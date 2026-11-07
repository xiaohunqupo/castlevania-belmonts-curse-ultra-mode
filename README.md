# Ultra Mode

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE-MIT)
[![License: Mulan PSL v2](https://img.shields.io/badge/License-Mulan%20PSL%20v2-blue.svg)](LICENSE-MulanPSL2)

English | [简体中文](README.zh-CN.md)

A BepInEx 6 IL2CPP plugin for **Castlevania: Belmont's Curse**. It keeps the original controls and makes them more responsive: faster movement and attacks, invulnerable dodges, a knockback recovery, parrying with normal attacks, whip aiming help, better camera and mirror handling, and relics that work passively once found.

The goal is simple: remove the parts of the controls that feel frustrating, so the game is more fun to play.

> Unofficial community mod. It contains no game files. Back up your saves before using it.

## Features

| Module | What it does |
| --- | --- |
| Fast response | Higher move speed and ground acceleration; faster turn, brake and start transitions; attack wind-up and recovery compressed separately; faster whip throw and retract. Action frame data is rescaled, so hit frames stay in sync with the animation. |
| Camera | The camera keeps up with fast moves and launches; right-stick look range and speed are adjustable. |
| Invulnerable evasion | No damage during the movement phase of the slide and backdash, shown by the speed-boost blink. The recovery afterwards can be hit as in vanilla. Traps, death zones and fall damage still apply. |
| Knockback recovery | While knocked back (flying, rolling on landing, getting up), press Jump to stop at once and regain control. Knockback momentum is cleared and the shield relic's flash plays. |
| Whip assist | Targets are first searched in the vanilla direction (enemies and anchors). If there is nothing that way and only one reachable highlighted anchor nearby, it is hooked automatically. With nothing to hook, the whip button does nothing and doesn't interrupt your action. Anchors are highlighted only when they can actually be reached. |
| Attack parry | Once parry is unlocked, normal attacks parry within the same frame window as the parry move. On success the attack keeps going and only the parry effects play; further enemy hits are ignored for the length of the vanilla parry-success action. |
| Mirrors | Press Down at a mirror to enter at once, no holding. At mirrors you can travel from, the interact prompt adds "Down Interact" with the game's own button icons. Teleport mirrors broken after The Hermit work again and fast travel is no longer locked (save-room mirrors you never used still can't be travelled to, as in vanilla). |
| Passive relics | Relics with auto-equip on are active once found, without a slot (only relics you own; missing ones stay inactive). Relics without and with drawbacks have separate switches; by default only the ones without drawbacks are auto-equipped. Other relics are equipped by hand as in vanilla. |
| Always-on Gloomstone | The Gloomstone's effect is always on, even before you find it: breakable hidden walls show when you are near (on by default, can be turned off). |

Each module is installed on its own. If one fails to install or throws at run time, only that module falls back to vanilla, and the reason is written to `BepInEx/LogOutput.log`; the others keep working.

## Install

Requires the Steam version of the game on Windows x64.

1. Install [BepInEx 6 IL2CPP x64 (be.788)](https://builds.bepinex.dev/projects/bepinex_be) (`BepInEx-Unity.IL2CPP-win-x64`), or the game-specific package [BepinEx for Castlevania](https://www.nexusmods.com/castlevaniabelmontscurse/mods/3). Start the game once and quit; the first start takes a few minutes while BepInEx generates its interop assemblies.
2. Download `UltraMode-v<version>.zip` from [Releases](https://github.com/xiaohunqupo/castlevania-belmonts-curse-ultra-mode/releases) and extract it into the game folder, next to `Castlevania Belmont's Curse.exe`. You should get `BepInEx/plugins/UltraMode/UltraMode.dll`.
3. Start the game. `BepInEx/LogOutput.log` shows `Ultra Mode ... loaded` when it works.

No other mod is required.

### Uninstall

Close the game and delete `BepInEx/plugins/UltraMode/`. You can also delete `BepInEx/config/ultra_mode.belmontscurse.cfg`. The mod changes no game files and passive relic effects are not written to your save, so your save is the same as vanilla afterwards.

## Settings

### In-game settings page

Pause menu → MODS → Ultra Mode. Switch features on and off and adjust the speed multipliers; changes apply at once. The page goes on exactly one MODS tab, picked in this order, so it never shows twice:

1. [Belmont Minimap](https://www.nexusmods.com/castlevaniabelmontscurse/mods/36) is installed: its MODS tab (it reads this mod's settings through the `BelmontModSettings.ProviderV2` contract).
2. [Belmont UI](https://www.nexusmods.com/castlevaniabelmontscurse/mods/89) is installed on its own: its MODS tab.
3. Neither: the copy of Belmont UI embedded in this mod, which also adds the tab to the title screen's Settings.

Texts are in Simplified Chinese when the game is, English otherwise.

### Hotkeys

| Key | Action |
| --- | --- |
| F10 | Turn Ultra Mode on or off (`ToggleKey`) |
| F11 | Dump runtime data (actions, relics, feedbacks...) to `BepInEx/ultra_mode_dump/` for debugging (`DumpKey`) |

### Config file

`BepInEx/config/ultra_mode.belmontscurse.cfg`, created on first start. Every entry is described in the file (in Chinese); the sections are:

| Section | Contents |
| --- | --- |
| `00 General` | Master switch, toggle key |
| `01 Fast Response` | Move speed, start/stop sharpness, turn/brake speed, attack wind-up and recovery, whip speed, mirror travel speed, instant mirror entry and prompt, camera follow and right-stick look |
| `02 Evasion` | Invulnerable evasion and how dodges are detected, knockback recovery and its effect |
| `03 Parry` | Attack parry and the parry action it reuses |
| `04 World` | Repair broken mirrors |
| `05 Charms` | Auto-equip relics without / with drawbacks, the drawback relic list, always-on Gloomstone |
| `06 Whip` | No empty whip, auto-aim, highlight reachable anchors only |
| `99 Debug` | Debug logging, dump key |

### Relics

- Two switches: `PassiveCharms` auto-equips every relic without drawbacks (on by default), `PassiveDrawbackCharms` auto-equips the relics with drawbacks (off by default). Turning a switch off removes the passive effect of its relics within about a second, and they go back to being equipped by hand; relics sitting in a slot are not affected.
- Relics with drawbacks are listed in `DrawbackCharms` (asset names, comma-separated). The default list:

  | Relic | Asset name | Drawback |
  | --- | --- | --- |
  | Blackened Heart | `Charm_GainHPHitMobNoPotion` | Healing potions can't be used |
  | Infernal Medallion | `Charm_ClampMCMana` | Mana capped at 80% |
  | Beelzebub's Seal | `Charm_BoostDamageButReduceLife` | Max HP reduced by 50% |
  | Pale Thorn | `Charm_BoostDamageBackReduceDamageFront` | Damage from behind doubled |
  | Phantom Ring | `Charm_RoomChangeResetMobSpawner` | Enemies respawn as soon as you leave a room |

- Auto-equipped relics can still be put in a slot (display only, effects don't stack); taking them out doesn't remove the passive effect.
- `ForceRevealSecret` (on by default) keeps the Gloomstone (`Charm_RevealSecret`) effect always on, even before you find it. It doesn't depend on the two auto-equip switches.

## Compatibility

- **Charms Unlimited** conflicts with passive relics. Use only one of them (turn off `PassiveCharms` and `PassiveDrawbackCharms`, or don't install Charms Unlimited).
- **Belmont Minimap / Belmont UI** work alongside; the settings page moves to their MODS tab, see above.
- After a game update, a module whose target methods changed falls back to vanilla on its own, and the log says why.

## Troubleshooting

- A feature or the settings page is missing: look for lines starting with `Ultra Mode` or `[ui]` in `BepInEx/LogOutput.log`.
- When reporting a problem, attach `LogOutput.log`, the game version and the mod version. If asked, turn on `DebugLogging` and attach the F11 dump.

## Build

Requires the .NET 6 SDK or newer, and the game with BepInEx installed and started once (the build references the interop assemblies it generates).

Every build packs `dist/UltraMode-v<version>.zip` (laid out as `BepInEx/plugins/UltraMode/`) and copies the plugin into the game folder.

```powershell
dotnet build -c Release                     # build, pack and copy to the game (close the game first)
dotnet build -c Release -p:SkipDeploy=true  # build and pack only
dotnet build -c Release -p:SkipPackage=true # don't pack
dotnet build -c Release -p:GamePath="D:\Games\Castlevania Belmont's Curse"
```

If the game isn't in the default Steam folder, pass `-p:GamePath=...` or set the `BELMONTS_CURSE_DIR` environment variable.

## Source layout

Before contributing, read the [maintainer docs](docs/README.md) (in Chinese: architecture, how each module works, debugging, adding a feature, test checklist, releasing) and the [contributing guide](CONTRIBUTING.md).

```
src/
  Plugin.cs                  Entry point: installs each module's patches and calls its Init
  Feature.cs                 Module registry and fallback on errors
  Settings.cs                Config entries
  Player.cs                  Main character lookup, component queries
  ActionTracker.cs           Tracks the player's current actions (diffed after Actor.UpdateState)
  ActionInfo.cs              Cached action component queries
  Inspector.cs               F11 runtime data dump
  Features/
    FastResponse.cs          Frame data scaler, move speed and acceleration, instant mirror entry
    CameraFollow.cs          Camera follow (Cinemachine damping)
    CameraLook.cs            Right-stick look range and speed (CameraOffset)
    Evasion.cs               Invulnerable evasion (HitStrategy.GetHitResult, DodgeMovementFeature movement phase only)
    EvasionBlink.cs          Blink while evasion is invulnerable
    TechRecovery.cs          Knockback recovery
    WhipAssist.cs            Whip assist (WhipHookDetect.FindEntityTarget probing, anchor highlight)
    AttackParry.cs           Parry window on attacks (ParryFeature.StartParry/StopParry) and the protection after success
    MirrorRepair.cs          Repairs broken mirrors, unlocks fast travel
    MirrorPrompt.cs          "Down Interact" prompt at mirrors (InputTagTextPreprocessor)
    PassiveCharms.cs         Passive relics, always-on Gloomstone
  Menu/
    SettingsRows.cs          Every row of the settings page (single definition shared by all menus)
    SettingsPage.cs          Picks Minimap, standalone Belmont UI or embedded Belmont UI for the page
    MinimapProvider.cs       Belmont Minimap's ProviderV2 contract
    EmbeddedBelmontUi.cs     Starts the embedded Belmont UI (replaces upstream Plugin.cs)
vendor/BelmontUI/            Belmont UI upstream source (MIT), generated by script, don't edit by hand
tools/TypeDump/              Dumps types and members from the interop assemblies
tools/sync-belmontui.ps1     Syncs the Belmont UI upstream source into vendor/BelmontUI
tools/release.ps1            One-step release: version bump, build, tag, GitHub release
docs/                        Maintainer docs
```

### Embedded Belmont UI

`vendor/BelmontUI/` is the source of [leandrobueno/BelmontUI](https://github.com/leandrobueno/BelmontUI). The version and commit are in `vendor/BelmontUI/UPSTREAM` and the license is the `LICENSE` next to it (shipped with the dll as `BelmontUI-LICENSE.txt`). The sync script only makes mechanical edits, so no hand-written code lives in that folder:

- the namespace `BelmontUi` becomes `UltraModeVendor.BelmontUi`, so it can't clash with a standalone Belmont UI;
- `#nullable disable` is added at the top of each file (upstream has no nullable annotations);
- `Plugin.cs` is left out: the embedded copy must not register as a BepInEx plugin, or it would take the standalone version's GUID. It is started from `src/Menu/EmbeddedBelmontUi.cs` instead.

To update upstream:

```powershell
git -C reference/BelmontUI pull          # or delete reference/BelmontUI and the script clones it again
powershell -File tools/sync-belmontui.ps1
```

Then build. If upstream changed how `Plugin.cs` starts the library, or the API used by `SettingsPage.cs` (`Settings.Register`, `SettingsMenu.Toggle/Slider/ResetButton`, `LocalizedText`), update `src/Menu/` by hand.

## License

Copyright (c) 2026 Danny许 ([@xiaohunqupo](https://github.com/xiaohunqupo))

Dual-licensed under your choice of:

- [MIT License](LICENSE-MIT)
- [Mulan Permissive Software License, Version 2 (Mulan PSL v2)](LICENSE-MulanPSL2)

Mulan PSL v2 is a permissive open-source license from China, written in Chinese and English with equal legal effect and approved by the [OSI](https://opensource.org/license/mulanpsl-2-0). Like MIT it allows use, modification and redistribution, and it adds an explicit patent grant. More at [license.coscl.org.cn/MulanPSL2](http://license.coscl.org.cn/MulanPSL2).

SPDX: `MIT OR MulanPSL-2.0`. Unless you state otherwise, contributions are licensed under the same terms.

The embedded Belmont UI keeps its own [MIT license](vendor/BelmontUI/LICENSE).

This is an unofficial fan project, not affiliated with Konami or the game's developers. Castlevania and related names and assets belong to their respective owners.

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx), Il2CppInterop, HarmonyX
- [leandrobueno/BelmontUI](https://github.com/leandrobueno/BelmontUI) (MIT, embedded as the settings page)
- [ollieatkinson/castlevania-belmonts-curse-ultrawide](https://github.com/ollieatkinson/castlevania-belmonts-curse-ultrawide)
- KZekai, for the game-specific BepInEx package
