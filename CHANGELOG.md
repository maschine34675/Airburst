# Changelog

## [Unreleased]

## [1.1.0]

### Forge version notes
- Fika co-op support (experimental; tested with a headless host and one client): the shooter's burst solution (range lock, ScopeRangefinder, zeroing) is sent to every peer, so XM1166 rounds detonate at the same point on all machines; the M397A1 fuze no longer depends on a game hook that Fika replaces. Every player and the headless host must run the identical Airburst build, and the host should add `com.maschine.Airburst` to Fika's `client.mods.required` - a peer without the mod drops network packets on every airburst shot. New file: `BepInEx/plugins/maschine-Airburst.Client.Fika.dll` (only used when Fika is installed).

### Added
- Fika co-op synchronisation of the airburst solution: the machine that owns a shot (the local shooter, or the host for bots) broadcasts a small `ReliableOrdered` packet `{shooter profile, burst distance, target height, shot start position}`; peers attach it to their locally replayed shot with the same shooter and start position (retro-apply or short pending queue, 2 s TTL). Fika is a BepInEx soft dependency; all Fika-typed code lives in the new satellite `maschine-Airburst.Client.Fika.dll`, which is only loaded when Fika 2.4.x is present - without Fika nothing changes.

### Changed
- The range-lock hotkey now only reacts (lock, clear, toast) when the weapon in hand - or its underbarrel launcher - can actually fire an airburst round (matching caliber, or such a round loaded); with any other weapon the key is left to other mods.
- Range locks now also apply to underbarrel launcher shots (M203): the game hands the launcher item to the ballistics, the lock was bound to the rifle in hand, so it never matched.
- Gameplay messages (lock set/cleared/refused, shell tracked, peer re-target, high-arc skip) moved from Info/Warning to the Debug log level; Info/Warning are reserved for lifecycle and real failures.
- Jump-Up impact detection moved from a `ClientGameWorld.ShotDelegate` postfix into the shared flight tracker (`BallisticsCalculator.UpdateShots` postfix, `Shot.HasAchievedTarget` + `HitPoint`/`HitNormal`), so it runs on every role including Fika joiners where `FikaClientGameWorld` overrides `ShotDelegate` without calling base. Behavior in single player is unchanged.

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
