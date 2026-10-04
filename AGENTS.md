# AGENTS.md — Dismantleheim

Guidance for Cursor agents (and humans) working in this repo.

## What this is

Valheim **BepInEx** mod: Satisfactory-inspired queued dismantle through the Hammer. Selection-first; confirm with hold Mouse3. Infinity Hammer Tools entry when installed; ActivateKey (default Delete) always available.

| | |
| --- | --- |
| GUID | `com.sonicdm.valheim.dismantleheim` |
| Assembly | `Dismantleheim.dll` |
| Source | `src/` + `src.Core/` (Core sources compile into the plugin DLL) |
| Tests | `tests/` via `.\test.ps1` (Core.Tests / Installer.Tests / AssemblyCompatibility.Tests) |

## Product rules

- Never wildcard / `id=*` area deletes. Exact queued identities only.
- No second inventory Hammer from this mod. IH entry via YAML upsert into `infinity_tools.yaml`.
- Unfiltered selection: `Piece` + `m_canBeRemoved`, deny environment (`TreeBase` / `MineRock*` / …). No generated piece allowlist.
- Prefab sample (Shift+Mouse3) may allow matching environment into the queue; permissions still apply at execute.
- v0.1: `Removal.DryRunOnly=true` by default — no world deletes until acceptance gate for 0.2.
- Client systems skip when `GUIManager.IsHeadless()`.
- Do not patch Infinity Hammer private APIs.

## Entry

1. Infinity Hammer: `hammer_menu` → Tools → **Dismantleheim**
2. Hotkey: `ActivateKey` (default Delete)
3. Console: `dismantleheim activate|deactivate|status|clear|why`

## Valheim references

```text
E:\Scripts\Valheim Mods\Reqs
```

Never commit those DLLs. GitHub Actions only refresh release notes from `CHANGELOG.md`.

## Build habits

```powershell
.\build.ps1
.\test.ps1
```

Prefer build/test only while iterating — do not package/release unless asked.

## Version bumps

Stay on **`0.1.0`** until the user says to ship. Keep PluginVersion / csproj / manifest / README / CHANGELOG aligned when shipping.

## Install

Do **not** copy the DLL into an r2modman profile unless asked. Output: `bin\Release\Dismantleheim.dll` and `dist\Dismantleheim.dll`.

## Acceptance

See [docs/acceptance-Hxx.md](docs/acceptance-Hxx.md). Fail ship if the version gate tests fail.
