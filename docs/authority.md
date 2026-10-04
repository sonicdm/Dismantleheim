# Removal authority (0.1 → 0.2)

## Dry-run (0.1 default)

`Removal.DryRunOnly = true` logs an immutable `RemovalPlan` of exact network identities (world UID session + ZDO UserId/ObjectId + prefab/kind). No destroy calls are invoked.

## Building pieces (when DryRunOnly = false)

Each target is revalidated at commit (session, prefab, kind, instance). Then:

1. `PrivateArea.CheckAccess` at the object position (ward/private area).
2. Container-contents / portal / ward-object rejects.
3. `Piece.m_canBeRemoved` and `Player.CheckCanRemovePiece`.
4. Destroy via **`WearNTear.Remove()`** on the exact network root (same network destroy path hammer remove uses). This is **not** a call to `Player.RemovePiece()` — that method is suppressed while Dismantleheim owns input so confirmation does not race vanilla Mouse3 remove.

Skips with logged reasons when any gate fails. `Removed=true` is reported only after the destroy call returns; missing instances stay skipped.

## Environment (rocks / trees)

Allowed into the **queue** only with an explicit prefab sample filter (and only recognized `TreeBase` / `TreeLog` / `MineRock` / `MineRock5` roots). Real deletion requires the client to be the server (listen host) and still uses WearNTear.Remove when present, else host-only `ZNetScene.Destroy`. Dedicated vanilla servers without host authority are **not** promised.

## Hard safety

- `ExtraAllowPrefabs` only overrides `ExtraDenyPrefabs` — never type, removability, or environment-without-filter rules.
- Unsupported / unknown networked objects are never eligible.
- Wildcard / area descriptors are rejected in the plan builder.

## Not supported

- Wildcard / area remove commands
- Refunds or undo
- Guaranteed rock/tree wipe on unmodded dedicated hosts
- Claiming observer sync beyond ordinary WearNTear network destroy behavior
