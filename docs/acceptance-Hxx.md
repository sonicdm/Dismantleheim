# Dismantleheim acceptance matrix (H01–H25)

Record results before shipping a version gate. Fill Date / Game / IH / Result columns when running in-game.

| Field | Value |
| --- | --- |
| Date | _(pending in-game)_ |
| Game build | |
| Infinity Hammer version | |
| Profile | disposable world + backup |
| DLL version | 0.1.0 |

## Gates

- **0.1 dry-run:** H01–H03, H05–H07, H09–H15, H18, H21, H22, H24, H25 (H12 = dry-run log only)
- **0.2 removal:** prior + H08, H16, H19, H20, H23, H12 real deletes on pieces
- **1.0:** full H01–H25 including H04, H17, mod-stack matrix

## Results

| ID | Test | Required outcome | Gate | Result | Notes |
| --- | --- | --- | --- | --- | --- |
| H01 | Equip IH with Dismantleheim | One Tools entry; no new inventory Hammer | 0.1 | pending | |
| H02 | Pipettes subgroup | No forged/duplicate pipette | 0.1 | pending | |
| H03 | Remove Infinity Hammer | No crash; ActivateKey + console work | 0.1 | pending | |
| H04 | SonicWorldTools coexistence | Independent entries | 1.0 | pending | |
| H05 | Switch to normal build tool | Ordinary clicks restored; queue cleared; no delete | 0.1 | pending | |
| H06 | Shift+M3 on wall | Filter sampled; pipette suppressed only while active | 0.1 | pending | |
| H07 | Shift+M3 matching wall | Filter cleared; queue remains | 0.1 | pending | |
| H08 | Sample rock/tree | Exact prefab; whole object highlight | 0.2 | pending | |
| H09 | No filter near trees+buildings | Nature excluded | 0.1 | pending | |
| H10 | Flag 8 objects | All stay queued; no removal | 0.1 | pending | |
| H11 | Mouse3 0.3s release | Ring resets; queue preserved | 0.1 | pending | |
| H12 | Hold to threshold | One execution; rearm on key-up | 0.1/0.2 | pending | 0.1=dry-run |
| H13 | Shift during Mouse3 | Sampling only | 0.1 | pending | |
| H14 | RMB with queue | Ordinary menu | 0.1 | pending | |
| H15 | Focus loss mid-hold | Cancel; no op | 0.1 | pending | |
| H16 | Object gone before commit | Skip; no expand | 0.2 | pending | |
| H17 | Multi-floor volume | Y bounds | 1.0 | pending | D7 |
| H18 | Chunk unload / disconnect | No stale delete; visuals cleared | 0.1 | pending | |
| H19 | Ward / restricted | Denied + reason | 0.2 | pending | |
| H20 | Vanilla observer | Sync without mod | 0.2 | pending | |
| H21 | YAML install twice | Two uniquely named Tools entries; shared tools preserved | 0.1 | pending (in-game) / synthetic pass | `Installer.Tests` cover dual owned `hammer:` doc; menu load still pending |
| H22 | 100+ selection | Bounded cost | 0.1 | pending | |
| H23 | Multi-part tree/rock | Scope match or reject | 0.2 | pending | |
| H24 | Crash mid-hold | No deferred remove | 0.1 | pending | |
| H25 | ActivateKey Delete | Toggle; no delete; chat ignored; rebound works | 0.1 | pending | |

## Automated (always before ship)

```powershell
.\build.ps1
.\test.ps1
```

Core.Tests cover queue, sampler, eligibility, FSM, confirm hold, removal plan.
Installer.Tests cover owned equipment-keyed YAML write, renamed-entry preservation, conflict backup on dual-path migration, quoted-name cleanup, and “other tool has activate command” association (synthetic; not IH menu load). Live removal reports `PendingNetwork` until the instance is gone (see `docs/authority.md`).
AssemblyCompatibility.Tests check `Piece.m_canBeRemoved` / `Player.RemovePiece` / `CheckCanRemovePiece` when Reqs present.

## Ship rule

Do **not** tag/publish a gate version if any required row for that gate is `fail`. In-game rows marked `pending` block 0.1 Thunderstore claim until filled `pass` on a disposable world.
