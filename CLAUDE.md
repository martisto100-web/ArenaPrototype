# ArenaPrototype

A twin-stick **vehicle arena shooter** for mobile (iOS/Android). Early prototype.
The player's car is the **Wraith** (a sports car; scene object `Wraith`, tag `Player`).

`ModeMenu` offers six modes: **Deathmatch**, **Capture the Flag**, and
**Gridlock** (all three run through `MatchModeManager`, structured matches with
a goal, a timer, and — for the first two — a `DeathZone` sudden-death
tiebreaker), **1v1 Arena** (untimed free play, enemy AI as normal, no
goal/HUD), **Test Arena** (the enemy becomes an inert `TestDummy` — 100 HP,
never moves or shoots, still explodes + runs the "Enemy respawns in" countdown
— for solo testing of movement / weapons / audio), or **DeathZone Test**
(debug: drops straight into sudden death against the live AI, no goal/timer,
for checking out the zone without grinding out a real tie). It shows on Play
and re-opens on **ESC / Android back** any time (time + audio frozen); you can
switch modes on the fly, or Resume / ESC-again to stay. When any of the four
structured/debug modes ends, `MatchModeManager` plays a short cinematic — a
"VICTORY!"/"DEFEAT!"/"DRAW!" banner, both cars' controls locked so they coast
to a natural stop, the camera easing back for a wider view, then a one-second
beat of quiet — before it reopens this menu itself. There's no separate
post-match/results/loading screen yet, so the mode-select menu is standing in
for that whole flow for now.

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
- **DeathZone sudden death:** a circular safe zone shrinks from covering the
  whole arena to nothing over `shrinkDuration` (20s); anything caught outside
  it takes escalating damage per second (1 → 3 at 5s → 5 at 10s → 7 at 15s,
  holding at 7 after). First car to go down loses. Visual is a translucent,
  fiery orange-to-yellow annulus (code-generated mesh, no art) plus a ring of
  ember particles riding the shrinking edge — loosely modelled on
  battle-royale "storm" zones (Fortnite/PUBG/Apex), reskinned fiery/transparent
  for this arena rather than copying any one game's look.

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
| `CarController.cs` | a vehicle (needs `Rigidbody`) | Turns to face the input direction, accelerates forward. Input priority: `IVehicleInput` component → `movementJoystick` → `Input.GetAxis`. Exposes `CurrentSpeed` (accel/decel-smoothed forward speed) for `EngineAudio`. `speedMultiplier` (1 = normal, not serialized) scales `moveSpeed`; `Flag` sets it below 1 while this car carries an enemy flag. `inputLocked` (not serialized) forces zero input while still running `FixedUpdate`, so the car coasts to a stop under its own decel curve instead of freezing in place — `MatchModeManager` sets it after a match ends. |
| `EngineAudio.cs` | a car root (needs `CarController`) | Loops one engine clip (the idle sound) and lerps its **pitch** + volume between `idlePitch`/`idleVolume` and `maxPitch`(1.7)/`maxVolume` by `load` = `CurrentSpeed / moveSpeed` — so the tone tracks the car's momentum, sweeping up as it accelerates and back down as it coasts to a stop (`responseSpeed` 6 adds a little smoothing). 2D on the `Player`-tagged car, 3D (linear rolloff, `spatialMaxDistance`) on others. Loop auto-loads from `Resources/Audio/Engine`. Disabled by `Respawner` on death. |
| `Weapon.cs` | a vehicle | Independent "turret" aim; auto-fires past `fireDeadzone`. Input priority: `IVehicleInput` → `aimJoystick` → `Fire1` (straight ahead). Stamps each spawned bullet with this vehicle's `Team`. Every bullet plays one discrete gunshot from `fireShots[]` (no spray loop) — a real .50-cal shot with a ~1.3s tail; random clip, never repeating back-to-back, small `firePitchJitter` / `fireVolumeJitter`, on a **5-voice** 2D `AudioSource` pool so consecutive shots' tails overlap and build the "wall" on a spray while a lone tap still rings out. Auto-loads from `Resources/Audio/Fire` if empty. `fireVolume` 0.5, `firePitch` (<1 = deeper). |
| `PlayerInputRouter.cs` | `Wraith` (with `CarController` + `Weapon`) | The player's `IVehicleInput`. `Scheme.Auto` → WASD + mouse-aim + hold-LMB-fire on non-mobile, else the two `VirtualJoystick`s (refs auto-pulled from `CarController`/`Weapon` if left empty). Turret only re-aims while LMB is held. |
| `HealthBar.cs` | a car root with `Health` | Builds a billboarded world-space bar (two unlit quads) above the car **at runtime** — no scene setup, no art. Green→red by `HealthFraction`. `Hidden` (set by `Respawner`) toggles the bar off while dead. Cleans up its bar object in `OnDestroy`. |
| `Respawner.cs` | `Wraith` / `EnemyCar` (needs `Health` + `Rigidbody`) | Sets `Health.destroyOnDeath = false`. On death: disables controls (`CarController`/`Weapon`/`EnemyDriverAI`/`PlayerInputRouter`/`EngineAudio`), collider, renderers, hides the health bar, freezes the body. `Respawn()` (called by `MatchDirector`) revives, drops the car in from `dropHeight` (7) with `dropSpeed` (12) downward velocity; controls return the instant a downward raycast says it's within `landClearance` of a surface (`maxFallTime` is a safety cap) — no dead time on the ground. |
| `MatchDirector.cs` | `GameDirector` (needs `ScreenFx`) | Subscribes to every `Health.Died` on a car with a `Respawner`. On death → (if enabled in `GameSettings`) camera `Shake` + `ScreenFx.Flash`, then counts `respawnSeconds`→1 on screen ("Respawning in:" / "Enemy respawns in:"), then `Respawner.Respawn()` + a small landing shake. Player's countdown owns the shared label. `CancelPendingRespawns()` stops any in-flight countdown/respawn dead and clears the shared-label flag — `MatchModeManager` calls it the instant a match ends, so a losing car can't pop back to life mid-cinematic. |
| `ScreenFx.cs` | `GameDirector` | Builds a screen-space overlay at runtime: full-screen `Flash()` quad + centred `ShowCountdown(label,n)` / `ShowMessage(text)` label (legacy `Text`, `LegacyRuntime.ttf`) + a persistent top-of-screen `SetHud(text)` line (`MatchModeManager`'s score/timer readout). |
| `ModeMenu.cs` | `GameDirector` (needs `MatchModeManager`) | Runtime uGUI overlay (same style as `ScreenFx`): title "WRAITH" + **Deathmatch** / **Capture the Flag** / **Gridlock** / **1v1 Arena** / **Test Arena** / **DeathZone Test** / **Resume** buttons. Opens on `Awake` and on **ESC / Android back** (`Input.GetKeyDown(KeyCode.Escape)`), and can be reopened by `MatchModeManager` via `OpenMenu()`; each open freezes `Time.timeScale` + `AudioListener` and holds the enemy AI disabled. A pick calls `ApplyMode` (every mode but Test → `Detach` any `TestDummy` + enable the AI; Test → disable the AI + `AddComponent<TestDummy>()`) and `MatchModeManager.StartMatch(...)` (`None` for 1v1/Test). Persists (not destroyed) so it can re-open. Resume shows only once a mode is chosen. |
| `MatchModeManager.cs` | `GameDirector` (needs `ScreenFx` + `DeathZone`) | Runs Deathmatch / Capture the Flag / Gridlock / DeathZone Test: owns the kill/capture/zone-percent tallies, the match timer, the HUD text (via `ScreenFx.SetHud`), the goal/timer win check, and (Deathmatch/CTF only) handing off to `DeathZone` for sudden death when tied at the buzzer (or immediately, for the debug DeathZone Test mode). Finds the two cars (+ their `CarController`/`Weapon`/`EnemyDriverAI`), two `Flag`s, and the `ControlZone` by type on `StartMatch`, wires the Flags to each other, and (de)activates the Flags/`ControlZone` per mode. On end: shows a "VICTORY!" / "DEFEAT!" (from the local player's POV) / "DRAW!" banner, tells `MatchDirector` to `CancelPendingRespawns()`, then runs the post-match sequence — `CarController.inputLocked` + `Weapon.enabled=false` on both cars (they coast to a stop under their own decel curve, guns fall silent) and `CameraFollow.ZoomOut()`, waits for both cars to actually settle and the camera to finish (`postMatchMaxWait` safety cap), a `postMatchMenuDelay` (1s) quiet beat, then unlocks + resets zoom and calls `ModeMenu.OpenMenu()` — standing in for a proper post-match/loading flow that doesn't exist yet. Also resets controls/zoom at the *start* of every match, so a new one never inherits a locked/zoomed leftover state. Doesn't touch respawn logic otherwise — `MatchDirector`/`Respawner` keep doing that regardless of mode. |
| `DeathZone.cs` | `GameDirector` | Sudden-death hazard, idle until `MatchModeManager` calls `BeginShrinking()`. See "Deathmatch" section above for the shrink/damage curve. `Stop()` hides it and resets. All-code visual (annulus mesh with a per-vertex outer/edge color gradient + a ring of ember `ParticleSystem`s), same no-art approach as `DamageFx`/`HealthBar`. Not used by Gridlock. |
| `Flag.cs` | an empty base object per team (added by the CTF setup) | One team's flag + base. `owningTeam` says whose base it is; only the *other* team can pick it up (`OnTriggerEnter`, needs `CarController`+`TeamMember`, blocked while `Respawner.IsDead`, a drop is in progress, or `recaptureLockout` is running). While carried it follows the carrier and sets `CarController.speedMultiplier`; reaching `captureRadius` of the other `Flag`'s home (found via `SetOther`, wired by `MatchModeManager`) fires `Captured` and snaps home with a `recaptureLockout`. A car dying calls `DropIfCarriedBy` (from `MatchModeManager`'s death handlers), which drops the flag right there — inert (not pickable) for `dropDuration`, its pole+cloth (`visual`) blinking at a rate that ramps from `blinkStartInterval` to `blinkEndInterval` — then auto-returns home with no lockout. `OnDisable`/`OnEnable` reset it (including any in-progress drop) cleanly when `MatchModeManager` toggles it off/on between modes. |
| `ControlZone.cs` | an empty object at the arena centre (added by the Gridlock setup) | Gridlock's capture point. Builds a glowing ground disc at runtime (two independently-tintable half-disc meshes, no art) and, every frame, checks each team's occupancy (`PlayerInside`/`EnemyInside`) via a simple in-`radius` distance check against every live, non-dead `TeamMember`. Neutral when empty, solid team colour when only one side is inside, split half-and-half when both are. Owns no scoring — `MatchModeManager` reads the occupancy flags to run each team's independent capture-percent bar. `OnEnable` resets occupancy/visual cleanly when toggled on for a new match. |
| `TestDummy.cs` | added at runtime to an `EnemyCar` by `ModeMenu` | Turns the enemy into an inert practice target: every frame disables `EnemyDriverAI` / `CarController` / `Weapon` / `EngineAudio` (re-killing them after a `Respawner` respawn) and, while fully alive, pins the body at `Respawner.SpawnPosition`. `Health` / `Respawner` / `DamageFx` / `HealthBar` untouched, so it still blows up with the same FX and runs the "Enemy respawns in" 5s countdown. `Detach()` restores the controls + removes itself (used when switching back to 1v1). |
| `GameSettings.cs` | — (static) | `ScreenShakeOnElimination` / `ScreenFlashOnElimination` bools (default on) + `BulletHitVolume` float (raw AudioSource volume, default 0.23, clamped `0..BulletHitVolumeMax` = 0.4) — gameplay reads this; the menu binds `BulletHitVolumePercent` (0..100, 100 = max, ~57.5 default), slider labelled `BulletHitVolumeLabel` ("Projectile Impact Volume"). `PlayerPrefs`-backed. No settings-menu UI yet — these are the hooks a menu will flip. |
| `DamageFx.cs` | a car root with `Health` | Runtime particle FX (all code-generated, no art): smoke below `smokeBelow` (0.5) HP, flames below `fireBelow` (0.3) — `flameCount` tufts scattered at random spots over the car (`flameArea` half-extents), each a random size/rate; and on `Health.Died` an explosion burst + an expanding translucent-grey shockwave dome (`shockwaveRadius`/`Duration`/`Color`) + the `explosionSfx` clip (~3.6s, smooth baked-in fade-out) on a runtime-added 2D `AudioSource` on the car root (`explosionVolume` 1, `explosionPitchJitter` 0); `explosionSfx` auto-loads from `Resources/Audio/Explosion` if left empty. FX rig follows the car unparented but copies its rotation so the flames stay car-relative. |
| `KillFloor.cs` | `GameDirector` | Any car below `fallLimit` (fell off an open arena / clipped through) is damaged to death → respawns. Safety net now; the fall hazard later. |
| `EnemyDriverAI.cs` | `EnemyCar` (with `CarController` + `Weapon` + `Health`) | Implements `IVehicleInput`. Never charges: holds a stand-off distance and circles the player, flipping orbit direction at random intervals, adding Perlin wander, sidestepping incoming player bullets, steering around obstacles (3 forward feelers). Dodge is deliberately fallible: per-bullet `dodgeChance` roll + `reactionDelay` before it acts + short `dodgeScanRadius`. Stances: `Pressing` (health > `evadeBelowHealth`, orbit `pressDistance`) / `Evasive` (hurt, orbit `evadeDistance`, twitchier). Finds the player by tag `Player`. While there's no live target (player dead / not found) it **roams** random points around `roamCenter` (holding fire) rather than idling. |
| `Projectile.cs` | `bullet.prefab` | Flies forward (`speed 40`, `damage 4`); on `OnTriggerEnter` passes through same-`team` (incl. shooter), else `Health.TakeDamage` + destroy. `team` set by the firing `Weapon`. When it damages the **`Player`-tagged** car it plays a round-robin `playerHitSfx` clip (2D, on a throwaway object so it outlives the bullet) — metal-on-metal for bullets, only the player hears their own hits; other projectile types set their own clips. Auto-loads from `Resources/Audio/BulletImpact` if empty. Loudness = `GameSettings.BulletHitVolume` (0 skips it entirely), not a field. Prefab visual: thin stretched cube (`scale 0.09×0.09×0.6`) with `Assets/Materials/Bullet.mat` (yellow URP Unlit) — a tracer round. Hitbox is a `SphereCollider` (trigger). |
| `Health.cs` | anything damageable | `maxHealth`, `CurrentHealth`, `HealthFraction`, `IsDead`, `TakeDamage`, `Revive()`. `Died` (C# `event Action<Health>`) + `onDeath` (`UnityEvent`) fire in `Die()`; `Destroy`s only if `destroyOnDeath` (a `Respawner` clears that). |
| `TeamMember.cs` | a vehicle root | `enum Team { Player, Enemy }` + one field. No `TeamMember` = neutral (destructible props) — hittable by anyone. |
| `CameraFollow.cs` | Main Camera | Smoothed chase cam; `target` = player. `offset` `(0,28,-36)` — pulled well back, ~38° down: small car, wide view. Camera FOV 60. `Shake(duration, magnitude)` for kill/landing juice. `ZoomOut(duration)` smoothly scales the whole offset up to `zoomOutMultiplier` (pulls back and up together, no FOV change) — `IsZooming` reports mid-transition; `MatchModeManager` calls it when a match ends and `ResetZoomImmediate()` at the start of every new one. |
| `OffscreenMarkers.cs` | `GameDirector` | Screen-edge arrow for every combatant that's off-camera (any edge, incl. behind), pointing at it. Uses `WorldToScreenPoint` + `RectTransformUtility`. Colour is relative to the local player (tag `Player`): other team → red, same team → yellow. Runtime overlay canvas + pooled code-drawn arrows with an `Outline`. |
| `VirtualJoystick.cs` | a UI Image (bg) with a child handle Image | Touch stick; exposes `InputVector` (-1..1 per axis). Two instances: move + aim. |
| `JoystickSkin.cs` | a joystick `bg` object (with `VirtualJoystick` + `Image`) | Reskins the bg + handle Images at runtime as translucent circles with a code-drawn glyph (`glyph`: `DirectionalArrows` on the move stick, `Bullet` on the fire stick). `backgroundOpacity` / `handleOpacity` / glyph opacities tune the look. |

- **Scene:** `Assets/Scenes/SampleScene.unity` (the only scene).
  - `Wraith` — the player's car; tag `Player`, layer `Player`; `CarController` + `Weapon` (still hold the two `VirtualJoystick` refs) + `Health(100)` + `TeamMember(Player)` + `PlayerInputRouter` + `HealthBar` + `Respawner` + `DamageFx` + `EngineAudio`; child `FirePoint`.
  - `EnemyCar` — red material, `Untagged`, starts at `(0, 0.5, 20)`; same base components as the Wraith but joystick refs cleared, plus `EnemyDriverAI` + `Health(100)` + `TeamMember(Enemy)` + `HealthBar` + `Respawner` + `DamageFx` + `EngineAudio`; child `FirePoint`. In **Test Arena** mode `ModeMenu` adds a `TestDummy` here.
  - `GameDirector` — empty; `MatchDirector` + `ScreenFx` + `KillFloor` + `OffscreenMarkers` + `ModeMenu` + `MatchModeManager` + `DeathZone`.
  - `PlayerFlag` (`owningTeam = Player`, near the Wraith's spawn) / `EnemyFlag` (`owningTeam = Enemy`, near the EnemyCar's spawn) — Capture the Flag's two bases; `Flag` builds its own pole/cloth/base-pad visual at runtime. Active only while Capture the Flag is the running mode (`MatchModeManager` toggles them).
  - `ControlZone` at the arena centre `(0, 0, 0)` — Gridlock's single capture point; `ControlZone` builds its own glowing ground-disc visual at runtime. Active only while Gridlock is the running mode.
  - `Arena_Wall_N/S/E/W` — invisible BoxCollider boundary walls at the Plane edges (±25.5), so cars can't drive off. Some future arenas will omit these (falling = a hazard, caught by `KillFloor`).
  - `Canvas/movejoystick bg` + `aimjoystick bg` — the two `VirtualJoystick`s, each also carrying a `JoystickSkin` (arrows / bullet glyph).
  - Four `Cube`s at `(0,1,±25)` / `(±25,1,0)`, scaled long and thin (`50×2×1` / `1×2×50`) — the *visible* wall panels sitting right on top of the invisible `Arena_Wall_N/S/E/W` colliders, not obstacles in the middle of the play field (the docs used to describe these as scattered mid-arena obstacles — that's stale; the centre of the arena is clear, which is exactly where `ControlZone` now sits).
  - `Plane` is 50×50 world units centred on origin (playable area ≈ x/z ∈ [-25, 25]). Square for now; arenas will become rectangular later.
- **Physics layers:** both cars are on `Player` (3), bullets on `Projectiles` (6). The Layer Collision Matrix is left fully enabled — `Projectile.cs` filters friendly/self hits by `Team` in code, so don't disable Projectiles↔anything or bullets stop registering.
- `Assets/Materials/` — runtime materials (`EnemyCar.mat`).
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

Enemy spawning/waves, audio mixer / master volume settings / more SFX (machine-gun fire, elimination explosion, player-hit impact, and a dynamic engine — idle+rev — exist in `Assets/Resources/Audio/`), muzzle flash, per-hit feedback, **settings menu UI** (hooks exist: `GameSettings.ScreenShake/FlashOnElimination` toggles + `BulletHitVolumePercent`, a 0..100 slider), main menu, a real post-match/results/loading screen (`MatchModeManager`'s end-of-match cinematic currently reopens the mode-select `ModeMenu` as a stand-in), line-of-sight checks for the AI (it currently shoots through walls; obstacle *avoidance* exists), per-vehicle stat presets, rectangular arenas.
