# Cheese Game (score, cheese eating, UI, win screen)

## Set up (once)
**Tools ▸ Cheese Game ▸ Set Up Everything (Main Menu + Game)** does all of it: builds `MainMenu.unity`
and `Game.unity`, MainMenu first in the build. Press Play on MainMenu.

Or step by step:
1. **Tools ▸ Cheese Game ▸ Create Game Scene (from FormSwitch_test)**
   - copies `FormSwitch_test` to `Assets/Scenes/Game.unity` (FormSwitch_test itself is untouched)
   - adds `Cheese` to every cheese object and one **Game Manager** object
   - makes the player start as the mouse, turns off the human's smell meter
   - adds `Game` to the build list (right after MainMenu if you have one)
2. **Tools ▸ Cheese Game ▸ Create Main Menu Scene** (title screen: Play, How to play, Quit, best time)
3. Press Play.

To set up a different scene instead: open it, **Tools ▸ Cheese Game ▸ Set Up Open Scene**, save.

## How it plays
- Goal: eat every cheese. **Only the mouse can eat** (walk up to it, press **E**).
- Each cheese = 100 points. Finishing under the par time (4:00) adds a time bonus. Best time is saved.
- Cheese behind or under furniture needs the human: open / push / pull / carry, then transform back.
- The mouse sees "something's in the way" when a cheese is close but blocked.
- Smell: each cheese owns the smell zone next to it. Zones follow cheese the human carries, and when
  a cheese is eaten its smell passes to a nearby cheese that had none (e.g. the wardrobe's top shelf).

## Screens and keys
| Screen | Keys |
|---|---|
| Main menu | Enter play, H how to play, Esc quit |
| Intro (goal + controls) | Enter or START |
| Playing | Esc pause |
| Paused | Esc resume, R restart, M main menu (Q quit if there's no main menu) |
| Win | R / Enter play again, M main menu |

## Scripts
| Script | What it does |
|---|---|
| `CheeseGameManager` | Phases (intro, playing, paused, won), score, timer, best time, sounds. Adds the HUD and `MouseCheeseEater` itself. |
| `MainMenu` | Title screen scene: Play, How to play, Quit, best time. |
| `UIKit` | Shared colours, panels, buttons and the controls card used by every screen. |
| `GameHUD` | All UI, built in code: objective + cheese counter, time + score, form badge, key-cap prompts, toasts, +points popups, intro / pause / win screens. |
| `Cheese` | On each cheese. Points, eat animation, owns its smell zone. |
| `MouseCheeseEater` | On the mouse (added at runtime). Finds reachable cheese, E to eat. |
| `GameState` | `InputBlocked` flag the player scripts check while a menu is open. |

Tweak on the Game Manager: title, par time, bonus per second, intro on/off, cursor lock, volume.
Tweak per cheese: `points`.

The team's smell meter (top centre) is unchanged; the new UI is laid out around it.
