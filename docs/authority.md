# Removal authority (0.1 → 0.2)

## Dry-run (0.1 default)

`Removal.DryRunOnly = true` logs an immutable `RemovalPlan` of exact network identities. No `Player.RemovePiece` / ZDO destroy is invoked.

## Building pieces (0.2 path)

When dry-run is off, each validated target with a `Piece` component is removed via **`Player.RemovePiece`** (vanilla path). Skips:

- Stale / missing ZDO or instance
- Containers with contents
- Portals (`TeleportWorld`)
- Wards (`PrivateArea`)
- `m_canBeRemoved == false`

## Environment (rocks / trees)

Allowed into the **queue** only with an explicit prefab sample filter. Real deletion on dedicated vanilla servers is **not** promised. Current validator rejects environment removes unless the client is the server (listen host). Further admin/Server Devcommands integration may expand this later — document any change here before advertising it.

## Not supported

- Wildcard / area remove commands
- Refunds or undo
- Guaranteed rock/tree wipe on unmodded dedicated hosts
