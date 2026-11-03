# Elf & Safety

A co-op toy-repair game where you are disposable elves in Santa's workshop. Turn old and broken toys into presents, meet the daily quota, and stay out of the furnace.

**Working title · Unity / C# · Solo-developed · Planned 2–4 player online co-op**

The workshop looks cheerful. Management is not.

## Current status

**Milestone 8 (menus, settings and HUD) is implemented, and its play test is pending.** The game now opens on a main menu (Play Solo, Host Co-op, Join Co-op, How to Play, Settings, Quit), has saved settings (mouse, field of view, volume, display, HUD options), an Esc pause menu, and a tidier HUD with the wish list on screen. All 56 automated PlayMode tests pass; nobody has clicked through the menus by hand yet, and both scenes need the builder re-run.

**Milestone 7 (bad-part malfunctions) is implemented, and its play test is pending.** Some delivered parts now hide a defect: a leaky battery zaps whoever picks up its toy, an overwound key makes a toy run away, a wobbly wheel set makes a car drive off, and loose button eyes pop off a carried bear. Faulty parts keep their normal names, have a small visual tell, and never pass inspection. All 49 automated PlayMode tests pass; it hasn't been played by hand yet, and the Workshop scene needs the builder re-run.

**Milestone 6 (more toys and wish orders) is implemented, and its play test is pending.** Alongside the wind-up robot there are now teddy bears and toy cars, and each day has a wish list of kids' orders, including **cyborg bears** (a teddy bear fitted with a robot battery and winding key). A shipped toy must exactly match an open order. All 44 automated PlayMode tests pass; it hasn't been played by hand yet, and the Workshop scene needs the builder re-run.

**Milestone 5 (the Christmas countdown) is implemented, and its play test is pending.** The game is now a week: seven days counting down from 7 days until Christmas to Christmas Eve, each with its own quota, shift length and delivery waves, getting harder. Pass a day to move on; fail one and it's back to day 1; survive Christmas Eve to win. All 38 automated PlayMode tests pass. It hasn't been played by hand yet, and the Workshop scene needs the builder re-run.

**Milestone 4 (two-player co-op proof) is implemented, and its two-player play test is pending.** One player hosts and a second joins. Both elves share one workshop: either player can carry toys, use every station, ship robots and clock in, and both see the same shift timer, shipped count and verdict. The host decides everything; the other player's actions are requests the host checks. All 30 automated PlayMode tests pass, but a second player joining can't be tested automatically, so the two-player checks below still need doing by hand.

Part 1 (shared workshop and carrying) was tried by the developer in Multiplayer Play Mode on 2026-11-03; the one issue reported, that Player 2 couldn't salvage, was part 1's intended host-only limit, which part 2 removes.

Milestone 3 is complete: one scored shift with a timer, quota, Santa's verdict and a trapdoor sequence on failure, verified by 11 automated workday tests and a developer play test on 2026-11-03 (table below).

Milestone 2 is complete: the repair loop passed its automated tests and a developer play test on 2026-11-03, confirmed in the editor log (table below).

| Milestone | Scope | Status |
|---|---|---|
| 1 — Workshop foundation | Graybox room, first-person movement, interaction targeting, pickup and drop | ✅ Complete (developer play-tested) |
| 2 — First toy loop | Robot delivery, salvage, cleaning, assembly, shipping | ✅ Complete (automated tests and developer play test) |
| 3 — A full workday | Timer, quota, Santa's verdict, trapdoor failure, restart | ✅ Complete (automated tests and developer play test) |
| 4 — Co-op proof | Two players sharing objects and synchronized workday state | 🟨 Implemented (parts 1 and 2); automated tests pass; two-player play test pending |
| 5 — Christmas countdown | Seven days to Christmas with rising quotas and delivery waves; fail = back to day 1 | 🟨 Implemented; automated tests pass; manual play test pending |
| 6 — More toys and orders | Bear and car; kids' wish orders such as a cyborg bear (a bear fitted with robot parts) | 🟨 Implemented; automated tests pass; manual play test pending |
| 7 — Bad-part malfunctions | Faulty parts that make assembled toys misbehave, to catch before shipping | 🟨 Implemented; automated tests pass; manual play test pending |
| 8 — Menus and settings | Main menu, settings, pause menu, HUD polish | 🟨 Implemented; automated tests pass; manual play test pending |

### Milestone 8 verification

| Check | How | Result |
|---|---|---|
| Scripts compile; builder makes the Main Menu and Workshop scenes, with the main menu first in the build order | Batch mode, in a copy with settings identical to the developer's project | ✅ 0 errors, 0 warnings, 0 missing scripts |
| Earlier milestones regression | The 49 other PlayMode tests (the co-op leave test now expects the main menu) | ✅ Pass |
| Settings are saved, clamped to their ranges, and volume applies at once | `MenuTests` | ✅ Pass |
| Field of view reaches the player camera | `MenuTests` | ✅ Pass |
| Main menu: Play Solo loads a solo Workshop; Host loads the Workshop and starts hosting | `MenuTests` | ✅ Pass |
| Pause freezes solo play (the shift timer stops) and resumes; Main Menu from pause returns to the title screen unpaused | `MenuTests` | ✅ Pass |
| Co-op Leave returns to the main menu with no error and no network manager left | `NetworkSessionTests` | ✅ Pass |
| The on-screen wish list follows its setting | `MenuTests` | ✅ Pass |
| Clicking every button, sliders, dropdowns, the join address box, layout at different resolutions, fullscreen/resolution in a build | Manual play test | ⬜ Pending |

### Milestone 7 verification

