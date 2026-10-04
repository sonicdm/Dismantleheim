# Dismantleheim

Satisfactory-inspired **queued dismantle** for Valheim. Flag pieces, optionally sample a prefab filter, then hold Mouse3 to confirm. Nothing is deleted on hover or tool switch.

| | |
| --- | --- |
| Version | 0.1.0 |
| GUID | `com.sonicdm.valheim.dismantleheim` |
| Dependencies | BepInExPack Valheim, [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) |
| Soft dependency | [Infinity Hammer](https://thunderstore.io/c/valheim/p/JereKuusela/Infinity_Hammer/) (Tools menu entry) |

## How to get the tool

### With Infinity Hammer (recommended)

1. Install Infinity Hammer (+ Server Devcommands as required by IH).
2. Run **`hammer_menu`** to spawn the Infinity Hammer into your inventory.
3. Equip it → build menu → **Tools** → **Dismantleheim**.
4. Or press **Delete** (configurable `ActivateKey`) to toggle mode.

### Without Infinity Hammer

Press **Delete** (or your configured ActivateKey), or run `dismantleheim activate`. There is no Tools menu entry and no extra hammer item.

## Controls (while active)

| Input | Action |
| --- | --- |
| Left click | Toggle hovered object in the queue |
| Shift + Mouse3 | Sample prefab filter (same prefab clears; other switches) |
| Hold Mouse3 | Confirmation ring; release early cancels |
| Right click | Normal build menu |
| Escape | Cancel hold or clear queue |
| ActivateKey again | Deactivate mode |

Unfiltered selection only allows hammer-removable **Pieces** (not trees/rocks). Sample a tree/rock prefab to queue matching environment objects; deletion authority still applies (see below).

## v0.1 dry-run

Config `Removal.DryRunOnly` defaults to **true**. Completing the confirm hold **logs** the exact removal plan to the BepInEx log — it does **not** delete world objects yet. Set `DryRunOnly = false` only after you accept the risk on a disposable world (path toward 0.2).

## Authority / multiplayer

- When dry-run is off, pieces are destroyed via `WearNTear.Remove` after `CheckCanRemovePiece` + ward `CheckAccess` (not a direct `Player.RemovePiece` call). See `docs/authority.md`.
- Environment (rocks/trees) on a vanilla dedicated server is **not** promised; host/admin limits are documented as we harden 0.2+.
- Observers see ordinary networked WearNTear destroys without needing this mod.

## Config

- `[General]` Enabled, DebugLogging
- `[Input]` ActivateKey (default Delete)
- `[Confirm]` HoldSeconds, ShowRing, ShowSelectedCount
- `[Selection]` AllowEnvironmentWithFilter, ClearQueueOnToolSwitch, ExtraDenyPrefabs, ExtraAllowPrefabs
- `[Integration]` InstallInfinityHammerTool, PreferInfinityHammerOnly
- `[Removal]` DryRunOnly

## Commands

| Command | Purpose |
| --- | --- |
| `dismantleheim activate` | Enter mode |
| `dismantleheim deactivate` | Leave mode |
| `dismantleheim status` | State / queue / filter |
| `dismantleheim clear` | Clear queue |
| `dismantleheim why` | Last selection reject reason |

## Install

1. Install BepInExPack + Jötunn (and Infinity Hammer if you want the Tools menu entry).
2. Place `Dismantleheim.dll` under `BepInEx/plugins/` (single DLL — Core is compiled in).

## Build

```powershell
.\build.ps1
.\test.ps1
```

## Uninstall

Remove the DLL. Optional: delete `BepInEx/config/infinity_tools_dismantleheim.yaml` (or under `config/tools/`). A prior shared-file entry is cleaned on next install write.

## Acceptance tests

Automated: `.\test.ps1`. In-game matrix: [docs/acceptance-Hxx.md](docs/acceptance-Hxx.md).
