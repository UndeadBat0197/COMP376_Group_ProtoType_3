# Human form — interaction prototype

Everything lives in `Assets/Script/Human/` so it doesn't touch the mouse scripts or existing scenes.

## Quick start (your own human in human_environment)
1. Copy this `Human` folder into `Assets/Script/` and let Unity compile.
2. Open `human_environment`, then **Tools ▸ Human Prototype ▸ Set Up Open Scene For Human**.
   It finds the object called `human` (or whatever you have selected) and keeps your model, but:
   - unpacks it from the mouse prefab, so nothing can be applied back onto the mouse
   - swaps ThirdPersonMovement → HumanMovement, keeping your walk/sprint/jump values
   - replaces the mouse-sized box collider with a capsule fitted to the body, and sets mass to 70
   - adds HumanInteractor + HoldPoint, moves the camera target to head height, pulls the FreeLook back
   - makes `armchair_01` grabbable (F)
   - swaps `wardrobe` for a same-size one that opens (E) and can be pulled off the wall (F); original only disabled
   - adds a lidded Crate with a book inside and cheese behind it, a Boulder with cheese under it, and two liftable rocks
   - running it again on an older setup rebuilds the old crate/wardrobe with the new behaviour
   Every step is listed in the Console. One Ctrl+Z undoes all of it. Save the scene afterwards.
3. Press Play.

## Separate test scene
**Tools ▸ Human Prototype ▸ Create Test Scene** builds `Assets/Scenes/Human_Interaction_Test.unity`
with a placeholder capsule human and the same props.

The test scene has: the human, a cabinet with double doors and a cheese wheel on the top shelf
(a book on the middle shelf), a chair, a heavy crate, two rocks, and the mouse (press **T** to switch forms).

## Controls (human)
| Key | Action |
|---|---|
| WASD / Shift / Space | Move / sprint / jump (camera-relative) |
| E | Use: open doors/lid, look inside, pick up |
| F | Grab heavy things (cabinet, crate, boulder, armchair) |
| While grabbing | W push, S pull (A/D turn on the armchair only), E or F let go |
| While carrying | E drop, left click throw |
| Looking into shelf / crate | Point with the cursor, click/E to take an item; Q / Esc / right click to step back |
| T | Switch mouse ⇄ human (only if a FormSwitcher is set up) |

Heavy things are locked in place until you grab them, so walking into them does nothing.

## What's hidden where (human_environment after setup)
- Cheese behind the wardrobe: F on the wardrobe, then S to pull it off the wall.
- Cheese on the wardrobe's top shelf: E open, aim at the top shelf, E look, click the cheese.
- Book inside the crate: E open lid, E look inside, click the book.
- Cheese behind the crate (against the wall): F, then S to pull the crate out.
- Cheese under the boulder: F, then W/S to roll it off.
- Small Rock / Rock: E to pick up, left click to throw.

## Scripts
| Script | Goes on | What it does |
|---|---|---|
| `HumanMovement` | Human root (Rigidbody + CapsuleCollider) | Movement. Exposes `SpeedMultiplier`, `InputEnabled`, grab mode. |
| `HumanInteractor` | Human root | Aiming, E/throw input, prompt text (OnGUI placeholder). |
| `Interactable` | (abstract base) | Subclass this for new interactions. |
| `Liftable` | Small props with Rigidbody (rock, cheese, book) | Pick up / carry / drop / throw. |
| `Pushable` | Furniture with Rigidbody (chair, crate) | Grab and push/pull/turn. `allowPull`, `canRotate` per object. |
| `Openable` | Door hinge pivot or drawer | Toggle open/close. Hinge or Slide mode, lockable, linkable double doors. |
| `LookPoint` | Trigger collider over a shelf | Switches to a fixed camera to look into the shelf. |
| `FormSwitcher` | Its own GameObject | Swaps mouse/human at the same spot. Call `BecomeHuman()` / `BecomeMouse()` from game logic. |

## Adding things to your own scene
**Tools ▸ Human Prototype ▸** Add Human Player / Add Cabinet / Add Pushable Chair / Add Pushable Crate / Add Liftable Rock
(spawns at the Scene view's centre), or select any object and use **Make Selected Liftable / Pushable**
(adds a Rigidbody + collider if missing).

Using a real model for a cabinet door: make an empty at the hinge edge, put the door mesh under it,
put `Openable` + a kinematic Rigidbody on the empty. Flip `openAngle`'s sign if it swings inward.

## Notes
- The human copies the mouse prefab's FreeLook camera (same input setup) with a bigger orbit radius.
  If both forms are in a scene, keep only one active (FormSwitcher handles this).
- Cinemachine's default blend is 2 s; shorten it on the Main Camera's CinemachineBrain if
  entering the top-shelf view feels slow.
- The mouse has no `HumanInteractor`, so it can't use any of this — that's the point.
- Prompt UI is OnGUI so it works with no setup; swap for TMP later by reading
  `HumanInteractor.Current / Held / Grabbed / ActiveLook`.
