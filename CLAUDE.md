# ArenaPrototype

A twin-stick **vehicle arena shooter** for mobile (iOS/Android). Early prototype.
The player's car is the **Wraith** (a sports car; scene object `Wraith`, tag `Player`).

`ModeMenu` offers seven modes: **Deathmatch**, **Capture the Flag**,
**Gridlock**, and **Wreckoning** (all four run through `MatchModeManager`;
Wreckoning is 2v2 and entirely delegated to `KnockoutManager` — the class
kept its original name since renaming a MonoBehaviour class risks breaking
the scene's script references (GUID-based) with no way to visually verify
the fix, so only the mode's *display name* changed — see its own bullet
below and `KnockoutManager.cs`/`KnockoutHud.cs`, the other three are
straight 1v1 with a goal, a timer, and — Deathmatch/CTF only — a `DeathZone`
sudden-death tiebreaker), **1v1 Arena** (untimed free play, enemy AI as
normal, no goal/HUD), **Test Arena** (the enemy becomes an inert `TestDummy`
— 100 HP, never moves or shoots, still explodes + runs the "Enemy respawns
in" countdown — for solo testing of movement / weapons / audio), or
**DeathZone Test** (debug: drops straight into sudden death against the live
AI, no goal/timer, for checking out the zone without grinding out a real
tie). It shows on Play and re-opens on **ESC / Android back** any time (time
+ audio frozen); you can switch modes on the fly, or Resume / ESC-again to
stay. When any of the five structured/debug modes ends (Wreckoning's whole
best-of-5 series, not each round), `MatchModeManager` plays a short cinematic
— a "VICTORY!"/"DEFEAT!"/"DRAW!" banner, every car currently in play locked
so it coasts to a natural stop, the camera easing back for a wider view, then
a 2.5-second beat of quiet (Wreckoning skips straight to the zoom the instant
the series is decided — no gap — since its own round-result banner already
held on screen for `postRoundDelay`) — before it reopens this menu itself.
There's no separate post-match/results/loading screen yet, so the mode-select
menu is standing in for that whole flow for now.

- **Deathmatch:** first team to `deathmatchKillGoal` (10) eliminations wins;
  `deathmatchDuration` (300s) timer. Nobody at the goal when it expires → more
  kills wins; tied on kills too → `DeathZone` sudden death.
- **Capture the Flag:** each team's `Flag` sits at its own base (built at
  runtime — pole + waving cloth + a translucent base-pad marking the capture
  radius, no art). Drive into the *enemy's* flag to pick it up — that cuts your
  `CarController.speedMultiplier` by `carrierSpeedPenalty` (35%) until you
  either bring it within `captureRadius` of your own base (scores a point, the
  flag snaps home with a `recaptureLockout` of 5s) or get killed — it then
  drops right where you died and sits there inert for `dropDuration` (6s),
  blinking faster and faster (`blinkStartInterval` 0.5s → `blinkEndInterval`
  0.08s between blinks) as that clock runs out, then teleports home on its own
  (no lockout). First team to `ctfCaptureGoal` (3) captures wins; `ctfDuration`
  (240s) timer. Nobody at the goal when it
  expires → more *eliminations* wins (kills are CTF's tiebreaker, not
  captures); tied on kills too → `DeathZone` sudden death.
