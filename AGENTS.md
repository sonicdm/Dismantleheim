# AGENTS.md — Dismantleheim

Guidance for Cursor agents (and humans) working in this repo.

## What this is

Valheim **BepInEx** mod: Satisfactory-inspired queued selection with two modes — **Mass Dismantle** (vanilla refunds) and **Mass Delete** (no drops). Infinity Hammer Tools entries when installed; ActivateKey (default Delete) toggles DefaultMode.

| | |
| --- | --- |
| GUID | `com.sonicdm.valheim.dismantleheim` |
| Assembly | `Dismantleheim.dll` |
| Source | `src/` + `src.Core/` (Core sources compile into the plugin DLL) |
| Tests | `tests/` via `.\test.ps1` |

## Product rules

- Never wildcard / `id=*` area deletes. Exact queued identities only.
- No second inventory Hammer. Owned `infinity_tools_dismantleheim.yaml` with **two** Tools entries (`mode dismantle` / `mode delete`).
- Separate policies/adapters — no `RemoveEverything(bool)`.
- Mass Dismantle: build pieces only; `WearNTear.Remove` + `Piece.DropResources`; occupied chests follow `Piece.CanBeRemoved`.
- Mass Delete: no DropResources; env only with prefab filter + WearNTear preview; occupied containers warn then may delete.
- ExtraAllow never bypasses type/env/preview rules.
- `Removal.DryRunOnly=true` by default until Gate B/C.
- Client systems skip when `GUIManager.IsHeadless()`.
- Do not patch Infinity Hammer private APIs.

## Entry

1. Infinity Hammer: `hammer_menu` → Tools → **Mass Dismantle** or **Mass Delete**
2. Hotkey: `ActivateKey` (default Delete) → DefaultMode
3. Console: `dismantleheim mode dismantle|delete` · `activate|deactivate|status|clear|why`

## Build habits

```powershell
.\build.ps1
.\test.ps1
```

Shipped **`0.1.0` beta**. Bump all five version locations + `CHANGELOG.md` before the next release. Do not copy DLL into r2modman/Gale unless the user asks (or is blocked testing a live install). Active Gale profile: `com.kesomannen.gale\valheim\profiles\Default`.

## Acceptance

- Prior selection/confirm: [docs/acceptance-Hxx.md](docs/acceptance-Hxx.md)
- Dual-mode: [docs/acceptance-Gxx.md](docs/acceptance-Gxx.md)
- Authority spike: [docs/authority.md](docs/authority.md)
