# Changelog

## 0.1.0

Beta release — dual-mode queued selection, live-tested on a disposable world.

- **Mass Dismantle** / **Mass Delete** Infinity Hammer Tools entries (`hammer_menu` → Tools), ActivateKey (default Delete), or `dismantleheim mode|activate`
- Click-add selection; Ctrl+wave paint-add; Ctrl+click unselect; Shift+Mouse3 prefab filter; hold Mouse3 confirm (early release cancels hold, keeps queue)
- Mass Delete: environment/props only after an explicit prefab sample; Mass Dismantle never queues env
- Hover/selection glow for build pieces and env/props (MaterialMan tint)
- Owned IH YAML install/migrate with conflict-safe preservation
- `Removal.DryRunOnly` defaults **true** (ship-safe); set false only on disposable worlds
- Automated Core + Installer tests; acceptance docs in `docs/acceptance-Gxx.md` / `docs/acceptance-Hxx.md`
