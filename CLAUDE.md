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
| `HealthBar.cs` | a car root with `Health` | Builds a billboarded world-space bar (two unlit quads) above the car **at runtime** — no scene setup, no art. Green→red by `HealthFraction`. Cleans up its bar object in `OnDestroy`. |
| `EnemyDriverAI.cs` | `EnemyCar` (with `CarController` + `Weapon` + `Health`) | Implements `IVehicleInput`. Never charges: holds a stand-off distance and circles the player, flipping orbit direction at random intervals, adding Perlin wander, sidestepping incoming player bullets, steering around obstacles (3 forward feelers). Dodge is deliberately fallible: per-bullet `dodgeChance` roll + `reactionDelay` before it acts + short `dodgeScanRadius`. Stances: `Pressing` (health > `evadeBelowHealth`, orbit `pressDistance`) / `Evasive` (hurt, orbit `evadeDistance`, twitchier). Finds the player by tag `Player`. |
| `Projectile.cs` | `bullet.prefab` | Flies forward (`speed 40`, `damage 4`); on `OnTriggerEnter` passes through same-`team` (incl. shooter), else `Health.TakeDamage` + destroy. `team` set by the firing `Weapon`. |
| `Health.cs` | anything damageable | `maxHealth`, `CurrentHealth`, `HealthFraction`, `TakeDamage(amount)`. `Die()` invokes `UnityEvent onDeath` then `Destroy`s. |
| `TeamMember.cs` | a vehicle root | `enum Team { Player, Enemy }` + one field. No `TeamMember` = neutral (destructible props) — hittable by anyone. |
| `CameraFollow.cs` | Main Camera | Smoothed elevated angled-down chase cam; `target` = the player vehicle. |
| `VirtualJoystick.cs` | a UI Image (bg) with a child handle Image | Touch stick; exposes `InputVector` (-1..1 per axis). Two instances: move + aim. |

- **Scene:** `Assets/Scenes/SampleScene.unity` (the only scene).
  - `PlayerCar` — tag `Player`, layer `Player`; `CarController` + `Weapon` (still hold the two `VirtualJoystick` refs) + `Health(100)` + `TeamMember(Player)` + `PlayerInputRouter` + `HealthBar`; child `FirePoint`.
  - `EnemyCar` — red material, `Untagged`, starts at `(0, 0.5, 20)`; same base components as PlayerCar but joystick refs cleared, plus `EnemyDriverAI` + `Health(100)` + `TeamMember(Enemy)` + `HealthBar`; child `FirePoint`.
  - Four `Cube`s — static obstacles (BoxCollider, no team/health).
  - `Plane` is 50×50 world units centred on origin (playable area ≈ x/z ∈ [-25, 25]).
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

Enemy spawning/waves, score, game-over / win state (player `Health.onDeath` is an empty hook), player respawn, audio, VFX (explosions, muzzle flash, hit feedback), menus, line-of-sight checks for the AI (it currently shoots through walls; obstacle *avoidance* exists), per-vehicle stat presets.