| Check | How | Result |
|---|---|---|
| Scripts compile; builder creates the Leaky Battery, Overwound Winding Key, Wobbly Wheel Set and Loose Button Eyes prefabs | Batch mode, in a copy with settings identical to the developer's project | ✅ 0 errors, 0 warnings, 0 missing scripts |
| Earlier milestones regression | The 44 other PlayMode tests (expectations updated only for the bear wave's extra loose part) | ✅ Pass |
| Faulty parts never count toward a day's solvability | `MalfunctionTests` | ✅ Pass |
| The risky robot's tells (labels say leaking/rusty, the part says "looks rusty", the prompt name stays neutral) and inspection ("battery is faulty", "winding key is faulty"), including at the shipping chute | `MalfunctionTests` | ✅ Pass |
| Overwound key: the robot runs off on its own, and stays put once the key is removed | `MalfunctionTests` | ✅ Pass |
| Leaky battery: the toy zaps the player into dropping it; a loose leaky battery is safe to carry | `MalfunctionTests` | ✅ Pass |
| Loose button eyes: they pop off a carried bear, which stays in hand | `MalfunctionTests` | ✅ Pass |
| Wobbly wheel set driving a car off | Code shared with the overwound key; not separately tested | ⬜ Manual |
| How the malfunctions feel and read in play; co-op (they run on the host) | Manual play test | ⬜ Pending |

### Milestone 6 verification

| Check | How | Result |
|---|---|---|
| Scripts compile; builder creates the bear, car, stuffing, button eyes and wheel set prefabs, upgrades the existing robot prefab to general slots (keeping its art), and removes the old delivery asset | Batch mode, in a copy with settings identical to the developer's project | ✅ 0 errors, 0 warnings, 0 missing scripts |
| Earlier milestones regression, on the generalized toy code | The 38 other PlayMode tests (expectations updated only for the new wording and wave contents) | ✅ Pass |
| Toys have their slots: bear (stuffing, eyes, battery, key), car (battery, two wheel sets) | `ToyTests` | ✅ Pass |
| Inspection: plain bear vs cyborg bear (robot parts "weren't on the wish list" for a plain bear), car missing a wheel set, wrong toy | `ToyTests` | ✅ Pass |
| Slots refuse a full slot and a part the toy has no slot for | `ToyTests` | ✅ Pass |
| Assembly bench fits a wheel set into the empty wheel slot, attached to the car | `ToyTests` | ✅ Pass |
| Shipping fills the matching wish order (board ticks it); a plain bear on a cyborg-only day is rejected with what's missing; a toy whose orders are all filled is refused | `ToyTests` | ✅ Pass |
| Every default day is winnable with its waves; impossible days (no bear delivered, no working battery, late wave, wave too big for the table) are refused | `CountdownTests` | ✅ Pass |
| Bear and car visuals and mounts, prompts, wish list board legibility, difficulty | Manual play test | ⬜ Pending |

### Milestone 5 verification

| Check | How | Result |
|---|---|---|
| Scripts compile; builder creates the Christmas Countdown asset and connects it | Batch mode, in a copy with settings identical to the developer's project | ✅ 0 errors, 0 warnings, 0 missing scripts |
| Earlier milestones regression | The 30 other PlayMode tests | ✅ Pass |
| Default week: 7 days, every day winnable with its waves, quotas never go down; impossible days are refused | `CountdownTests` | ✅ Pass |
| Day 1 is "7 days until Christmas", quota 2, 5:00, with a Santa note | `CountdownTests` | ✅ Pass |
| Passing a day: Next Day starts day 2 in place (no reload); toys, count, verdict and last result cleared; elf unfrozen; quota 3 | `CountdownTests` | ✅ Pass |
| Later delivery waves arrive on schedule, wait while the table is cluttered, and add to the first wave | `CountdownTests` | ✅ Pass |
| Failing day 3 says "Back to 7 days until Christmas" and Back to Day 1 reloads at day 1 | `CountdownTests` | ✅ Pass |
| Winning Christmas Eve shows MERRY CHRISTMAS!; Play Again starts the week over | `CountdownTests` | ✅ Pass |
| Co-op: Next Day starts in place for everyone and the day is shared | `NetworkSessionTests` | ✅ Pass |
| HUD day line and Santa note, verdict button labels, wave timing feel, difficulty curve | Manual play test | ⬜ Pending |

### Milestone 4 part 2 verification

| Check | How | Result |
|---|---|---|
| Scripts compile; builder adds the Workshop Sync prefab and registers it once | Batch mode, in a copy with settings identical to the developer's project | ✅ 0 errors, 0 warnings, 0 missing scripts |
| Milestones 2 and 3 and co-op part 1 regression | The 28 other PlayMode tests | ✅ Pass |
| Forwarded targets resolve on the host: a scene station by scene path, a robot part mount by robot plus mount path; the real station code runs; a repeated request is refused | `NetworkSessionTests` | ✅ Pass |
| Online shift: clock in works; shift state and shipped count are published; shipping the quota wins once; Play Again resets in place while still hosting (toys despawned, count 0, elf unfrozen); a second shift starts | `NetworkSessionTests` | ✅ Pass |
| Player 2 uses every station, ships, clocks in; both screens agree on timer, count, labels and verdict; both fall through the trapdoor on failure; Play Again from either player | Multiplayer Play Mode, by hand | ⬜ Pending; see [Co-op test](#co-op-test-two-players-on-one-pc) |

### Milestone 4 part 1 verification

| Check | How | Result |
|---|---|---|
| Netcode for GameObjects 2.13.3 and Multiplayer Play Mode 3.0.0 resolve offline (bundled with the editor) | Batch mode, in a copy with settings identical to the developer's project | ✅ |
| Scripts compile; builder adds network components to existing toy prefabs and creates the networked elf, the network manager prefab and the session | Batch mode, same copy | ✅ 0 errors, 0 warnings, 0 missing scripts |
| Milestones 2 and 3 regression (solo play) | The 24 existing PlayMode tests, unchanged | ✅ Pass |
| Solo play creates no network manager | `NetworkSessionTests` | ✅ Pass |
| Hosting clears practice items and replaces the solo elf with one networked, owned elf | `NetworkSessionTests` | ✅ Pass |
| Host: delivered items are spawned on the network; robot slots sync the actual part; dirty state syncs; carry state follows pick up and drop; installed parts stay on their mounts | `NetworkSessionTests` | ✅ Pass |
| Online: clocking in is refused | `NetworkSessionTests` | ✅ Pass |
| Leaving returns to solo with no network manager left behind | `NetworkSessionTests` | ✅ Pass |
| A second player joins, both elves move, both carry, no double-grabs, host actions show on the client | Multiplayer Play Mode, by hand | 🟨 Tried by the developer; the only issue reported was the intended part 1 host-only salvage limit. Not yet re-checked with part 2 |

### Milestone 3 verification

| Check | How | Result |
|---|---|---|
| Scripts compile; builder generates the new scene | Batch mode, in a copy with settings identical to the developer's project | ✅ 0 errors, 0 warnings, 0 missing scripts |
| Milestone 2 regression | The 13 existing PlayMode tests, unchanged | ✅ Pass |
| Defaults: 5:00, quota 2, timer stopped before clocking in | `WorkdayTests` | ✅ Pass |
| Clocking in clears practice items (including one in hand), zeroes the count and delivers one fresh batch | `WorkdayTests` | ✅ Pass |
| Request Delivery and DEV reset refused during the shift, even after every robot is gone | `WorkdayTests` | ✅ Pass |
| Rejected robots earn zero credit | `WorkdayTests` (all three delivered robots rejected) | ✅ Pass |
| Win: shipping the quota succeeds once; interactions, shipping and DEV reset stop | `WorkdayTests` | ✅ Pass |
| Timeout: fails once, trapdoor drops the player, then Retry with the furnace line | `WorkdayTests` | ✅ Pass |
| Deadline frame, quota shipment first: one verdict (success) | `WorkdayTests` | ✅ Pass |
| Deadline frame, timer first: the late shipment is refused, one verdict (failure) | `WorkdayTests` | ✅ Pass |
| Play Again and Retry each reload a fresh Workshop: timer, count, delivery, stations, carried item, trapdoor | `WorkdayTests` | ✅ Pass |
| Builder run in the developer's project | Menu, in the open editor | ✅ Scene rebuilt (previous scene backed up) |
| Clock in, then win by shipping 2 | Developer play test; editor log | ✅ Reported passing; the log shows "Shift started! Ship 2 robots in 5:00", then 1 and 2 shipped |
| Play Again returns to practice mode | Developer play test; editor log | ✅ Reported passing; the log shows practice deliveries and DEV resets working afterwards (only possible after a fresh reload), then a second clock-in |
| Timeout, trapdoor sequence, Retry | Developer play test; editor log | ✅ Reported passing; the log shows a 10-second shift starting. The verdict, fall and Retry write no log line, so the developer's report is the evidence |
| Delivery and DEV reset refused during a shift | Developer play test | ✅ Reported passing (prompt-only, so nothing is logged) |
| Rejected robot during a scored shift earns nothing | Developer play test; `WorkdayTests` | ✅ Reported passing and covered by the automated test; no rejection appears in the logged shift sessions |

### Milestone 2 verification

| Check | How | Result |
|---|---|---|
| Scripts compile (Runtime, Editor, Tests) | Developer's editor, Unity 6000.6.7f1 | ✅ 0 errors, 0 warnings |
| Builder generates scene, prefabs and batch asset | Batch mode, in a copy with identical settings | ✅ 0 import failures, 0 missing scripts |
| Rule tests (inspection reasons, batch solvability) | 4 PlayMode tests | ✅ Pass |
| Loop tests: delivery once as loose items, salvage once, blocked outputs, cleaning reservation, robots arriving and leaving by physics, assembly, rejections, repair and resubmit, second robot, reset | 9 PlayMode tests driving the real Workshop scene | ✅ Pass |
| Builder run in the developer's project | Menu, in the open editor | ✅ Scene rebuilt and backed up first; no old slot components left |
| First manual attempt (salvage) | Developer, in Play mode | ❌ NullReferenceException after a mid-Play recompile. Fixed; no exceptions in the play test after the fix |
| Movement, aiming, prompts, pickup, drop, free physics | Developer play test | ✅ Reported passing |
| Delivery | Developer play test; editor log | ✅ Reported passing; the log shows one delivery |
| Salvage without duplication | Developer play test; editor log | ✅ Reported passing; the log shows the battery and dead battery recovered once each |
| Cleaning | Developer play test; editor log | ✅ Reported passing; the log shows one body cleaned |
| Assembly, and shipping a valid robot | Developer play test; editor log | ✅ Reported passing; the log shows 1 robot shipped |
| Dirty and incomplete rejections with reasons | Developer play test; editor log | ✅ Reported passing; the log shows 3 rejections with the specific reasons |
| Dead-battery rejection | Developer play test; editor log | ✅ Log shows "body is dirty, battery is dead" rejection |
| Repair a rejected robot and resubmit; second robot shipped | Developer play test; editor log | ✅ Log shows the rejected robot's dead battery salvaged, the body cleaned, then 1 and 2 shipped |
| DEV reset keeps ownership and counter consistent | Developer play test; editor log | ✅ Two resets, each followed by a fresh delivery that spawned; no exceptions |
| Installing a dead battery at the assembly bench (warning shown) | Developer play test; editor log | ✅ Log shows "Installed Dead Battery. It's dead, so inspection will reject it" |

The tests call the same methods the interaction prompts call. They don't cover raycast aiming, prompt text, how attached parts look, or input. Every station posts a `[Workshop]` line to the Console, which is how the log evidence above was gathered.

**Recompiling during Play mode.** Unity recompiles changed scripts when you tab back in, even during Play. Ownership now survives that reload, which was the cause of the first manual crash. Other runtime state may still behave oddly after a mid-Play recompile, so set **Edit → Preferences → General → Script Changes While Playing → Recompile After Finished Playing**.

## Getting started

1. Clone this repository and add its project folder in Unity Hub.
2. Use **Unity 6000.6.7f1** (recorded in `ProjectSettings/ProjectVersion.txt`). The project uses URP, uGUI and the Input System package (Input System only) from the editor's URP Blank template, plus **Netcode for GameObjects 2.13.3** and **Multiplayer Play Mode 3.0.0**, both bundled with this editor.
3. Let Unity import the project.
4. **Generate the scenes:** run the menu **Elf & Safety → Build Game Scenes**. This creates `Assets/_Project/Scenes/MainMenu.unity` and `Assets/_Project/Scenes/Workshop.unity` (it asks before overwriting, and offers to save unsaved scene changes), plus any toy prefabs, materials and data assets that don't exist yet, and puts the main menu first in the build order. Before overwriting, it copies each existing scene to `SceneBackups/<scene>_<date>_<time>.unity` next to `Assets/` (ignored by git, and not imported by Unity). To restore a backup, copy it back over the scene file while keeping that file's `.meta`. Prefabs, materials and the batch asset that already exist are never overwritten, so swapped-in art and tuned values survive a rebuild. Re-run it after pulling builder changes.
5. Press **Play**. Play always starts at the main menu, like the built game, whichever scene is open (turn this off with **Elf & Safety → Always Play From Main Menu** to play the open scene directly). Test Runner sessions are exempt.

### Menus and settings

- **Main menu:** **Play Solo**, **Host Co-op**, **Join Co-op** (type the host's address in the box first; it's remembered), **How to Play**, **Settings**, **Quit**. If a join fails or the host leaves, you're back here with a message saying so.
- **Pause (Esc in the Workshop):** **Resume**, **Settings**, **Main Menu** (leaves a co-op session), **Quit Game**. Solo play freezes while paused, timer included. Co-op can't be paused for everyone: the menu frees your mouse and stops your input, and the shift keeps running.
- **Settings** (saved between sessions, from either menu): mouse sensitivity, invert mouse Y, field of view, master volume, fullscreen, resolution, graphics quality, VSync, show controls hint, show wish list on screen, and **Reset to defaults**. Fullscreen and resolution only take effect in a built game, not in the editor's game view.
- **HUD:** a dark bar behind the day, time and wishes; today's wish list on the right; the controls hint top-left (both can be turned off in Settings); and a **Main Menu** button next to Next Day / Back to Day 1 on Santa's verdict.

The builder can also run headless (it fails if the project is open in another editor):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Unity.exe" -batchmode -projectPath "D:/GameDev/ElfAndSafety" -executeMethod ElfAndSafety.EditorTools.WorkshopBuilder.BuildFromCommandLine -logFile build.log
```

### Running the tests

Use **Window → General → Test Runner → PlayMode → Run All**, or from the command line (with the project closed):

```bash
"C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Unity.exe" -batchmode -projectPath "D:/GameDev/ElfAndSafety" -runTests -testPlatform PlayMode -testResults results.xml -logFile tests.log
```

The loop tests load the generated `Workshop` scene, so run the builder first. Keep the project at a short path: on Windows, a project nested deep under a long user folder can exceed the 260-character path limit, which breaks URP package imports and leaves generated assets with missing scripts.

### Controls

Bindings are defined in code in `Assets/_Project/Scripts/Runtime/Player/PlayerInputReader.cs`.

| Input | Action |
|---|---|
| W A S D / arrow keys | Move |
| Mouse | Look |
| Left Shift | Sprint |
| Space | Hop |
| E or left click | Interact: pick up, place, remove part, install part, ship, press a button (including **CLOCK IN**) |
| Q | Drop the carried item |
| Esc | Pause menu (Esc again resumes, or closes Settings). Click the game view to capture the mouse again after it's been released |
| Mouse click or R (verdict screen) | Next Day / Back to Day 1 / Play Again |

### Co-op test (two players on one PC)

Milestone 4: two elves share one workshop and one shift. Solo play is unchanged and uses no networking.

1. Open **Window → Multiplayer → Multiplayer Play Mode** and tick **Player 2**. A second game window opens; give it a few seconds to load.
2. Press **Play**. Both windows open on the main menu.
3. In the main window, click **Host Co-op**. In Player 2's window, check the join address says `127.0.0.1` and click **Join Co-op**. Each window loads the Workshop with a networked elf; the bottom-left line shows "hosting, 2 elves connected" or "joined".
4. **Esc → Main Menu** leaves the session. If the host leaves, the client goes back to the main menu with "The host ended the session".

What works:
- Both elves move, and each sees the other move and look up and down.
- Either player can pick up, carry, drop (Q) and place (E on a station) any loose toy. The host decides every carry, so two players can never hold the same item.
- Either player can use everything: REQUEST DELIVERY and DEV RESET (in practice), salvage, cleaning, assembly, shipping and CLOCK IN. When Player 2 presses E, the request goes to the host, which checks it against its own state and runs the same station code. If it's no longer possible (someone else got there first), nothing happens.
- Loose toys fall and settle with physics on the host; the other player sees their synced positions. Installed parts stay attached to their robot on both screens.
- Both screens show the same shift timer, shipped count, cleaning progress, station labels and delivery status, and every station message appears for both players.
- One shared verdict. On success, both players get Santa's message. On failure, each player's elf is pulled onto the trapdoor and drops into the pit, and each sees the other fall.
- Play Again / Retry from either player resets the workshop for both without leaving the session: toys cleared, count 0, trapdoor closed, both elves back at the start in practice mode.

Limits of this proof:
- Station messages go to both players, including ones meant for the player who acted (such as "Output tray is blocked").
- There's no client prediction, so a client's actions wait for the host's reply. That's invisible on one PC, but noticeable over a real network.
- Shift length and quota come from the host; both machines should use the same scene settings.
- A player who joins mid-shift isn't handled specially.
- For a second PC on your network, set **Address** on the joining machine's **Network Session** object (Workshop scene) to the host's IP. Playing over the internet needs a relay service and is not part of this milestone.

### Playing the week

The game is a countdown to Christmas: day 1 is "7 days until Christmas" and day 7 is Christmas Eve. The top of the screen always shows the day, and before clocking in it also shows Santa's note for the day.

| Day | Until Christmas | Wish list | Shift | Deliveries |
|---|---|---|---|---|
| 1 | 7 days | 2 robots | 5:00 | Robots |
| 2 | 6 days | 2 robots, 1 bear | 5:00 | Robots, Bears (every 1:40) |
| 3 | 5 days | 1 robot, 1 bear, 1 car | 5:00 | Robots, Bears, Cars (every 1:30) |
| 4 | 4 days | 2 bears, 2 cars | 5:00 | Bears, Cars (every 1:30) |
| 5 | 3 days | 2 robots, 2 cyborg bears | 5:00 | Risky robots, Cyborg kit (every 1:30) |
| 6 | 2 days | 2 cars, 1 bear, 2 cyborg bears | 5:00 | Cars, Bears, Cyborg kit (every 1:20) |
| 7 | Christmas Eve | 2 robots, 1 car, 1 bear, 2 cyborg bears | 5:00 | Risky robots, Cars, Cyborg kit, Bears (every 1:10) |

Every order has a kid's name, such as *Timmy's wind-up robot*. The **WISH LIST** board on the east wall, between the shipping chute and the reject bin, lists today's orders and ticks them off as they ship.

1. Each day starts in **practice mode**. Everything works but nothing counts, and the top of the screen says so. Look around, or try the loop with **REQUEST DELIVERY**.
2. Press E on the green **CLOCK IN** button on the red post just ahead and to the left of where you spawn. This removes any practice items (even one in your hands), sets the count to 0, drops the first delivery onto the table and starts the timer. The top of the screen shows the time left, **Wishes filled N / total** and, on days with several deliveries, **Deliveries N / total**; the time turns red under 30 seconds.
3. Repair toys and ship them as described below. During a shift a shipped toy must **exactly match an open order**: the right toy, a clean body, every part the order needs installed and working, and nothing extra. It then fills that order. Anything else is rejected with what's wrong, for example *"battery is missing, winding key is missing (for Jax's cyborg bear)"* or *"nobody on today's wish list wants a toy car"*. Later deliveries arrive on their own during the shift, and a day's deliveries always bring enough toy bodies and working parts for its whole wish list. If the delivery table is still cluttered when one is due, it waits ("A delivery is waiting") until there's room. During the shift, Request Delivery and DEV reset are refused.
4. **Fill every wish:** the shift ends at once. Santa congratulates you and says tomorrow's quota, with **Next Day**. The next day starts right there, without reloading, in practice mode with a clean workshop.
5. **Run out of time:** you're pulled onto the trapdoor in front of Santa's window, the doors swing open over an orange glow, and you drop into the furnace pit. Then *"You've been reassigned to furnace operations"* appears with **Back to Day 1**, and the week starts over.
6. **Pass Christmas Eve:** *MERRY CHRISTMAS!* and **Play Again**, which starts the week over.
7. Once a shift ends, nothing can be picked up or shipped. Click the button or press **R** to continue.

The deadline is strict. The timer is checked before player actions each frame, so a toy shipped in the very frame time runs out doesn't count, and there's only ever one verdict.

### Playing the repair loop

Every robot and part is a normal physics object: drop it anywhere with Q, knock it over, stack it. Stations don't lock items to a spot. Each one works on whichever robot is resting in its **work area** (the space above its gold pad), however it got there. Aiming at a station while carrying a robot offers **Place** (sets it down neatly on the pad), but dropping it onto the bench works too.

1. Press the green **REQUEST DELIVERY** button at the delivery chute (north-west corner). Six items drop onto the **Delivery Table** by the west wall: three robots (dirty with a dead battery and key; dirty and empty; clean with a working battery), a working battery, a winding key, and a dead battery. Enough usable parts for exactly two robots.
2. **Salvage:** put a robot on the salvage bench, then with empty hands aim at its battery or key and press E to remove it. Removed parts pop out loose onto the green parts tray (or into your hands if the tray spots are blocked).
3. **Clean:** put a dirty robot in the blue tub. Once it comes to rest, the station holds it upright and scrubs it for 3 seconds with a progress bar, then lets go. It can't be taken mid-scrub.
4. **Assemble:** put a robot on the assembly bench, carry a battery or key to it, aim at the robot or bench and press E to install it. The label lists the body, battery and key status.
5. **Ship:** carry a finished robot to the shipping chute and press E. Valid robots count once (the shipped count at the top of the screen). Invalid ones are dumped into the red-banded **Reject Bin** with the reasons.
6. The red **DEV: RESET TEST BATCH** button removes every delivered item, wherever it is, so you can request a fresh batch. The shipped counter is kept. It only works in practice mode, before clocking in.

Looking at a robot or part shows its status under the crosshair. Every station message is also written to the Console with a `[Workshop]` prefix.

## The game loop

1. **Receive:** broken toys arrive through a delivery chute.
2. **Sort:** decide what to repair and what to dismantle for parts.
3. **Salvage:** recover usable components at a workbench.
4. **Clean:** wash dirty toy bodies before assembly.
5. **Assemble:** fit compatible parts into a complete toy.
6. **Ship:** send finished presents through inspection for quota credit.
7. **Survive inspection:** meet quota before time runs out, or face Santa's cheerful disciplinary process.

The first prototype uses predictable deliveries so the loop can be tested. More varied deliveries and toy malfunctions come after the foundation works.

## Architecture — proposed responsibilities

This diagram describes the intended separation of responsibilities. It does not prescribe new class names or require a rewrite of milestone 1.

```mermaid
flowchart TD
    Player["Player input and interaction"] --> Carry["Object carrying and ownership"]
    Carry --> Stations["Salvage, cleaning, and assembly stations"]
    Toys["Toy state and installed components"] <--> Stations
    Toys --> Shipping["Shipping validation"]
    Shipping --> Day["Workday timer and quota"]
    Day --> Verdict["Santa verdict and restart"]
    Stations -.-> Feedback["Prompts, visuals, and audio"]
    Shipping -.-> Feedback
    Day -.-> Feedback
```

Gameplay rules should remain independent of visual models. A replacement robot mesh should not require changing the shipping rules; a new sound should not require rewriting assembly.

## Feature breakdown

✅ complete · 🟨 implemented, automated tests pass, manual play test pending · ⬜ planned

| Area | Feature | Status |
|---|---|---|
| Player | First-person movement and mouse look | ✅ |
| Interaction | Targeting, prompts, pickup, and drop | ✅ |
| Workshop | Initial graybox room | ✅ |
| Delivery | Repeatable, solvable robot test delivery | ✅ |
| Salvage | Recover installed parts without duplication | ✅ |
| Cleaning | Clean dirty bodies with visible progress | ✅ |
| Assembly | Attach battery and winding key to a robot body | ✅ |
| Shipping | Validate finished toys and award credit once | ✅ |
| Workday | Timer, quota, success, and restart (one repeatable shift) | ✅ |
| Workday | Seven-day Christmas countdown with rising quotas and delivery waves | 🟨 |
| Santa | Verdict messages and placeholder trapdoor punishment | ✅ |
| Co-op | Two-player shared workshop and carrying (part 1) | 🟨 |
| Co-op | Shared stations, shipping and shift (part 2) | 🟨 |
| Co-op | Expand to 2–4 players | ⬜ |
| Art | Finished elf and workshop models | ⬜ |

## Folder layout

Everything lives under `Assets/_Project/`. Runtime code is in the `ElfAndSafety.Runtime` assembly; editor tools are in the separate, editor-only `ElfAndSafety.Editor` assembly; tests are in `ElfAndSafety.Tests`. An audio folder doesn't exist yet and will be added when sound is.

| Path | Contents |
|---|---|
| `Scripts/Runtime/Player/` | `FirstPersonController`, `PlayerInputReader` (bindings, cursor lock) |
| `Scripts/Runtime/Interaction/` | `PlayerInteractor` (aiming, carrying), `Carryable` (single-owner physical object), `IInteractable`, `ICarryableOwner`, `IDescribable` |
| `Scripts/Runtime/Toys/` | `ToyBody` (toy type, dirty state, named part slots), `ToyPart`, `ToySlotTarget` (aim target on a part mount), `ToyRecipe` (toy types, part kinds, wish orders and recipes), `ToyInspection` (shipping rules), `PartMalfunction` (what a faulty part does once installed) |
| `Scripts/Runtime/Stations/` | `WorkStation` (work-area base class), `DeliveryChute`, `DeliveryButton`, `TestDeliveryBatch`, `SalvageBench`, `CleaningStation`, `AssemblyBench`, `ShippingChute`, `ShipmentCounter` |
| `Scripts/Runtime/Workday/` | `WorkdayManager` (current day, shift state, timer, quota, delivery waves, the single verdict, next day and restart), `ChristmasCountdown` (the seven days' tuning), `DeliveryWave` (what one delivery contains, plus the standard Robots, Bears, Cars and Cyborg kit waves), `OrderBoard` (the WISH LIST board), `ShiftButton` (CLOCK IN), `TrapdoorSequence` (failure cutscene) |
| `Scripts/Runtime/UI/` | `InteractionHud` (prompts, hints, status line, feedback), `WorkdayHud` (time, wishes, on-screen wish list, controls hint, verdict panel), `MainMenu`, `PauseMenu`, `SettingsMenu` (the panel both menus use), `GameSettings` (saved settings), `GameLaunch` (moving between the menu and the Workshop, solo/host/join) |
| `Scripts/Runtime/Core/` | `GameLayers`, `WorkshopFeedback` |
| `Scripts/Runtime/Net/` | `NetworkSession` (Host/Join/Leave, creates and destroys the network manager), `NetworkPlayer` (networked elf, carry requests to the host), `NetworkCarryable` (syncs owner state and loose pose), `NetworkToy` (syncs dirty state and the part in each slot), `NetworkWorkshop` (shared shift, timer, count, station state and messages; Play Again for everyone) |
| `Scripts/Editor/` | `WorkshopBuilder` (generates both scenes, the prefabs and the UI), `PlayFromMainMenu` |
| `Tests/` | `RobotRulesTests`, `WorkshopLoopTests`, `WorkdayTests` (PlayMode) |
| `Prefabs/` | `Elf Player` (solo), `Elf Player (Net)` (variant used online), `Network Manager` (instantiated when hosting or joining), `Workshop Sync` (spawned by the host), toys `Robot Body`, `Teddy Bear`, `Toy Car`, parts `Battery`, `Dead Battery`, `Winding Key`, `Stuffing`, `Button Eyes`, `Wheel Set`, and the faulty `Leaky Battery`, `Overwound Winding Key`, `Wobbly Wheel Set`, `Loose Button Eyes`. The three `Placeholder *` prefabs are milestone 1 leftovers and unused |
| `Assets/DefaultNetworkPrefabs.asset` | Netcode's own list of networked prefabs; Netcode keeps it up to date, and the network manager uses only this list |
| `Data/` | `Christmas Countdown.asset` (the seven days: wish orders, shift length, delivery waves, Santa's notes). The old `Test Delivery Batch.asset` is deleted by the builder |
| `Art/Materials/` | Flat-colour URP materials |
| `Scenes/` | `MainMenu.unity` and `Workshop.unity` (both generated) |

### Ownership model

A `Carryable` is in exactly one state at a time: `Loose` (a normal physics object, anywhere), `Held` (a player), `Installed` (a toy), `Reserved` (held still by a station operation, currently only the cleaning scrub) or `Consumed` (shipped or reset). Every transfer goes through one `Set*` method, which first notifies the previous owner (`ICarryableOwner.OnCarryableReleased`) so its reference is cleared. Destroying an object releases its owner the same way, and consuming a toy also consumes its installed parts. Stations never own loose items: each finds the toy resting in its work-area box every time it needs one, so a toy that is knocked or walks off a bench simply stops counting. The owner reference is a plain Unity-serializable field, so it survives a script recompile in Play mode. The state-changing methods (`PlayerInteractor.Request*`, `ToyBody.TryInstall`, `WorkdayManager.TryMatchOrder`, `SalvageBench.TryRemovePart`, `AssemblyBench.TryInstallHeld`, `ShippingChute.TrySubmit`, `DeliveryChute.TryRequestDelivery` / `ResetTestBatch`) are the points a network host will validate in milestone 4.

### Replacing placeholder art

Each toy prefab's root holds the rigidbody, a simple box collider and the gameplay components; its `Visual` child holds the art. Replace the `Visual` child freely. Keep each toy's mount objects (such as `Battery Mount`, `Key Mount`, `Stuffing Mount`, `Front Wheels Mount`): installed parts attach there, their triggers are the aim targets, and `ToyBody`'s slot list points at them. Point `ToyBody`'s **Dirt Visual** field at whatever object shows dirt.

## Key ideas

- **A physical part has one owner.** A component can be loose, held, or installed. Transferring it must remove its previous ownership; it must never leave a duplicate behind.
- **Installed parts are explicit.** A robot's battery and key slots refer to actual components. Avoid separate flags that disagree with what is attached.
- **Stations have clear occupancy.** A station accepts compatible objects, reserves them during an operation, and returns outputs to reachable positions.
- **Shipping is the validation boundary.** Only valid toys earn credit, and each accepted toy can count once. Rejections explain the problem and return the object safely.
- **Reliable interaction comes first.** Use stable carrying and attachment points. Loose objects can use physics, but essential crafting should not depend on unstable joints.
- **Visuals are replaceable.** Keep simple colliders and gameplay components separate from detailed art. Primitive placeholders are enough to prove the loop.
- **Deliveries must be solvable.** The prototype delivery includes enough usable parts for at least two finished robots. Random shortages should not accidentally make the test unwinnable.
- **Co-op changes ownership rules.** Keep pickup, assembly, shipping, and workday changes behind clear methods suitable for host authority. Netcode for GameObjects is the chosen package, with the host as the referee: a client sends requests and mirrors the results. Prove two-player behavior before expanding content.
- **Everyone can do every job.** Fixed character classes are outside the first prototype. Players decide who sorts, cleans, assembles, and ships.

## Toys

| Toy | Part slots | Finished version (what a plain order wants) |
|---|---|---|
| Wind-up robot | Battery, Winding Key | Clean, working battery, winding key |
| Teddy bear | Stuffing, Button Eyes, plus Battery and Winding Key on its back | Clean, stuffing, button eyes, **nothing else** |
| Toy car | Battery, Front Wheels, Rear Wheels | Clean, working battery, a wheel set in both wheel slots |

| Special order | Built from | Needs |
|---|---|---|
| Cyborg bear | A teddy bear | Clean, stuffing, button eyes, working battery, winding key |

Parts: battery, winding key, stuffing, button eyes, wheel set. A part can be good, dead (batteries; obvious) or **faulty** (see below). Parts fit any toy with a matching slot, which is how a bear becomes a cyborg bear. A dead battery can be installed for diagnosis, but it won't pass inspection. Installed parts sit visibly on their mounts and move with the toy. Outside a shift (practice), a toy only has to be its ordinary finished version to ship.

### Faulty parts (malfunctions)

Some deliveries include parts with a hidden defect. They keep their normal names in prompts, so catch them by looking: each has a small visual tell, the look-at line says it "looks rusty / leaking / wobbly / loose", and the assembly bench and toy status show that word in place of "ok". A faulty part never passes inspection ("winding key is faulty"), and only arrives as an extra: a day's deliveries always include enough good parts for its orders.

| Faulty part | Tell | Once it's in a toy | Arrives |
|---|---|---|---|
| Leaky battery | Green goo | Picking up the toy **zaps** you after a moment, and you drop it. Carrying the loose battery on its own is safe | Risky robots wave (days 5 and 7), fitted in the first robot |
| Overwound winding key | Rust | Left loose for 3 seconds, the toy **runs away** in a random direction, again and again until the key is removed | Risky robots wave, fitted in the first robot |
| Wobbly wheel set | One bent wheel | The car **drives off** on its own, like the overwound key | Cars wave, as a loose spare |
| Loose button eyes | A dangling thread, one eye askew | Carry the bear for 2 seconds and the eyes **pop off** | Bears wave, as a loose spare |

To deal with a fitted faulty part: put the toy on the salvage bench (quickly, if it's leaking), remove the part, and fit a good one. Malfunctions run where gameplay is decided (solo, or the co-op host); their results sync to the other player.

### Stations

| Station | Input | Action / output |
|---|---|---|
| Delivery chute | Request button (practice: the day's first wave, one batch at a time) and DEV reset button; during a shift the day's waves arrive on schedule | Toys and loose parts dropped onto the delivery table's six spots; a wave waits while the spots are cluttered |
| Salvage bench | The toy resting in its work area | Aim at an installed part to remove it; it pops out loose at the first clear tray spot, or into the player's hands if all three are blocked |
| Cleaning station | One dirty body | 3-second automatic scrub with progress bar; body locked until clean; clean bodies refused |
| Assembly bench | The toy resting in its work area, then carried parts | Installs the carried part into the first empty slot that takes it (or the slot you aim at); the label lists every slot and says which finished toy it now is |
| Shipping chute | Carried toy | During a shift, fills the matching wish order once; otherwise drops it loose into the reject bin with the reasons (stays in hand if all three drop spots are blocked) |

## How to add things

These are extension guidelines. Replace them with exact file references as systems are implemented.

| I want to add… | Approach |
|---|---|
| A new toy | Add a `ToyType`, a recipe in `ToyRecipe`, a prefab with a `ToyBody` and one mount (with a `ToySlotTarget`) per slot, and register the prefab on the delivery chute (the builder shows how). The stations, shipping and co-op sync already work with any toy of up to 4 slots |
| A new malfunction | Add a `PartFault` (append only), its tell word in `ToyPart.TellWord`, its behaviour in `PartMalfunction`, and a faulty prefab built from the normal part plus a visual tell (see `Faulty(...)` in the builder) |
| A new component | Add a `PartKind` (append only, values are saved in assets), its name in `ToyRecipe.PartName`, a part prefab with a `ToyPart`, and slots for it on the toys that take it |
| A new special order | Add an `OrderKind` and its recipe in `ToyRecipe` (like the cyborg bear), then put it on some days' wish lists |
| A station | Reuse interaction and ownership rules; specify accepted inputs, operation, and accessible output positions |
| A replacement model | Replace the visual child or prefab while preserving gameplay references, attachment points, scale, and colliders |
| A malfunction | Add a readable cause, visible feedback, and a recovery action after the basic loop is stable |
| Santa dialogue | Trigger it from workday events; do not put quota logic inside dialogue code |
| A quota rule | Change workday configuration and test its relationship to deliveries and day length |
| A multiplayer interaction | Resolve ownership and validate the operation on the host; synchronize its result |

## Tuning

Keep relevant values editable rather than scattering unexplained constants through scripts.

| Setting | What it changes | Where |
|---|---|---|
| Movement speed | Player movement | `Elf Player` prefab → First Person Controller |
| Mouse sensitivity, invert Y, field of view, volume, display | Player preferences | In game: **Settings** (saved in PlayerPrefs); defaults in `GameSettings` |
| Interaction reach / carry distance | How players target and transport objects | `Elf Player` prefab → Player Interactor (reach); `Hold Point` child position |
| Cleaning duration | Cleaning workload and station bottlenecks | Workshop scene → Cleaning Station → Work Area → Clean Duration (3 s) |
| Delivery composition | Available bodies, usable components, and repair decisions | `Data/Test Delivery Batch.asset`. Deliveries are refused if it can't complete at least two robots, or has more items than the 6 delivery-table spots |
| Days, wish orders, shift length, delivery waves, Santa's notes | The week's difficulty curve | `Data/Christmas Countdown.asset` (one entry per day; the first is 7 days until Christmas). Each order is a kind (robot, bear, car, cyborg bear) and a kid's name; each wave lists its toys (with their installed parts) and loose parts. A day whose waves can't bring enough bodies and working parts for its orders is refused at clock-in. The builder keeps this asset (it only resets it if it predates wish orders) |
| Santa's lines | Verdict titles and messages | Workday Manager → Santa section |
| Trapdoor timing | How long the failure sequence takes before Retry | Workshop scene → Room → **Trapdoor** → Trapdoor Sequence (gather 0.7 s, open 0.5 s, fall 1.3 s) |
| Time warning | When the timer turns red | HUD → Workday Hud → Warning Seconds (30) |

Values set on scene objects are regenerated by **Build Game Scenes**; prefab and asset values are kept.

Do not balance for four players before validating the local loop and two-player proof. Co-op tuning should consider travel, handoffs, and station congestion as well as player count.

## Art direction

Chunky low-poly shapes, flat colors, readable silhouettes, and minimal surface detail. Cute Christmas decorations share space with hazard stripes, industrial equipment, and a suspiciously warm trapdoor.

- **Elves:** pointed ears, oversized noses, mittens, big boots, expressive simple faces.
- **Toys:** visibly separate components and easily recognized broken states.
- **Stations:** obvious work surfaces, input positions, and output areas.
- **Santa:** cheerful, increasingly threatening management; cartoon scares without realistic gore.

Start with primitives. Final models are not a prerequisite for testing the game.

## Manual checks

Run the checks relevant to the milestone being changed. Planned-system checks become applicable when those systems exist.

- **Foundation:** movement, mouse look, prompts, pickup, and drop work without held objects hitting the player or launching away.
- **Play start:** with `SampleScene` open, pressing Play starts in the workshop.
- **Free physics:** drop robots and parts anywhere with Q; they fall and settle. A robot dropped onto a bench (not placed) is recognised by it; push it off and the bench label goes back to empty.
- **Delivery:** the request button drops six items onto the delivery table once; pressing it again shows a refusal instead of spawning more.
- **Salvage:** the battery and key are aimable on a benched robot, each removes once, and parts pop out onto the green tray. With all three tray spots blocked, the next part goes into your hands, and a further removal is refused with a message.
- **Installed parts:** parts visibly sit on the robot and move with it when carried; they can't be picked up off the robot.
- **Cleaning:** the progress bar fills, dirt smudges disappear, the body can't be taken mid-scrub, and a clean body is refused with "Already clean".
- **Assembly:** the label shows missing parts and dirt; a dead battery installs with a warning and shows DEAD; a second battery is refused with the part kept in hand.
- **Invalid toys:** dirty, incomplete, and dead-battery robots are dumped in the reject bin with the specific reasons.
- **Repair and resubmit:** salvage the dead battery from a rejected robot, clean it, fit a working battery, and ship it.
- **Second robot:** completes and ships; the counter reads 2.
- **Ownership:** spamming E at stations and the chute can't duplicate parts, install two parts in one slot, or add credit. DEV reset mid-cleaning and while carrying a part clears everything, leaves nothing stuck in your hands, and lets a fresh batch be requested.
- **Menus:** Play starts at the main menu. Every button works; Settings opens and Back returns; How to Play shows the controls. Join with a wrong address (e.g. 10.0.0.99) and you're back at the menu with "Couldn't connect to …".
- **Settings:** changing mouse sensitivity, invert Y and field of view takes effect in the Workshop straight away, and survives quitting and restarting. Turning off the wish list and controls hint hides them.
- **Pause:** Esc during a solo shift freezes the timer and toys; Esc or Resume continues with the mouse captured again. Main Menu from pause returns to the title screen.
- **Clock in:** before clocking in, the top of the screen says PRACTICE and the timer doesn't move. Pressing E on CLOCK IN clears practice items and starts a 5:00 countdown with **Shipped 0 / 2**.
- **During the shift:** REQUEST DELIVERY and DEV RESET show "Disabled during a scored shift". A rejected robot leaves the count unchanged.
- **Toys:** on day 2, the bear wave arrives around 1:40. A bear takes stuffing in its belly and button eyes on its face. On day 3 the car wave arrives: wheel sets go into the front and rear wheel slots and the battery on the rear deck. The assembly bench label lists every slot and says READY: teddy bear / toy car when finished.
- **Malfunctions:** on day 5, pick up the first delivered robot. It should zap you out of your hands within a second. Leave it on the floor and within a few seconds it sets off on its own. On the salvage bench, remove its battery and key, and it calms down. Its bench label shows "leaking" and "rusty". On day 2, fit the loose pair of eyes with the dangling thread to a bear and carry it: the eyes pop off. On day 3, fit the bent wheel set to a car and leave it: it drives off.
- **Wish orders:** the WISH LIST board ticks off each order as it ships. On day 5, a plain bear is rejected with "battery is missing, winding key is missing (for …'s cyborg bear)". Fit a battery and key on its back, and it's accepted as a cyborg bear.
- **Win a day:** filling every wish shows Santa's message, tomorrow's quota and **Next Day** at once. You can't move or interact, and the mouse is free to click the button. Next Day (click or R) puts you at the start of day 2 ("6 days until Christmas", quota 3) in practice mode, with an empty delivery table, idle stations, empty hands and Shipped 0, and the mouse captured again.
- **Delivery waves:** on day 2, a second delivery arrives about 1:40 into the shift. If the first delivery's toys are still on the table, it waits until you clear room.
- **Timeout:** with the quota unmet when time runs out (shorten **Shift Length Seconds** for day 1 in `Data/Christmas Countdown.asset` to test quickly), you're pulled onto the trapdoor, the doors open over an orange glow, you fall, and *"You've been reassigned to furnace operations… Back to 7 days until Christmas"* appears with **Back to Day 1**, which reloads at day 1.
- **The whole week:** to check Christmas Eve without playing six days, temporarily set every day's quota to 1 in the countdown asset. Passing day 7 shows MERRY CHRISTMAS! and Play Again.
- **Co-op part 1** (Multiplayer Play Mode, see [Co-op test](#co-op-test-two-players-on-one-pc)):
  - Host Co-op in one window and Join Co-op in the other; each window shows exactly one elf of its own plus the other player's elf.
  - Walking and looking up and down show on the other screen.
  - The host requests a delivery; six toys appear on both screens, with installed parts on the robots.
  - Each player picks up and carries a toy; the other sees it in their hands. Both press E on the same toy at once: only one gets it, and the other's prompt doesn't offer it.
  - Dropping (Q) and placing on a station work for both, and the toy lands in the same spot on both screens.
  - Esc → Main Menu on the host sends both back to the main menu (the client with "The host ended the session"); hosting and joining again works.
- **Co-op part 2** (same setup):
  - Player 2 puts a robot on the salvage bench and removes its battery and key; the parts pop out on both screens, and pressing E again removes nothing more.
  - Player 2 drops a dirty robot in the tub; the progress bar fills on both screens and the body turns clean on both.
  - Player 2 installs parts at the assembly bench; the bench label updates on both screens.
  - Player 2 ships a robot. A valid one raises the count on both screens; an invalid one lands in the reject bin with the reasons shown to both players.
  - Player 2 presses CLOCK IN; the timer and Shipped 0 / 2 appear on both screens. During the shift, REQUEST DELIVERY and DEV RESET refuse for both players.
  - Both players ship robots: the second accepted robot ends the shift once, and both see Santa's message. Play Again from Player 2 resets both into practice mode, still connected.
  - With day 1's Shift Length Seconds set to 30 in the countdown asset, let time run out: both elves fall through the trapdoor, each sees the other fall, and Retry from either player resets both.

## Development workflow

- Work in small milestones that leave a playable result.
- Read the repository's current implementation before introducing another system that solves the same problem.
- Commit Unity `.meta` files alongside their assets. Exclude generated caches, build output, and secrets using a Unity-appropriate `.gitignore`.
- Keep the project's editor version and package changes deliberate and documented.
- Verify compilation and relevant behavior before marking a feature complete. Distinguish automated checks from manual gameplay checks.
- Update this README when controls, setup, structure, or feature status changes. Replace proposed documentation with verified paths and names as the implementation settles.

## Scope boundary

First prove one room, one toy, one workday, and two players. Additional toys, elaborate malfunctions, shop upgrades, cosmetics, procedural maps, and broader progression can follow once that foundation is fun and reliable.
