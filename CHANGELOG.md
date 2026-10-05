# Changelog

## Unreleased

- README soft-dependency links for Infinity Hammer on Thunderstore and Hexium

## 0.1.0

Beta — first public package. Satisfactory-inspired dual-mode queued selection, live-tested on a disposable world. Same `0.1.0` tag was refreshed as this landed (icon, live default, docs).

### Modes

- **Mass Dismantle** — reclaim construction materials like the normal Hammer (`WearNTear.Remove` + `Piece.DropResources`)
- **Mass Delete** — erase with **no** material/harvest drops; occupied containers warn then may delete
- Nothing removes on hover, select, tool switch, or mode switch — only after hold-to-confirm Mouse3
- HUD labels: `MASS DISMANTLE — Returns Materials` / `MASS DELETE — No Drops`, plus selection count

### Activation

- Infinity Hammer Tools: `hammer_menu` → Tools → **Dismantleheim - Mass Dismantle** / **Mass Delete**
- ActivateKey (default **Delete**) while in hammer placement / DefaultMode
- Console: `dismantleheim mode dismantle|delete`, `activate`, `deactivate`, `status`, `clear`, `why`
- IH Instant Tools: activate on menu select (BuildUi path); holster / other-tool / re-equip ownership handled without sticky HUD
- No second inventory Hammer — owned `infinity_tools_dismantleheim.yaml` only

### Selection & controls

- Plain LMB: click-add only (does not toggle off)
- Hold **Ctrl** + wave aim: paint-add nearby valid targets (add-only; no box select)
- **Ctrl+click**: unselect one hovered target
- **Shift+Mouse3**: sample / clear prefab filter (vanilla pipette + missing-requirement toast suppressed while active)
- Hold **Mouse3**: confirmation ring; early release cancels the hold and **keeps** the queue
- Escape: cancel hold or clear queue; holster / re-equip also clears mode ownership
- Mass Delete: rocks / trees / props queue **only** after an explicit prefab sample; Mass Dismantle never queues environment
- Hover + queued glow for build pieces **and** env/props (MaterialMan tint; WearNTear when present)

### Removal authority

- Exact queued ZDO identities only — no wildcard / area deletes
- Separate dismantle vs delete adapters (no shared `RemoveEverything(bool)`)
- Mass Dismantle: ward → `CheckCanRemovePiece` → `Piece.CanBeRemoved` → refund path
- Mass Delete: ward → remove/destroy without DropResources; Destructible env/props supported when sampled
- `Removal.DryRunOnly` defaults **false** (live); set **true** for plan-only testing / logging

### Integration & packaging

- Conflict-safe Infinity Hammer YAML install/migrate; preserve non-stock owned tool files; honest removal reporting
- Soft dependency on Infinity Hammer; works with vanilla hammer via ActivateKey / console
- Thunderstore zip: `manifest.json`, README, 256×256 icon, CHANGELOG, `Dismantleheim.dll`
- Mod icon: Satisfactory dismantle tool + Valheim-style scene

### Tests & docs

- Automated Core + Installer + AssemblyCompatibility tests
- Acceptance: `docs/acceptance-Gxx.md` (dual-mode), `docs/acceptance-Hxx.md` (selection/confirm), `docs/authority.md`
