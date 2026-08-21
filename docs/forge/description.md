Two experimental 40x46mm grenades that beat cover instead of hitting it. The **XM1166 HEAB** detonates mid-flight at a range you set — lase the cover, lob the round over it, and it bursts right above whoever hides behind. The **M397A1 Jump-Up** is the classic bounding grenade: on impact it kicks its charge about 1.5 m off the surface before exploding, catching anyone prone or crouched nearby.

## Features

- **Range lock:** aim at the cover, press the lock hotkey (`J` by default), then fire with elevation. An in-game toast confirms the locked range and suggests the matching sight zeroing step. Press from the hip to clear the lock.
- **XM1166 HEAB:** full HE round with a red tracer; bursts at the locked range, at the sight zeroing when no lock is set, and explodes normally on direct hits. Refuses to arm below 20 m for your own safety.
- **M397A1 Jump-Up:** green tracer; jumps visibly off the surface it hits — ground hits pop straight up, wall hits pop into the room. Arms after ~14 m of flight; closer impacts are duds, like the real one.
- Both rounds fit all 40x46 launchers (MSGL drum, M203, FN40GL) and are sold by Skier at loyalty level 2 (21,250 ₽, 18 per restock).

## Installation and first use

Extract the release ZIP into your SPT installation directory. Client files go to `BepInEx/plugins/`, server files to `SPT_Runtime/user/mods/Airburst/`. Start the server, buy the rounds from Skier, and try the XM1166 on a target behind sandbags: lock the sandbags with `J`, aim a little higher, fire.

## Requirements and compatibility

- SPT: 4.1.x, built and tested on 4.1 (single player).
- Dependencies: [WTT-ServerCommonLib](https://github.com/WelcomeToTarkov/WTT-CommonLib) (`com.wtt.commonlib`) is required. [ScopeRangefinder](https://github.com/maschine34675/ScopeRangefinder) is optional — with it installed, locks and bursts use its meter-exact live measurement instead of the 50 m sight zeroing steps.
- **Not compatible with Fika (co-op):** the M397A1 stays inert there and the XM1166's mid-air detonation desyncs between players.

## Known limitations

- The GP-25 uses a different caliber (40mmRU) and cannot fire these rounds.
- On a flat direct shot the XM1166's burst point coincides with the impact — the round is made for lobbing over cover, not for direct fire.

## Support

Report issues on the GitHub issue tracker with your exact mod and SPT versions, what you expected, what happened, short reproduction steps, and your complete BepInEx log file.
