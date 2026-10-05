# Removal authority (dual-mode)

Observed against `assembly_valheim.dll` in workspace `Reqs` (spike for Phase E). Default: `Removal.DryRunOnly = false` (live remove). Set true for plan-only testing.

## Spike: `Player.RemovePiece` (managed IL)

Vanilla Mouse3 remove (simplified):

1. Camera ray → `Piece` (or terrain modifier piece).
2. `PrivateArea.CheckAccess(pos, 0, flash:true, wardCheck:false)`.
3. `Player.CheckCanRemovePiece(piece)` — crafting-station range / global-key gates (private method).
4. `Piece.CanBeRemoved()` — **not** only `m_canBeRemoved`:
   - `Container.CanBeRemoved()`: public chests with `Inventory.NrOfItems() > 0` return **false** (vanilla occupied-chest refusal).
   - Ships have their own `CanBeRemoved`.
5. Optional `IRemoved.OnRemoved()`.
6. `WearNTear.Remove(bool)` → `ZNetView.InvokeRPC("RPC_Remove", bool)` (network destroy).
7. **`Piece.DropResources(HitData)`** — construction refunds (separate from WearNTear).
8. Place effects; fallback `ZNetScene.Destroy` in some branches.

Implication: calling `WearNTear.Remove` alone does **not** refund materials. Mass Dismantle must invoke the refund step (or the full native path). Mass Delete must invoke destroy **without** `DropResources` and without harvest callbacks.

## Modes

### Mass Dismantle

- Targets: removable **build pieces** only. Environment never eligible (filter does not unlock harvest).
- Live path: ward access → `CheckCanRemovePiece` → `Piece.CanBeRemoved()` → `WearNTear.Remove(false)` → `Piece.DropResources(null)` once. Never spawn extra recipe drops.
- Occupied ordinary chests: skip when `Piece.CanBeRemoved()` is false (matches vanilla).

### Mass Delete

- Targets: build pieces; environment only with explicit prefab filter + `WearNTear` scope preview.
- Live path: ward access → authorized exact delete via `WearNTear.Remove(false)` **without** `DropResources`. No chopping/mining loot.
- Occupied containers: allowed after HUD contents-loss warning; not a categorical ban.
- Unknown / no-preview / unsafe lifecycle → skip `Unsafe delete`.

## Live outcome reporting

`WearNTear.Remove` issues `RPC_Remove` and may complete asynchronously. Live execution only sets `Removed=true` when the instance is already gone after the call. If the instance remains, the result is `PendingNetwork` (logged as PENDING); the queue is not cleared for that target. Dry-run never claims removal.

## Dry-run

Logs per-object plan lines: mode, drop policy (`RefundsViaNative` / `NoDrops` / skip reason). No RPC / DropResources.

## Hard safety

- Exact ZDO identities only; no `id=*` / area wipes.
- `ExtraAllow` only overrides `ExtraDeny`.
- Env without whole-object preview (`HasScopePreview`) rejected.
- World UID + peer epoch; logout hard-resets queue.

## Not supported / not claimed

- Guaranteed env wipe on unmodded dedicated hosts.
- Undo, fabricated harvest loot, double refunds.
- Observer sync beyond ordinary WearNTear network destroy.
