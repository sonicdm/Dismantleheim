# Dismantleheim acceptance matrix (G01–G20)

Addendum Mass Dismantle / Mass Delete gates. Record in-game results before shipping Gate B/C/D claims.

| Field | Value |
| --- | --- |
| Date | _(pending in-game)_ |
| Game build | |
| Infinity Hammer version | |
| Profile | disposable world + backup |
| DLL version | 0.1.0 |

## Automated Gate A (offline)

| Check | Result |
| --- | --- |
| `.\build.ps1` | run locally |
| Core.Tests (policies, mode eligibility, queue cap, FSM) | automated |
| Installer.Tests (two YAML tools, preserve/migrate) | automated |
| AssemblyCompatibility (`RemovePiece`, `DropResources`, `CanBeRemoved`) | automated when Reqs present |

## In-game results

| ID | Scenario | Pass condition | Gate | Result | Notes |
| --- | --- | --- | --- | --- | --- |
| G01 | One vanilla wall Mass Dismantle | Same materials/restrictions as Hammer; no duplicate drops | B | pending | |
| G02 | Several connected floors/walls Dismantle | Only queued exact objects | B | pending | |
| G03 | Same structures Mass Delete | No material drops | C | pending | |
| G04 | Empty chest both modes | Dismantle matches vanilla; Delete no loot | B/C | pending | |
| G05 | Occupied chest both modes | Dismantle skip/refuse; Delete warns + no drops | B/C | pending | |
| G06 | Personal/restricted chest | Server/ownership rules enforced | B/C | pending | |
| G07 | Buildable decorative rock | Recipe refund only in Dismantle | B/C | pending | |
| G08 | Filtered natural rock/tree Delete | Exact targets; no harvest drops | C | pending | |
| G09 | Multi-part rock/tree | Full footprint highlight or refuse | C | pending | |
| G10 | 50+ selection | Cap works; no extras | A | pending | |
| G11 | Short Mouse3 | No dismantle; ring resets | A | pending | |
| G12 | Held Mouse3 | One batch max | A | pending | |
| G13 | Shift+Mouse3 | Sample only | A | pending | |
| G14 | Right-click | Standard Hammer menu | A | pending | |
| G15 | IH installed | Both Tools entries; one Hammer | D | pending | |
| G16 | IH absent | Hotkey/console only; no broken buttons | D | pending | |
| G17 | DragNBuild stack | No input leakage when switched | D | pending | |
| G18 | Dedicated server | Authority boundaries / observer | E | pending | |
| G19 | Interrupted hold / UI / disconnect | No destructive action | A | pending | |
| G20 | Crash/network mid-batch | Honest per-target summary | C/E | pending | |

## Ship rule

Do **not** claim Gate B/C/D/E until required rows are `pass` on a disposable world with recorded game/IH versions. `DryRunOnly` defaults to false (live); use true when validating plan-only behavior.
