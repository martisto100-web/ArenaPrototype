# ArenaPrototype

A twin-stick **vehicle arena shooter** for mobile (iOS/Android). Early prototype.

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
| `CarController.cs` | a vehicle (needs `Rigidbody`) | Turns to face the input direction, accelerates forward. Input priority: `IVehicleInput` component → `movementJoystick` → `Input.GetAxis`. |
| `Weapon.cs` | a vehicle | Independent "turret" aim; auto-fires past `fireDeadzone`. Input priority: `IVehicleInput` → `aimJoystick` → `Fire1` (straight ahead). Stamps each spawned bullet with this vehicle's `Team`. |
| `PlayerInputRouter.cs` | `PlayerCar` (with `CarController` + `Weapon`) | The player's `IVehicleInput`. `Scheme.Auto` → WASD + mouse-aim + hold-LMB-fire on non-mobile, else the two `VirtualJoystick`s (refs auto-pulled from `CarController`/`Weapon` if left empty). Turret only re-aims while LMB is held. |
| `HealthBar.cs` | a car root with `Health` | Builds a billboarded world-space bar (two unlit quads) above the car **at runtime** — no scene setup, no art. Green→red by `HealthFraction`. `Hidden` (set by `Respawner`) toggles the bar off while dead. Cleans up its bar object in `OnDestroy`. |
| `Respawner.cs` | `PlayerCar` / `EnemyCar` (needs `Health` + `Rigidbody`) | Sets `Health.destroyOnDeath = false`. On death: disables controls/AI/collider/renderers, hides the health bar, freezes the body. `Respawn()` (called by `MatchDirector`) revives, drops the car in from `dropHeight` (7) with `dropSpeed` (12) downward velocity; controls return the instant a downward raycast says it's within `landClearance` of a surface (`maxFallTime` is a safety cap) — no dead time on the ground. |
| `MatchDirector.cs` | `GameDirector` (needs `ScreenFx`) | Subscribes to every `Health.Died` on a car with a `Respawner`. On death → (if enabled in `GameSettings`) camera `Shake` + `ScreenFx.Flash`, then counts `respawnSeconds`→1 on screen ("Respawning in:" / "Enemy respawns in:"), then `Respawner.Respawn()` + a small landing shake. Player's countdown owns the shared label. |
| `ScreenFx.cs` | `GameDirector` | Builds a screen-space overlay at runtime: full-screen `Flash()` quad + centred `ShowCountdown(label,n)` label (legacy `Text`, `LegacyRuntime.ttf`). |
| `GameSettings.cs` | — (static) | `ScreenShakeOnElimination` / `ScreenFlashOnElimination` bools, `PlayerPrefs`-backed, default on. No settings-menu UI yet — these are the hooks a menu will flip. |
| `DamageFx.cs` | a car root with `Health` | Runtime particle FX (all code-generated, no art): smoke below `smokeBelow` (0.5) HP, flames below `fireBelow` (0.3) — `flameCount` tufts scattered at random spots over the car (`flameArea` half-extents), each a random size/rate; and on `Health.Died` an explosion burst + an expanding translucent-grey shockwave dome (`shockwaveRadius`/`Duration`/`Color`). FX rig follows the car unparented but copies its rotation so the flames stay car-relative. |
| `KillFloor.cs` | `GameDirector` | Any car below `fallLimit` (fell off an open arena / clipped through) is damaged to death → respawns. Safety net now; the fall hazard later. |
| `EnemyDriverAI.cs` | `EnemyCar` (with `CarController` + `Weapon` + `Health`) | Implements `IVehicleInput`. Never charges: holds a stand-off distance and circles the player, flipping orbit direction at random intervals, adding Perlin wander, sidestepping incoming player bullets, steering around obstacles (3 forward feelers). Dodge is deliberately fallible: per-bullet `dodgeChance` roll + `reactionDelay` before it acts + short `dodgeScanRadius`. Stances: `Pressing` (health > `evadeBelowHealth`, orbit `pressDistance`) / `Evasive` (hurt, orbit `evadeDistance`, twitchier). Finds the player by tag `Player`. While there's no live target (player dead / not found) it **roams** random points around `roamCenter` (holding fire) rather than idling. |
| `Projectile.cs` | `bullet.prefab` | Flies forward (`speed 40`, `damage 4`); on `OnTriggerEnter` passes through same-`team` (incl. shooter), else `Health.TakeDamage` + destroy. `team` set by the firing `Weapon`. Prefab visual: thin stretched cube (`scale 0.09×0.09×0.6`) with `Assets/Materials/Bullet.mat` (yellow URP Unlit) — a tracer round. Hitbox is a `SphereCollider` (trigger). |
| `Health.cs` | anything damageable | `maxHealth`, `CurrentHealth`, `HealthFraction`, `IsDead`, `TakeDamage`, `Revive()`. `Died` (C# `event Action<Health>`) + `onDeath` (`UnityEvent`) fire in `Die()`; `Destroy`s only if `destroyOnDeath` (a `Respawner` clears that). |
| `TeamMember.cs` | a vehicle root | `enum Team { Player, Enemy }` + one field. No `TeamMember` = neutral (destructible props) — hittable by anyone. |
| `CameraFollow.cs` | Main Camera | Smoothed chase cam; `target` = player. `offset` `(0,13,-16)` — directly behind + above, ~35° down (an action view; horizon in the upper third). Camera FOV 62. `Shake(duration, magnitude)` for kill/landing juice. |
| `OffscreenMarkers.cs` | `GameDirector` | Screen-edge arrow for every combatant that's off-camera, pointing at it. Colour is relative to the local player (tag `Player`): other team → red, same team → yellow. Runtime overlay canvas + pooled code-drawn arrow Images with an `Outline`. |
| `VirtualJoystick.cs` | a UI Image (bg) with a child handle Image | Touch stick; exposes `InputVector` (-1..1 per axis). Two instances: move + aim. |
| `JoystickSkin.cs` | a joystick `bg` object (with `VirtualJoystick` + `Image`) | Reskins the bg + handle Images at runtime as translucent circles with a code-drawn glyph (`glyph`: `DirectionalArrows` on the move stick, `Bullet` on the fire stick). `backgroundOpacity` / `handleOpacity` / glyph opacities tune the look. |

- **Scene:** `Assets/Scenes/SampleScene.unity` (the only scene).
  - `PlayerCar` — tag `Player`, layer `Player`; `CarController` + `Weapon` (still hold the two `VirtualJoystick` refs) + `Health(100)` + `TeamMember(Player)` + `PlayerInputRouter` + `HealthBar` + `Respawner` + `DamageFx`; child `FirePoint`.
  - `EnemyCar` — red material, `Untagged`, starts at `(0, 0.5, 20)`; same base components as PlayerCar but joystick refs cleared, plus `EnemyDriverAI` + `Health(100)` + `TeamMember(Enemy)` + `HealthBar` + `Respawner` + `DamageFx`; child `FirePoint`.
  - `GameDirector` — empty; `MatchDirector` + `ScreenFx` + `KillFloor` + `OffscreenMarkers`.
  - `Arena_Wall_N/S/E/W` — invisible BoxCollider boundary walls at the Plane edges (±25.5), so cars can't drive off. Some future arenas will omit these (falling = a hazard, caught by `KillFloor`).
  - `Canvas/movejoystick bg` + `aimjoystick bg` — the two `VirtualJoystick`s, each also carrying a `JoystickSkin` (arrows / bullet glyph).
  - Four `Cube`s — static obstacles (BoxCollider, no team/health).
  - `Plane` is 50×50 world units centred on origin (playable area ≈ x/z ∈ [-25, 25]). Square for now; arenas will become rectangular later.
- **Physics layers:** both cars are on `Player` (3), bullets on `Projectiles` (6). The Layer Collision Matrix is left fully enabled — `Projectile.cs` filters friendly/self hits by `Team` in code, so don't disable Projectiles↔anything or bullets stop registering.
- `Assets/Materials/` — runtime materials (`EnemyCar.mat`).
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

Enemy spawning/waves, score / match win state, audio, muzzle flash, per-hit feedback, **settings menu UI** (toggles for `GameSettings.ScreenShake/FlashOnElimination` already exist), main menu, line-of-sight checks for the AI (it currently shoots through walls; obstacle *avoidance* exists), per-vehicle stat presets, rectangular arenas.
