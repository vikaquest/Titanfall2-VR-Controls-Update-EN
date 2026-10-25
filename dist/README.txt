# Titanfall 2 VR — Controls Update v1.5.0

Unofficial patcher for CircuitLord’s Titanfall 2 VR mod. Adds control, dash, reticle and sound improvements. Configure individual features in the game; the patcher shows a feature catalog on the left and detailed instructions on the right.

## Public project files

This repository publishes the patcher interface, installation logic, text resources and screenshot. The private `payload.json` containing prepared game files has been removed from branches and tags, including their history. Automatic builds and publication are disabled. Release 1.5.0 has been withdrawn.

This is **not a complete standalone build kit**: interface source is available, but a working EXE requires the private payload. Existing local developer builds are unaffected.

## Install

1. Install Titanfall 2, then VR mod **1.0.17** through [CircuitLordVRModInstaller](https://github.com/CircuitLord/CircuitLordVRModInstaller).
2. Close the game and run `Titanfall2VR-QuestPatch.exe`.
3. Select the installed game folder and click **Apply changes**. The complete update is installed together; there are no installation checkboxes.
4. Launch VR through CircuitLord as usual, then open **VR Settings** in the game to configure features.

Windows 10/11 x64, .NET Framework 4.8, Quest / Touch controllers. Running the EXE does not require Python or a separate patcher installation. Supports the verified **VR mod 1.0.17** build; unrecognized DLLs and incompatible game dependencies block installation. Existing saved preferences are preserved.

## Application

![Titanfall 2 VR — Controls Update 1.5.0](docs/screenshot.png)

## Features and controls

| Feature | Controls |
| --- | --- |
| Cycle Titan loadouts in combat | **Right Grip + B**, while piloting a Titan. Left Grip + B keeps priority for ability 3. |
| All eight campaign loadouts | VR Settings → Titan → Available loadouts: unlocked only or all eight. Menu locks and story progress remain unchanged. |
| Titan abilities without arrows | **Left Grip + left Trigger** — 1, **+ A** — 2, **+ B** — 3, **+ R3** — core. Visual arrows can be restored in settings. |
| Melee | **R3**, or **left Grip + right Trigger** in a Titan. |
| Cabin steering | Extended or original steering; weapon/head mix in 5% steps, speed, inertia and a toggle for stick turning. |
| Ground slide and air dash | **Left Grip + left Trigger**. Ground and air can be enabled separately. Cooldown 0.2 s; default forced slide hold 0.2 s. |
| Directional stick dashes | Enable right-stick dashes and return the stick to center before repeating. Original stick controls are the default. |
| Swap holsters | **B** with empty hands swaps the main and side holsters without drawing a weapon. |
| Random back-holster weapon | **Right Trigger + B** with empty hands. Availability depends on level resources. |
| Auto-swap an empty weapon | Enable auto-swap, fire the last round, then holster the weapon. Off by default. |
| Magazines and reloading | **Left Trigger**; release left Grip to grab a magazine. Pilot ability is on **L3**. |
| Two-hand support | Doubled acquisition area; support remains latched until left Grip is released. Reload reach is also doubled. |
| Chest grenade | **Right Trigger**. Original Grip-based grabbing can be restored in settings. |
| Weapon reticle | Filled dot, ring, cross or three lines; size 50–200%, or disabled. Head reticle hidden; other HUD indicators preserved. |
| Sound feedback | Empty-magazine click, last-round notification and air-dash jet sound. Toggles and volume 0–300% in 10% steps; sample selection for weapon notifications. |

### In-game settings

Six tabs: **Basic VR → Titan → Weapons → Dash → Sounds → Reticle**. Ground dash, air dash, Grip + Trigger activation and stick activation have independent settings. Auto-sprint is forced on for this control scheme.

The complete update also includes unlimited reserve ammunition for **manual** reloading and a recharge of normal grenades to three 20 seconds after the last throw, including partially spent supplies. C4, mines, remotely detonated devices and the charge-based sonar grenade are excluded. There is no separate reserve-ammo toggle in the menu.

Random weapons and Titan loadouts depend on assets available on the current map. The full weapon pool is not guaranteed on every level. Magazine capacity and fire rate are unchanged.

## Restore

**Restore (Remove patcher files)** returns files to their state **before the first 1.5.0 installation**: replaced files are restored and files added by this installation are removed. Improvements installed before 1.5.0 may remain because they belong to that original state. Game saves and profile settings are not deleted.

Backups are stored in `.quest-controls-update-1.5.0` inside the game folder. Earlier backups are retained. Do not apply an older patcher over 1.5.0; restore with the current version first.

## Source and validation

Window and installation logic source: `QuestPatch.cs`; public text resources: `features.json`, `messages.json`. Building requires the **unpublished** `payload.json`. Once supplied locally, run `powershell -ExecutionPolicy Bypass -File ./build.ps1`. Without it, the build stops with an explanatory message.

The private `payload.json` contains prepared game files, not the original VR mod’s C++ source; it is not part of the public repository. CircuitLord creates the original VR mod; this project supplies unofficial modifications and a patcher.

Installation, reapplication, restoration and interrupted-transaction recovery were checked on file fixtures, along with embedded-file integrity and both interface languages. See [VERIFICATION.md](VERIFICATION.md). Code checks do not replace testing your installation in a headset.

[**♥ Support the project on Ko-fi**](https://ko-fi.com/vikaquest)
