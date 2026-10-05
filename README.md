# Dismantleheim

Satisfactory-inspired **queued selection** for Valheim with two modes:

- **Mass Dismantle** — reclaim construction materials like the normal Hammer (vanilla refunds).
- **Mass Delete** — erase selected objects with **no** material or harvest drops. Deleting containers can destroy their contents.

Nothing is removed on hover, select, tool switch, or mode switch. Hold Mouse3 to confirm.

| | |
| --- | --- |
| Version | 0.1.0 (beta) |
| GUID | `com.sonicdm.valheim.dismantleheim` |
| Dependencies | BepInExPack Valheim, [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) |
| Soft dependency | [Infinity Hammer](https://thunderstore.io/c/valheim/p/JereKuusela/Infinity_Hammer/) (Tools menu entries) |

## How to get the tools

### With Infinity Hammer (recommended)

1. Install Infinity Hammer (+ Server Devcommands / World Edit Commands as required by IH).
2. Run **`hammer_menu`** to spawn the Infinity Hammer.
3. Equip it → build menu → category **hammer** → **Dismantleheim - Mass Dismantle** / **Dismantleheim - Mass Delete** (Hammer icons; IH loads `BepInEx/config/tools/infinity_tools*.yaml`).
4. Or press **Delete** (`ActivateKey`) while in hammer placement mode.

First launch after install writes the tools file; **restart once** if the entries are missing from the menu.

### Without Infinity Hammer

Equip a hammer → enter build/placement mode → press **Delete**, or console: `dismantleheim mode dismantle|delete`. No extra hammer item.

## Controls (while active)

| Input | Action |
| --- | --- |
| Click / hold LMB | Add piece under aim (does not unselect) |
| Hold Ctrl + wave aim | Mass-select valid pieces under the cursor (add-only; no box) |
| Ctrl + click | Unselect one hovered piece |
| Shift + Mouse3 | Sample prefab filter (vanilla pipette suppressed) |
| Hold Mouse3 | Confirmation ring; release early cancels |
| Right click | Normal build menu |
| Escape | Cancel hold or clear queue |
| ActivateKey again | Deactivate |

HUD shows `MASS DISMANTLE — Returns Materials` or `MASS DELETE — No Drops`, plus `N selected`. Mass Delete warns when queued containers hold items.

Unfiltered selection: removable **Pieces** only. Mass Delete may include rocks/trees/props after an explicit prefab sample. Mass Dismantle never queues environment.

## Dry-run

`Removal.DryRunOnly = false` by default (live dismantle/delete). Set `true` to log the plan only with no world deletes — useful for testing. Read `docs/authority.md` before enabling live remove on a valued world.

## Authority

- **Mass Dismantle (live):** ward access → `CheckCanRemovePiece` → `Piece.CanBeRemoved` → `WearNTear.Remove` → `Piece.DropResources` (vanilla refund path).
- **Mass Delete (live):** ward access → `WearNTear.Remove` **without** `DropResources`. Occupied chests allowed after warning.
- Environment on unmodded dedicated servers is not promised. Details: [docs/authority.md](docs/authority.md).

## Config

- `[Modes]` DefaultMode, ShowActiveMode, PreserveSelectionOnModeSwitch
- `[Input]` ActivateKey (Delete)
- `[Confirm]` HoldSeconds, ShowRing, ShowSelectedCount
- `[Selection]` AllowEnvironmentWithFilter, ClearQueueOnToolSwitch, MaximumTargets, ExtraDeny/Allow
- `[Safety]` WarnWhenDeletingOccupiedContainers, RefuseUnknownDeleteTargets
- `[Integration]` InstallInfinityHammerTool, PreferInfinityHammerOnly
- `[Removal]` DryRunOnly

## Commands

| Command | Purpose |
| --- | --- |
| `dismantleheim mode dismantle\|delete` | Enter / switch mode |
| `dismantleheim activate` | Enter DefaultMode |
| `dismantleheim deactivate` | Leave mode |
| `dismantleheim status` | State / mode / queue / filter |
| `dismantleheim clear` | Clear queue |
| `dismantleheim why` | Last selection reject reason |

## Install / Build / Uninstall

1. BepInExPack + Jötunn (+ Infinity Hammer for Tools entries).
2. Place `Dismantleheim.dll` in `BepInEx/plugins/`.

```powershell
.\build.ps1
.\test.ps1
```

Uninstall: remove the DLL; optional delete `BepInEx/config/infinity_tools_dismantleheim.yaml` (or `config/tools/`).

## Acceptance

- Dual-mode: [docs/acceptance-Gxx.md](docs/acceptance-Gxx.md)
- Selection/confirm history: [docs/acceptance-Hxx.md](docs/acceptance-Hxx.md)
