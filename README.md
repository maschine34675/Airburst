# Airburst

Two experimental 40x46mm grenades for SPT that beat cover instead of hitting it: the **XM1166 HEAB** detonates mid-flight at the range you set, the **M397A1 Jump-Up** jumps off the surface it hits before exploding.

## Features

- **XM1166 HEAB** (red tracer): a full HE round that additionally detonates mid-air at a set distance, raining fragments down on whatever hides behind cover — XM25 style, instant, no time for the target to react. On a direct hit it simply explodes like a regular HE round.
- **Range lock:** aim down sights at the cover, press the lock hotkey (`J` by default), then lob and fire. The lock stays until you re-lock or the raid ends — press the hotkey from the hip to clear it. Every press answers with an in-game toast: the locked ground range plus the nearest sight zeroing step, or why the lock was refused.
- **M397A1 Jump-Up** (green tracer): the classic 1964 bounding grenade. On impact, an ejection charge kicks the HE charge — a real, visible projectile — about 1.5 m **along the surface normal** before it detonates: ground hits pop straight up (fragments over anyone prone or crouched), wall hits pop out of the wall into the room. Arms after ~14 m of flight; closer impacts are duds, exactly like the real one.
- Both rounds fit all 40x46 launchers (MSGL drum, M203, FN40GL) and are sold by Skier at loyalty level 2 (21,250 ₽, limited stock of 18 per restock).
- Optional **ScopeRangefinder** integration for meter-exact ranging, and configurable template-ID lists so explosive rounds from other mods can use the same mechanisms.

## Requirements and compatibility

- SPT: 4.1.x; built and directly tested on SPT 4.1 in single player.
- Components: combined client (BepInEx plugin) and server mod — both parts must be installed.
- Dependencies: [WTT-ServerCommonLib](https://github.com/WelcomeToTarkov/WTT-CommonLib) (`com.wtt.commonlib`) is required. [ScopeRangefinder](https://github.com/maschine34675/ScopeRangefinder) is optional — with it installed, locks and bursts use its meter-exact live measurement instead of the 50 m sight zeroing steps.
- **Not compatible with Fika (co-op):** Fika replaces the game hook the M397A1 relies on, leaving that round completely inert, and the XM1166's mid-air detonation is computed per machine, so peers see different results (static analysis, 2026-08 audit). Single player only.

## Installation

1. Extract the release ZIP into your SPT installation directory.
2. Verify that `BepInEx/plugins/maschine-Airburst.Client.dll` and `SPT_Runtime/user/mods/Airburst/maschine-Airburst.Server.dll` exist.
3. Start the server, then the game; the rounds appear at Skier (LL2).

## Updating

Extract the new ZIP over the old files; nothing needs to be removed. Items already in your stash keep working because the item IDs are stable.

## Usage

The intended workflow for the XM1166 is the same as the real thing: **range the cover, then lob the round over it.**

- Aim down sights at the cover, press `J`, then aim higher and fire. The lock belongs to the weapon it was set with, so zeroing a rifle later cannot hijack the launcher's range, and it is refused below a 20 m safety minimum (the round carries a full HE charge with a 7 m blast radius).
- The lock hotkey defaults to the same key as ScopeRangefinder's zeroing hotkey, so a single press can do both — note that SR only zeroes if its own Auto Zero is switched on (it ships disabled).
- A locked or measured range gets `AirburstBurstOffset` (default 1 m) added so the shell bursts *past* the cover rather than level with it — real fire control does the same, and it keeps the wall from shielding whoever is behind it.
- Ranges are measured as line of sight but the fuze counts **ground distance**, so shooting down from a rooftop bursts where you aimed rather than long. A wild mortar lob crosses the set distance far above the target, and rather than pop uselessly up there the round simply flies on and impacts.
- **Without a lock** the round falls back to the sight zeroing at the moment of the shot (MSGL reflex sight: 50 m steps from 50 to 400 m, a fresh sight sits at 50 m), and without any sight to `AirburstDefaultDistance`. The distance source is logged per shot in the BepInEx log.
- The **M397A1** needs no fire control: shoot at the ground or wall next to your target.

## Configuration

`BepInEx/config/com.maschine.Airburst.cfg` (also live in the F12 menu):

| Setting | Default | Description |
|-------------|----------|--------------|
| `Enabled` | `true` | Mod on/off |
| `AirburstLockHotkey` | `J` | Lock the measured range (same key as ScopeRangefinder's zeroing by default) |
| `AirburstBurstOffset` | `1` | Meters added to a measured range so the burst clears the cover (0–10) |
| `AirburstDefaultDistance` | `100` | Detonation distance in meters without a mounted sight (25–400) |
| `UseScopeRangefinderDistance` | `true` | Prefer ScopeRangefinder's live measured distance when available |
| `AirburstShellTemplateIds` | `67d4f0c8a1b2e30123457041` | Comma-separated template IDs treated as airburst rounds |
| `JumpUpHeight` | `1.5` | Meters the charge jumps from the surface before detonating (0.5–3) |
| `JumpUpTemplateIds` | `67d4f0c8a1b2e30123457043` | Comma-separated template IDs treated as bounding grenades |

**Other mods' ammunition:** any explosive round (`HasGrenaderComponent: true`) can be given either mechanism by adding its template ID to the matching list; the detonation then uses that round's own explosion values. Known candidate: WTT-Armory's 25x59mm XM1019 (`6938bc0b6e96bcf17932873e`) for the Barrett XM109, once that mod is available for SPT 4.1.

## Known limitations

- Not compatible with Fika (see above).
- The GP-25 uses a different caliber (40mmRU) and cannot fire these rounds.
- On a flat direct shot the XM1166's burst point coincides with the impact — the round is made for lobbing over cover, not for direct fire.
- Both rounds reuse the vanilla M381 model; tell them apart by name, tracer color, and the in-game toast.

## Support

Report issues on the [GitHub issue tracker](https://github.com/maschine34675/Airburst/issues). Include the exact mod and SPT versions, expected and actual behavior, short reproduction steps, and your complete `BepInEx/LogOutput.log` rather than pasted excerpts.

## License and credits

MIT License (see `LICENSE`). Item loading via [WTT-ServerCommonLib](https://github.com/WelcomeToTarkov/WTT-CommonLib). The real-world rounds that inspired the two items are the US Army's XM1166 40 mm LV HEAB program and the M397A1 bounding grenade.

## Build

```powershell
cd D:\SPT41\Development\Airburst
dotnet build .\Airburst.slnx -c Release
```

With `-p:DeployToSpt=true` (default) the client DLL goes to `BepInEx/plugins/` and the server DLL plus item JSON to `SPT_Runtime/user/mods/Airburst/`. `scripts\New-ReleasePackage.ps1` builds both projects and stages the release ZIP under `artifacts/`.
