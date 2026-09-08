# ArenaPrototype

A twin-stick **vehicle arena shooter** for mobile (iOS/Android). Early prototype.

## Engine / setup

- **Unity 6000.3.23f1** (Unity 6.3). Open with Unity Hub; do not change the editor version without asking.
- **Render pipeline:** URP 17.3 (`Assets/Settings/` has separate `PC_*` and `Mobile_*` renderer + RP assets).
- **Input:** the new Input System package (1.20.0) is installed and `Assets/InputSystem_Actions.inputactions` exists, **but gameplay scripts currently use the legacy `Input.GetAxis` / `Input.GetButton` API** plus a custom on-screen `VirtualJoystick`. Keep this in mind — the project is mid-migration.
- No test project, no CI, no build scripts yet.

## Layout

Scripts live flat in `Assets/` (not in a `Scripts/` subfolder yet).

| File | Attach to | Role |
|---|---|---|
| `CarController.cs` | player vehicle (needs `Rigidbody`) | Drives toward the pushed direction; smooth turn-to-face. Reads optional `VirtualJoystick movementJoystick`, else `Input.GetAxis`. |
| `Weapon.cs` | player vehicle | Turret that aims independently via `aimJoystick`; auto-fires past `fireDeadzone`. Keyboard fallback: `Fire1` shoots straight ahead. Instantiates `projectilePrefab` at `firePoint`. |
| `Projectile.cs` | `bullet.prefab` | Moves forward at `speed`, `OnTriggerEnter` → `Health.TakeDamage`, self-destroys after `lifeTime`. |
| `Health.cs` | anything damageable | `maxHealth`, `TakeDamage(amount)`, `Die()` just `Destroy`s for now. |
| `CameraFollow.cs` | Main Camera | Smoothed elevated angled-down chase cam; drag vehicle into `target`. |
| `VirtualJoystick.cs` | a UI Image (joystick background) with a child handle Image | Touch stick; exposes `InputVector` (-1..1 per axis). Need two instances: move + aim. |

- **Scene:** `Assets/Scenes/SampleScene.unity` (the only scene).
- `Assets/TutorialInfo/` and `Assets/Readme.asset` are leftover URP-template content — safe to ignore or delete.

## Conventions

- Each gameplay script has a top-of-file `// ATTACH THIS TO:` comment saying which GameObject it belongs on. **Keep that comment updated** when behaviour changes.
- Tunables are `public` fields grouped under `[Header("...")]` so they're editable in the Inspector. Prefer this over hard-coded constants.
- Plain `MonoBehaviour` + `Instantiate`/`Destroy`. No object pooling, no ScriptableObjects, no DI, no assembly definitions yet — don't introduce these without discussing.
- 3D game: movement is on the XZ plane, `Vector3(input.x, 0, input.y)`.

## Working agreements

- **Version control:** git repo initialised. Commit working checkpoints; the user relies on this for undo.
- The user is newer to Unity — when a change needs manual steps in the Unity Editor (adding a component, wiring an Inspector reference, creating a prefab/tag/layer), spell them out step by step.
- Can't run the Unity Editor or enter Play mode from here — after code changes, tell the user what to test in the Editor.

## Not yet built

Enemies, enemy spawning/waves, score, game-over / win state, player respawn, audio, VFX (explosions, muzzle flash, hit feedback), menus.