- **Gridlock:** a single `ControlZone` sits at the arena centre — a glowing
  ground disc, code-generated (no art), neutral until a car drives in, then
  tints solid blue/red for whichever team is inside, or splits half-and-half
  when both are. Each team has its own independent 0-100%
  `gridlockSecondsPerPercent`-paced bar (1% per 0.7s while ≥1 of their cars is
  in the `radius` (6), paused — never reversed — the instant none of theirs
  are; both bars can climb at once, it's not contested/shared). First to 100%
  wins immediately. `gridlockDuration` (210s = 3.5 min) timer; nobody at 100%
  when it expires → higher percentage wins outright, and an *exact* tie is a
  flat draw — this mode never hands off to `DeathZone`.
- **Wreckoning:** 2v2 (`Wraith`+`AllyCar` vs `EnemyCar`+`EnemyCar2`, the extra
  two normally inactive and only switched on for this mode), run entirely by
  `KnockoutManager` on its own arena — **twice** the normal size (built once
  at runtime and swapped in for the normal `Plane`/walls/wall-`Cube`s *and*
  the normal `PlayerBase`/`EnemyBase` circles — all of it hidden for the
  duration rather than resized/moved) so a 2v2 has room to work with.
  Best-of-5 rounds, first to `roundsToWin` (3) wins the series;
  **no mid-round respawns** — `MatchDirector.RespawnsSuspended` is set for the
  whole series, so an eliminated car stays down (hidden/frozen, same visual
  Respawner already gives every death) until the round resets. A round is won
  the instant the whole opposing team is down; both teams' last car going
  down in the same instant (most likely the forcing zone's damage tick
  catching both at once — the round-end check is deferred a frame specifically
  to catch this) is a round **tie**. Nobody at `roundsToWin` after `maxRounds`
  (5) → higher round-win total wins, and an equal total (any mix of ties and
  even splits, "all five tied" included) makes the whole **series** a tie —
  `MatchModeManager.EndKnockoutSeries(...)` reuses the shared cinematic either
  way, kicking off the very instant the series is decided with no extra delay
  (this mode's only deviation from the shared post-match beat — see above).
  A round can't stall out: `KnockoutManager`'s own `DeathZone` (different
  tuning from Deathmatch's — see `DeathZone.cs`) starts closing `zoneStartDelay`
  (30s) into every round, its shrink *rate* escalating every 3s from 30% of
  Deathmatch's speed up to 120% (instead of Deathmatch's constant rate), and
  its damage stages are 1/5/10/12 dmg-per-sec (vs Deathmatch's 1/3/5/7) at the
  same 5s marks. Between rounds every car's health/position resets via
  `Respawner.Respawn()` at its team's base (`Respawner.SetSpawnOverride` points
  it at Wreckoning's bigger-arena bases instead of its normal scene spot for
  the series' duration) — the same drop-in animation as any other respawn, so
  round-start reads as "everyone drops back in." Each team also gets its own
  `BaseMarker` circle (one per team, not per car) at the midpoint of its two
  bases, sized so both cars land at/around its edge rather than needing to
  both fit inside it. Controls stay locked through a "Round starts in: 3, 2,
  1" countdown (`preRoundCountdown`, `ScreenFx.ShowCountdown`) before each
  round actually starts, which also resets the camera to follow the player's
  own car (`blueCar1`) in case last round ended mid-spectate. If the player's
  own car goes down before their teammate's, `KnockoutManager` switches
  `CameraFollow.target` to the teammate so the player keeps watching the
  round instead of staring at a wreck; it switches back to the player's own
  car at the very start of the next round (or on `Cleanup()`, defensively).
  UI: five round-result dots upper-middle (`KnockoutHud.SetDot`, blue/red/tie)
  and two roster squares per side, upper-left/right ("Blue 1"/"Blue 2"/"Red
  1"/"Red 2" — placeholders until an art pass), each getting a red **X**
  dropped onto it from above with a gentle settling bounce the instant that
  player's eliminated, cleared again on the next round's reset.
- **DeathZone sudden death (Deathmatch/CTF's `DeathZone` instance):** a
  circular safe zone shrinks from covering the whole arena to nothing at a
  constant `shrinkRateStages` (1.8 radius-units/sec, `startRadius` 36 ÷ 20s);
  anything caught outside it takes escalating damage per second (1 → 3 at 5s
  → 5 at 10s → 7 at 15s, holding at 7 after). First car to go down loses.
  Visual is a translucent, fiery orange-to-yellow annulus (code-generated
  mesh, no art) plus a ring of ember particles riding the shrinking edge —
  loosely modelled on battle-royale "storm" zones (Fortnite/PUBG/Apex),
  reskinned fiery/transparent for this arena rather than copying any one
  game's look. Wreckoning's forcing zone is a *second*, separately-tuned
  `DeathZone` instance living on `KnockoutManager` — see the Wreckoning
  bullet above — reusing the same mechanism/visual, not this one.

## Engine / setup

- **Unity 6000.3.23f1** (Unity 6.3). Open with Unity Hub; do not change the editor version without asking.
- **Render pipeline:** URP 17.3 (`Assets/Settings/` has separate `PC_*` and `Mobile_*` renderer + RP assets).
- **Input:** the new Input System package (1.20.0) is installed and `Assets/InputSystem_Actions.inputactions` exists, **but gameplay scripts still use the legacy `Input.*` API**. The player goes through `PlayerInputRouter` (an `IVehicleInput`): WASD + mouse-aim + hold-LMB-to-fire on PC/editor, the two on-screen `VirtualJoystick`s on a mobile build (`Scheme.Auto` decides). Project is mid-migration.
- **Unity MCP bridge:** `com.gamelovers.mcp-unity` is installed. With the Editor open and *Tools ▸ MCP Unity ▸ Server Window* running, Claude can read the hierarchy/console and edit the scene/components directly. `.mcp.json` path holds a PackageCache hash — re-run Configure if the server stops resolving.
- No test project, no CI, no build scripts yet.

## Layout

Scripts live flat in `Assets/` (not in a `Scripts/` subfolder yet).

| File | Attach to | Role |
|---|---|---|
| `IVehicleInput.cs` | — (interface) | `MoveInput` / `AimInput` (Vector2, twin-stick style). Anything implementing it on a vehicle drives that vehicle. |
| `CarController.cs` | a vehicle (needs `Rigidbody`) | Turns to face the input direction, accelerates forward. Input priority: `IVehicleInput` component → `movementJoystick` → `Input.GetAxis`. Exposes `CurrentSpeed` (accel/decel-smoothed forward speed) for `EngineAudio`. `speedMultiplier` (1 = normal, not serialized) scales `moveSpeed`; `Flag` sets it below 1 while this car carries an enemy flag. `inputLocked` (not serialized) forces zero input while still running `FixedUpdate`, so the car coasts to a stop under its own decel curve instead of freezing in place — `MatchModeManager` sets it after a match ends. `ResetSpeed()` zeroes the smoothed speed immediately, bypassing that decel curve — `Respawner` calls it on every respawn, otherwise a car that died mid-acceleration keeps its last speed frozen while disabled and visibly lurches forward under it the instant controls return, even though input was already locked (it's stale momentum driving the motion, not input). |
| `EngineAudio.cs` | a car root (needs `CarController`) | Loops one engine clip (the idle sound) and lerps its **pitch** + volume between `idlePitch`/`idleVolume` and `maxPitch`(1.7)/`maxVolume` by `load` = `CurrentSpeed / moveSpeed` — so the tone tracks the car's momentum, sweeping up as it accelerates and back down as it coasts to a stop (`responseSpeed` 6 adds a little smoothing). 2D on the `Player`-tagged car, 3D (linear rolloff, `spatialMaxDistance`) on others. Loop auto-loads from `Resources/Audio/Engine`. Disabled by `Respawner` on death. |
| `Weapon.cs` | a vehicle | Independent "turret" aim; auto-fires past `fireDeadzone`. Input priority: `IVehicleInput` → `aimJoystick` → `Fire1` (straight ahead). Stamps each spawned bullet with this vehicle's `Team`. Every bullet plays one discrete gunshot from `fireShots[]` (no spray loop) — a real .50-cal shot with a ~1.3s tail; random clip, never repeating back-to-back, small `firePitchJitter` / `fireVolumeJitter`, on a **5-voice** 2D `AudioSource` pool so consecutive shots' tails overlap and build the "wall" on a spray while a lone tap still rings out. Auto-loads from `Resources/Audio/Fire` if empty. `fireVolume` 0.5, `firePitch` (<1 = deeper). `inputLocked` (not serialized) silences firing without touching `enabled` — `Respawner` re-enables the component on landing regardless of who wants it held off, so callers needing it to actually stay quiet (a post-match cinematic, a Wreckoning round countdown) set this instead. |
| `PlayerInputRouter.cs` | `Wraith` (with `CarController` + `Weapon`) | The player's `IVehicleInput`. `Scheme.Auto` → WASD + mouse-aim + hold-LMB-fire on non-mobile, else the two `VirtualJoystick`s (refs auto-pulled from `CarController`/`Weapon` if left empty). Turret only re-aims while LMB is held. |
| `HealthBar.cs` | a car root with `Health` | Builds a billboarded world-space bar (two unlit quads) above the car **at runtime** — no scene setup, no art. Green→red by `HealthFraction`. `Hidden` (set by `Respawner`) toggles the bar off while dead. Cleans up its bar object in `OnDestroy`. |
| `Respawner.cs` | any car (needs `Health` + `Rigidbody`) | Sets `Health.destroyOnDeath = false`. On death: disables controls (`CarController`/`Weapon`/`EnemyDriverAI`/`PlayerInputRouter`/`EngineAudio`), collider, renderers, hides the health bar, freezes the body. `Respawn()` (called by `MatchDirector`, or `KnockoutManager` between rounds) revives (also calling `CarController.ResetSpeed()` — see that row — so a car that died moving doesn't lurch forward on landing), drops the car in from `dropHeight` (7) with `dropSpeed` (12) downward velocity; controls return the instant a downward raycast says it's within `landClearance` of a surface (`maxFallTime` is a safety cap) — no dead time on the ground. `SetSpawnOverride(pos, rot)`/`ClearSpawnOverride()` redirect `SpawnPosition`/`SpawnRotation` (and so where `Respawn()` drops the car) away from this car's own scene-authored spot — `KnockoutManager` uses it to drop cars at its bigger-arena bases instead, for the series' duration. |
| `MatchDirector.cs` | `GameDirector` (needs `ScreenFx`) | Subscribes to every `Health.Died` on a car with a `Respawner`. On death → (if enabled in `GameSettings`) camera `Shake` + `ScreenFx.Flash`, then counts `respawnSeconds`→1 on screen ("Respawning in:" / "Enemy respawns in:"), then `Respawner.Respawn()` + a small landing shake. Player's countdown owns the shared label. `CancelPendingRespawns()` stops any in-flight countdown/respawn dead and clears the shared-label flag — `MatchModeManager` calls it the instant a match ends, so a losing car can't pop back to life mid-cinematic. `RespawnsSuspended` goes further: while true, a death never starts a countdown/respawn at all (the shake/flash still plays) — `KnockoutManager` sets this for its whole series, since an eliminated car should just stay down until the round resets, not auto-revive 5s later. |
| `ScreenFx.cs` | `GameDirector` | Builds a screen-space overlay at runtime: full-screen `Flash()` quad + centred `ShowCountdown(label,n)` / `ShowMessage(text)` label (legacy `Text`, `LegacyRuntime.ttf`) + a persistent top-of-screen `SetHud(text)` line (`MatchModeManager`'s score/timer readout). |
| `ModeMenu.cs` | `GameDirector` (needs `MatchModeManager`) | Runtime uGUI overlay (same style as `ScreenFx`): title "WRAITH" + **Deathmatch** / **Capture the Flag** / **Gridlock** / **Wreckoning** / **1v1 Arena** / **Test Arena** / **DeathZone Test** / **Resume** buttons. Opens on `Awake` and on **ESC / Android back** (`Input.GetKeyDown(KeyCode.Escape)`), and can be reopened by `MatchModeManager` via `OpenMenu()`; each open freezes `Time.timeScale` + `AudioListener` and holds the enemy AI disabled. A pick calls `ApplyMode` (every mode but Test → `Detach` any `TestDummy` + enable the AI; Test → disable the AI + `AddComponent<TestDummy>()`) and `MatchModeManager.StartMatch(...)` (`None` for 1v1/Test). Persists (not destroyed) so it can re-open. Resume shows only once a mode is chosen. |
| `MatchModeManager.cs` | `GameDirector` (needs `ScreenFx` + `DeathZone`) | Runs Deathmatch / Capture the Flag / Gridlock / DeathZone Test directly: owns the kill/capture/zone-percent tallies, the match timer, the HUD text (via `ScreenFx.SetHud`), the goal/timer win check, and (Deathmatch/CTF only) handing off to `DeathZone` for sudden death when tied at the buzzer (or immediately, for the debug DeathZone Test mode). For Wreckoning (`Mode.Wreckoning` — the enum value was renamed along with the mode's display name; `KnockoutManager`/`EndKnockoutSeries` kept their original names, see that row) it just calls `KnockoutManager.BeginSeries()`/`Cleanup()` and otherwise gets out of the way (`Update()` returns early for that mode) — `EndKnockoutSeries(Team?)` is `KnockoutManager`'s one way back in, once its series concludes, called the instant the series is decided with no extra delay (Wreckoning's only deviation from the shared post-match beat below). Finds every currently-active `CarController`/`Weapon`/`EnemyDriverAI` into `allCars`/`allWeapons`/`allAI` on `StartMatch` (2 normally, Wreckoning's 4 once its extra cars are active — `FindCombatants()` runs again right after `BeginSeries()` activates them) plus the two `Flag`s and the `ControlZone` by type, wires the Flags to each other, and (de)activates the Flags/`ControlZone` per mode. On end: shows a "VICTORY!" / "DEFEAT!" (from the local player's POV) / "DRAW!" banner, tells `MatchDirector` to `CancelPendingRespawns()`, then runs the post-match sequence — `CarController.inputLocked` + `Weapon.inputLocked` on every car in `allCars`/`allWeapons` (they coast to a stop under their own decel curve, guns fall silent) and `CameraFollow.ZoomOut()`, waits for every one of them to actually settle and the camera to finish (`postMatchMaxWait` safety cap), a `postMatchMenuDelay` (2.5s) quiet beat, then unlocks + resets zoom and calls `ModeMenu.OpenMenu()` — standing in for a proper post-match/loading flow that doesn't exist yet. Also resets controls/zoom (and calls `KnockoutManager.Cleanup()`) at the *start* of every match, so a new one never inherits a locked/zoomed/Wreckoning-arena leftover state — and, on every mode switch (`ModeMenu` calls `StartMatch` for 1v1/Test too, as `Mode.None`), cancels any in-flight `MatchDirector` respawn countdown + clears the on-screen countdown text, clears `ScreenFx`'s persistent HUD line (so one mode's score/timer/percent never sits frozen through the next, since only some modes call `UpdateHud()` every frame), and — for every mode but Wreckoning, which does its own equivalent via its round-reset — sends every currently-active car through a full `Respawner.Respawn()` back to *this* mode's own scene-authored spawn point at full health (`ResetCombatantsToSpawn()`), so nobody carries over a leftover position/HP from whatever was just running (this is also what stops a car left sitting out on Wreckoning's doubled arena from falling into the void the instant a smaller-map mode's floor doesn't reach that far). |
| `DeathZone.cs` | `GameDirector` (Deathmatch/CTF's tuning) or any standalone object (Wreckoning's own, differently-tuned instance on `KnockoutManager`) | Sudden-death/forcing-zone hazard, idle until told to `BeginShrinking()`. Both the shrink speed and the damage use the same "stage" shape — a list of (elapsed-seconds, value) pairs, holding at the last one reached forever after — so a constant shrink (Deathmatch: one `shrinkRateStageTimes`/`shrinkRateStages` stage) and an escalating one (Wreckoning: several) are just different data on the same `StageValue()` lookup also used for `stageTimes`/`stageDamage`. See "Deathmatch"/"Wreckoning" above for each one's numbers. `Stop()` hides it and resets. All-code visual (annulus mesh with a per-vertex outer/edge color gradient + a ring of ember `ParticleSystem`s), same no-art approach as `DamageFx`/`HealthBar`. Not used by Gridlock. |
| `Flag.cs` | an empty object at each team's base (added by the CTF setup, same spot as that team's `BaseMarker`) | One team's flag. `owningTeam` says whose it is; only the *other* team can pick it up (`OnTriggerEnter`, needs `CarController`+`TeamMember`, blocked while `Respawner.IsDead`, a drop is in progress, or `recaptureLockout` is running). While carried it follows the carrier and sets `CarController.speedMultiplier`; reaching `captureRadius` of the other `Flag`'s home (found via `SetOther`, wired by `MatchModeManager`) fires `Captured` and snaps home with a `recaptureLockout`. A car dying calls `DropIfCarriedBy` (from `MatchModeManager`'s death handlers), which drops the flag right there — inert (not pickable) for `dropDuration`, its pole+cloth (`visual`) blinking at a rate that ramps from `blinkStartInterval` to `blinkEndInterval` — then auto-returns home with no lockout. `OnDisable`/`OnEnable` reset it (including any in-progress drop) cleanly when `MatchModeManager` toggles it off/on between modes. Only builds the pole+cloth now — the base-pad circle at its `homePos` is `BaseMarker`'s job, always there regardless of mode. |
| `BaseMarker.cs` | a standalone object at a team's base (`PlayerBase`/`EnemyBase` for the normal arena; two more per Wreckoning series, built by `KnockoutManager`) | Purely a visual: one flat translucent circle (`radius`, `color`), built once at `Awake` (no art, same flattened-cylinder trick `Flag`'s old base-pad used). No gameplay logic, always on regardless of mode — this is what makes "the blue/red circle" a universal base marker instead of a Capture-the-Flag-only thing. `PlayerBase`/`EnemyBase` are hidden (along with the rest of the normal arena) while Wreckoning's own, bigger set is active — see `KnockoutManager.cs` — so the small-arena circles don't show through on the doubled map. |
| `ControlZone.cs` | an empty object at the arena centre (added by the Gridlock setup) | Gridlock's capture point. Builds a glowing ground disc at runtime (two independently-tintable half-disc meshes, no art) and, every frame, checks each team's occupancy (`PlayerInside`/`EnemyInside`) via a simple in-`radius` distance check against every live, non-dead `TeamMember`. Neutral when empty, solid team colour when only one side is inside, split half-and-half when both are. Owns no scoring — `MatchModeManager` reads the occupancy flags to run each team's independent capture-percent bar. `OnEnable` resets occupancy/visual cleanly when toggled on for a new match. |
| `KnockoutManager.cs` | a standalone `KnockoutManager` object (needs its own `DeathZone` + `KnockoutHud`) | Runs Wreckoning end to end once `MatchModeManager.StartMatch(Wreckoning)` calls `BeginSeries()` (class/method names weren't renamed along with the mode's display name — see `MatchModeManager.cs` row). Auto-finds `blueCar1/2`/`redCar1/2` (`Wraith`/`AllyCar`/`EnemyCar`/`EnemyCar2`) by name if left empty (via `FindObjectsInactive.Include`, since the extra two start inactive) — activates them, points all four `Respawner`s at this mode's bases via `SetSpawnOverride`, suspends `MatchDirector`'s auto-respawn for the series, and grabs `Camera.main`'s `CameraFollow` for the spectate-teammate behaviour below. `OriginalArenaNames` (the normal arena pieces this mode hides/restores) includes `PlayerBase`/`EnemyBase` alongside the `Plane`/walls/`Cube`s, so the small-arena base circles don't show through on Wreckoning's doubled map. Owns the round loop (`RunSeries`/`RunRound` coroutines): reset (also re-pointing the camera at `blueCar1` in case last round ended mid-spectate) → cars locked through a `preRoundCountdown` (3) "Round starts in:" tick → unlock → wait for `EvaluateRoundEnd` (deferred a frame off each death via `ScheduleRoundCheck`, so a same-tick double-KO reads as a tie, not a win) → result banner → `postRoundDelay` → next round or `CheckSeriesDone` hands `MatchModeManager.EndKnockoutSeries` the result immediately, no extra wait. If the player's own car (`blueCar1`) goes down while the teammate (`blueCar2`) is still alive, `OnBlue1Died` switches `CameraFollow.target` to the teammate so the player can keep watching the round; `ResetAllCarsForRound` and `Cleanup()` both point it back at `blueCar1`. Also builds/toggles its own bigger arena, including a `BaseMarker` per team at the midpoint of its two bases (`BuildKnockoutArena`/`SetArenaActive`) — see "Wreckoning" above. `Cleanup()` (called by `MatchModeManager` at the start of *every* `StartMatch`, not just when leaving Wreckoning) tears all of this back down, and revives+un-overrides any car left dead mid-round so it doesn't carry a hidden/frozen state into whatever mode's picked next. |
| `KnockoutHud.cs` | the same object as `KnockoutManager` | Builds Wreckoning's UI at `Awake` (no art): five round-result dots upper-middle (`SetDot(i, DotResult)`) and two roster squares per side upper-left/right (`SetEliminated(slot, bool)` drops a red "X" onto slot 0-3 = Blue1/Blue2/Red1/Red2 from `crossDropHeight` above with an "ease-out-back" landing bounce — a gentle slam, not a scale-in pop). Hidden until `KnockoutManager` calls `SetVisible(true)`; `ResetAll()` blanks every dot and cross for a fresh series. |
| `TestDummy.cs` | added at runtime to an `EnemyCar` by `ModeMenu` | Turns the enemy into an inert practice target: every frame disables `EnemyDriverAI` / `CarController` / `Weapon` / `EngineAudio` (re-killing them after a `Respawner` respawn) and, while fully alive, pins the body at `Respawner.SpawnPosition`. `Health` / `Respawner` / `DamageFx` / `HealthBar` untouched, so it still blows up with the same FX and runs the "Enemy respawns in" 5s countdown. `Detach()` restores the controls + removes itself (used when switching back to 1v1). |
| `GameSettings.cs` | — (static) | `ScreenShakeOnElimination` / `ScreenFlashOnElimination` bools (default on) + `BulletHitVolume` float (raw AudioSource volume, default 0.23, clamped `0..BulletHitVolumeMax` = 0.4) — gameplay reads this; the menu binds `BulletHitVolumePercent` (0..100, 100 = max, ~57.5 default), slider labelled `BulletHitVolumeLabel` ("Projectile Impact Volume"). `PlayerPrefs`-backed. No settings-menu UI yet — these are the hooks a menu will flip. |
| `DamageFx.cs` | a car root with `Health` | Runtime particle FX (all code-generated, no art): smoke below `smokeBelow` (0.5) HP, flames below `fireBelow` (0.3) — `flameCount` tufts scattered at random spots over the car (`flameArea` half-extents), each a random size/rate; and on `Health.Died` an explosion burst + an expanding translucent-grey shockwave dome (`shockwaveRadius`/`Duration`/`Color`) + the `explosionSfx` clip (~3.6s, smooth baked-in fade-out) on a runtime-added 2D `AudioSource` on the car root (`explosionVolume` 1, `explosionPitchJitter` 0); `explosionSfx` auto-loads from `Resources/Audio/Explosion` if left empty. FX rig follows the car unparented but copies its rotation so the flames stay car-relative. |
| `KillFloor.cs` | `GameDirector` | Any car below `fallLimit` (fell off an open arena / clipped through) is damaged to death → respawns. Safety net now; the fall hazard later. |
| `EnemyDriverAI.cs` | any AI-driven car (with `CarController` + `Weapon` + `Health`) — `EnemyCar`/`EnemyCar2`/`AllyCar` today | Implements `IVehicleInput`. Despite the name it drives **any** side — an ally bot uses this exact script with `TeamMember.team = Player`. Targeting is team-based: `target` is always the nearest living car on the *opposing* team (found via `TeamMember`, not a tag), re-picked whenever the current one stops being valid (dead, gone, or — Wreckoning — permanently down for the round) rather than waiting on a respawn that isn't coming; this is what makes 1v1 scale to 2v2 with no special-casing. Never charges its target: holds a stand-off distance and circles, flipping orbit direction at random intervals, adding Perlin wander, sidestepping incoming bullets, steering around obstacles (3 forward feelers). Dodge is deliberately fallible: per-bullet `dodgeChance` roll + `reactionDelay` before it acts + short `dodgeScanRadius`. Stances: `Pressing` (health > `evadeBelowHealth`, orbit `pressDistance`) / `Evasive` (hurt, orbit `evadeDistance`, twitchier). While there's no live target (whole opposing team down or not found) it **roams** random points around `roamCenter` (holding fire) rather than idling. |
| `Projectile.cs` | `bullet.prefab` | Flies forward (`speed 40`, `damage 4`); on `OnTriggerEnter` passes through same-`team` (incl. shooter), else `Health.TakeDamage` + destroy. `team` set by the firing `Weapon`. When it damages the **`Player`-tagged** car it plays a round-robin `playerHitSfx` clip (2D, on a throwaway object so it outlives the bullet) — metal-on-metal for bullets, only the player hears their own hits; other projectile types set their own clips. Auto-loads from `Resources/Audio/BulletImpact` if empty. Loudness = `GameSettings.BulletHitVolume` (0 skips it entirely), not a field. Prefab visual: thin stretched cube (`scale 0.09×0.09×0.6`) with `Assets/Materials/Bullet.mat` (yellow URP Unlit) — a tracer round. Hitbox is a `SphereCollider` (trigger). |
| `Health.cs` | anything damageable | `maxHealth`, `CurrentHealth`, `HealthFraction`, `IsDead`, `TakeDamage`, `Revive()`. `Died` (C# `event Action<Health>`) + `onDeath` (`UnityEvent`) fire in `Die()`; `Destroy`s only if `destroyOnDeath` (a `Respawner` clears that). |
| `TeamMember.cs` | a vehicle root | `enum Team { Player, Enemy }` + one field. No `TeamMember` = neutral (destructible props) — hittable by anyone. |
| `CameraFollow.cs` | Main Camera | Smoothed chase cam; `target` = player. `offset` `(0,28,-36)` — pulled well back, ~38° down: small car, wide view. Camera FOV 60. `Shake(duration, magnitude)` for kill/landing juice. `ZoomOut(duration)` smoothly scales the whole offset up to `zoomOutMultiplier` (pulls back and up together, no FOV change) — `IsZooming` reports mid-transition; `MatchModeManager` calls it when a match ends and `ResetZoomImmediate()` at the start of every new one. |
| `OffscreenMarkers.cs` | `GameDirector` | Screen-edge arrow for every combatant that's off-camera (any edge, incl. behind), pointing at it. Uses `WorldToScreenPoint` + `RectTransformUtility`. Colour is relative to the local player (tag `Player`): other team → red, same team (ally/teammate) → blue. Runtime overlay canvas + pooled code-drawn arrows with an `Outline`. |
| `VirtualJoystick.cs` | a UI Image (bg) with a child handle Image | Touch stick; exposes `InputVector` (-1..1 per axis). Two instances: move + aim. |
| `JoystickSkin.cs` | a joystick `bg` object (with `VirtualJoystick` + `Image`) | Reskins the bg + handle Images at runtime as translucent circles with a code-drawn glyph (`glyph`: `DirectionalArrows` on the move stick, `Bullet` on the fire stick). `backgroundOpacity` / `handleOpacity` / glyph opacities tune the look. |

- **Scene:** `Assets/Scenes/SampleScene.unity` (the only scene).
  - `Wraith` — the player's car; tag `Player`, layer `Player`; starts at `(0, 0.5, -20)` (moved here from the arena centre so it lines up with `PlayerBase`/`PlayerFlag` — every mode's blue base, not just CTF's); `CarController` + `Weapon` (still hold the two `VirtualJoystick` refs) + `Health(100)` + `TeamMember(Player)` + `PlayerInputRouter` + `HealthBar` + `Respawner` + `DamageFx` + `EngineAudio`; child `FirePoint`.
  - `EnemyCar` — red material, `Untagged`, starts at `(0, 0.5, 20)` (matches `EnemyBase`/`EnemyFlag`); same base components as the Wraith but joystick refs cleared, plus `EnemyDriverAI` + `Health(100)` + `TeamMember(Enemy)` + `HealthBar` + `Respawner` + `DamageFx` + `EngineAudio`; child `FirePoint`. In **Test Arena** mode `ModeMenu` adds a `TestDummy` here.
  - `PlayerBase` (`(0, 0.5, -20)`, blue) / `EnemyBase` (`(0, 0.5, 20)`, red) — always-on `BaseMarker` circles (`radius` 3) showing each team's start, in every mode, not just Capture the Flag (whose own `Flag` base-pad this replaced — see `Flag.cs`).
  - `AllyCar` / `EnemyCar2` — Wreckoning's extra two, exact component-for-component duplicates of `EnemyCar` (so `EnemyDriverAI` behaves identically), **inactive by default** and only switched on by `KnockoutManager.BeginSeries()`. `AllyCar` has `TeamMember(Player)` + `Assets/Materials/AllyCar.mat` (blue, so it reads as a teammate at a glance); `EnemyCar2` is left `TeamMember(Enemy)` with `EnemyCar`'s own red material. Scene position doesn't matter for either — `KnockoutManager` overrides both their `Respawner` spawn points to its own bases the moment it activates them.
  - `GameDirector` — empty; `MatchDirector` + `ScreenFx` + `KillFloor` + `OffscreenMarkers` + `ModeMenu` + `MatchModeManager` + `DeathZone` (Deathmatch/CTF's tuning).
  - `PlayerFlag` (`owningTeam = Player`, at the Wraith's spawn, same spot as `PlayerBase`) / `EnemyFlag` (`owningTeam = Enemy`, at the EnemyCar's spawn, same spot as `EnemyBase`) — Capture the Flag's two flags; `Flag` builds its own pole/cloth visual at runtime (the base-pad circle at the same spot is `BaseMarker`'s, always there regardless of mode). Active only while Capture the Flag is the running mode (`MatchModeManager` toggles them).
  - `ControlZone` at the arena centre `(0, 0, 0)` — Gridlock's single capture point; `ControlZone` builds its own glowing ground-disc visual at runtime. Active only while Gridlock is the running mode.
  - `KnockoutManager` — empty; `KnockoutManager` + its own `DeathZone` (Wreckoning's tuning: `startRadius` 72 / `outerRadius` 90 to cover its doubled arena, `shrinkRateStageTimes/Stages` `[0,3,6,9]`→`[0.54,1.08,1.62,2.16]`, `stageTimes/Damage` `[0,5,10,15]`→`[1,5,10,12]`) + `KnockoutHud`. Also builds its own bigger `Plane`/walls at runtime (see `KnockoutManager.cs`), all inactive until Wreckoning is picked.
  - `Arena_Wall_N/S/E/W` — invisible BoxCollider boundary walls at the Plane edges (±25.5), so cars can't drive off. Hidden (not resized) while Wreckoning runs its own, bigger set instead — see `KnockoutManager.cs`.
  - `Canvas/movejoystick bg` + `aimjoystick bg` — the two `VirtualJoystick`s, each also carrying a `JoystickSkin` (arrows / bullet glyph).
  - Four `Cube`s at `(0,1,±25)` / `(±25,1,0)`, scaled long and thin (`50×2×1` / `1×2×50`) — the *visible* wall panels sitting right on top of the invisible `Arena_Wall_N/S/E/W` colliders, not obstacles in the middle of the play field (the docs used to describe these as scattered mid-arena obstacles — that's stale; the centre of the arena is clear, which is exactly where `ControlZone` now sits). Also hidden while Wreckoning runs.
  - `Plane` is 50×50 world units centred on origin (playable area ≈ x/z ∈ [-25, 25]). Square for now; non-Wreckoning arenas will become rectangular later. Wreckoning's own is 100×100 (see above).
- **Physics layers:** every car is on `Player` (3), bullets on `Projectiles` (6). The Layer Collision Matrix is left fully enabled — `Projectile.cs` filters friendly/self hits by `Team` in code, so don't disable Projectiles↔anything or bullets stop registering.
- `Assets/Materials/` — runtime materials (`EnemyCar.mat`, `AllyCar.mat`).
- `Assets/Resources/Audio/` — SFX loaded by name at runtime. `Fire/` = machine gun (`mgshot_1..4`, pitch variants of one real .50-cal shot — "072807 Heavy Machine Gun .50 Caliber" from Pixabay, **Pixabay Content License**: free commercial use, no attribution, don't resell the raw files). `Explosion/` = elimination (`explosion.ogg`, Pixabay "Explosion FX" — mono, +30% gain, smooth exponential fade-out tail, ~3.6s). `BulletImpact/` = `impact_1..3.ogg`, metal-on-metal, played by `Projectile` only when the **player** is hit. `Engine/` = `engine_loop.ogg` (Pixabay "Car engine idle") — `EngineAudio` loops it and pitch-shifts it up with speed for accel / max-RPM. See `CREDITS.txt` for exact terms. `Weapon` / `DamageFx` / `Projectile` / `EngineAudio` `Resources.LoadAll` these when their clip fields are empty; assign clips on the components to override.
- `Assets/TutorialInfo/` and `Assets/Readme.asset` are leftover URP-template content — safe to ignore or delete.

## Conventions

- Each gameplay script has a top-of-file `// ATTACH THIS TO:` comment. **Keep it updated** when behaviour changes.
- Tunables are `public` fields grouped under `[Header("...")]` so they're editable in the Inspector. Prefer this over hard-coded constants.
- **Shared vehicle code:** player and AI use the *same* `CarController` / `Weapon`. New per-vehicle behaviour goes through an `IVehicleInput` provider, not a forked controller — this is how "same stats" stays true.
- Plain `MonoBehaviour` + `Instantiate`/`Destroy`. No object pooling, no ScriptableObjects, no DI, no assembly definitions yet — don't introduce these without discussing.
- 3D game: movement is on the XZ plane, `Vector3(input.x, 0, input.y)`.

## Working agreements

- **Version control:** git repo initialised. Commit working checkpoints; the user relies on this for undo.
- The user is newer to Unity — when a change needs manual Editor steps that the MCP bridge can't do (or the bridge is offline), spell them out step by step.
- Claude can drive the Editor via the MCP bridge but **cannot see the Game view** — after gameplay changes, ask the user to enter Play mode and report what they see.

## Not yet built

Enemy spawning/waves, audio mixer / master volume settings / more SFX (machine-gun fire, elimination explosion, player-hit impact, and a dynamic engine — idle+rev — exist in `Assets/Resources/Audio/`), muzzle flash, per-hit feedback, **settings menu UI** (hooks exist: `GameSettings.ScreenShake/FlashOnElimination` toggles + `BulletHitVolumePercent`, a 0..100 slider), main menu, a real post-match/results/loading screen (`MatchModeManager`'s end-of-match cinematic currently reopens the mode-select `ModeMenu` as a stand-in), line-of-sight checks for the AI (it currently shoots through walls; obstacle *avoidance* exists), per-vehicle stat presets, rectangular arenas, real player portraits/art for Wreckoning's roster squares (currently text-only placeholders), teams bigger than 2v2.
