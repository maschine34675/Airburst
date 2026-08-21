# Changelog

## [Unreleased]

## [1.0.0]

### Forge version notes
- Initial release: two experimental 40x46mm grenades for all MSGL/M203/FN40GL launchers, sold by Skier LL2.
- XM1166 HEAB: range-lock cover with the `J` hotkey (in-game toast confirms the range and suggests the sight zeroing), then lob the round over it — it detonates mid-air right above the target. Works meter-exact with ScopeRangefinder installed, and with plain sight zeroing without it.
- Single player only: not supported with Fika co-op (the M397A1 stays inert there and airbursts desync between players).
- M397A1 Jump-Up: bounding grenade that jumps visibly off the surface it hits — ground hits pop straight up, wall hits pop into the room — and detonates about 1.5 m out. Arms after ~14 m; closer impacts are duds.

### Added
- XM1166 HEAB airburst round: flight tracking via `BallisticsCalculator` postfixes, horizontal-distance fuze with frame interpolation, instant detonation through the vanilla explosion pipeline, safety minimum of 20 m, and a skip-and-impact fallback when a mortar lob crosses the set range far above the target.
- Range lock with self-hit-filtered raycast fallback, weapon-bound and local-shooter-only state, IMGUI-focus and raid gates, and toast feedback including the nearest sight calibration step.
- ScopeRangefinder soft dependency via reflection against its stable `RangefinderApi` contract.
- M397A1 Jump-Up bounding round: `FuzeArmTimeSec 999` keeps the vanilla impact path inert while the mod's `ShotDelegate` postfix acts as the fuze, launching the charge as a real ballistic projectile along the surface normal and detonating it after the configured jump height (or at an obstacle that cuts the jump short).
- Configurable template-ID lists so explosive rounds from other mods can reuse both mechanisms; F12 menu with friendly display names and deliberate ordering.
