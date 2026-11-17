# Play As Historical

A BepInEx 5 plugin for Grand Tactician: The Civil War. It lets you play career mode as a historical officer, either from the start of a new career or by taking one over mid-career. It works on its own and does not need bugFixMod. It was built against the AOM data mod and should work with vanilla data too.

**Status: alpha.** Tested in game for new careers (Sherman, Lee). Please report problems with your `BepInEx/LogOutput.log` attached.

## New career as a historical officer

1. In the career menu, choose your side, then press the yellow **Play as a historical officer** button at the top.
2. A setup window replaces the questionnaire. Set:
   - the six personality values (each shows the game's label for that level, e.g. "Contrarian");
   - starting prestige (1,000–10,000);
   - starting money ($0–20,000);
   - how much of the officer's recorded experience he keeps (0–100 %).

   Press **Done**, or **Skip quiz (defaults)** for neutral personality, 1,000 prestige, $1,000 and full experience.
3. Type any name and nickname so the game lets you sign up. The name is thrown away. The nickname is kept and shows in career texts once your fame reaches 0.6, as in any career.
4. Sign up. When the campaign opens, the game stays paused and a picker opens instead of the start-of-career unit list.
5. Click an officer in the campaign officer window, or pick one from the picker's searchable list. Press **Play as him**, then **Start**.

You become him in his historical command, with his attributes and your chosen personality, prestige, money and experience. Weight, fitness and the basic uniform for your side are the game's defaults. The stand-in avatar is retired and never appears.

- **Garrison commanders** can be picked. Another officer takes over the garrison, and you choose a field command from the game's normal start list. You keep his rank.
- **Officers with no command** get the same start list, also keeping their rank.
- **Officers who had left the war before the start date:** tick the box in the picker to list them. Picking one clears his exit date.
- Naval officers, the other side's officers, and officers who are wounded, captured or dead can't be picked.

Prestige can't be set below AOM's base of 1,000, so the start list is never left empty for lack of prestige.

## Take over mid-career

1. Start or load a career.
2. Open the campaign officer window and click the officer you want.
3. Press **Ctrl+Shift+H**. A window shows who he is, his command, and any warnings.
4. Press **Take over**.

Your prestige, money, companions, biography and relations carry over. Your old avatar stays in the army as an ordinary officer. The change is saved with the game; nothing extra is written.

By default, personality is set to neutral (0.5, the game's own default) and weight to the career start weight. Set `ResetPersonality = false` to keep the avatar's.

The officer list hides officers whose historical exit date is before the scenario's start date. Press **Ctrl+Shift+G** to include them, then reopen the list. Exit dates only matter at scenario start: the game does not remove officers when their exit date passes during a campaign.

Mid-career take-over is refused for officers of the other side; officers who are wounded, captured, dead or on leave; naval officers; and garrison commanders (career mode never offers garrison commands, so that state is untested). Corps, army and general-in-chief commands are allowed, with a warning.

## How it works

In career mode the game knows the player only as an index into its list of officers (`DLC_WL.dlc_chosencommander`). Wound rules, units under your command, the commander-in-chief role and so on all follow that index. The mod points it at the historical officer, then refreshes the things the game works out only once:

- which units are under your command, for both battle and campaign screens;
- whether running engagements have units of yours in them, which decides whether **Play manually** is offered;
- the hall of fame row for you (and the historical row for the same officer is dropped, so he isn't listed twice);
- the career button and career window portrait, tooltip, money and prestige.

Technical details are in [`notes/how_it_works.md`](notes/how_it_works.md).

## Compatibility

- Non-career games and normal careers: nothing changes until you press the menu button or a hotkey. One exception: in every career, the engagement window now rechecks whether you have units in the battle each time it updates. This fixes two vanilla bugs: an engagement already running when your command changes can say you have no units in it, and once all your units were absent, **Play manually** stayed off even after they arrived.
- Works alongside bugFixMod. Neither depends on the other.

## Known issues

- If you save and reload before picking your officer, the game shows the normal start list for the stand-in avatar instead of the picker.
- The setup window, picker and take-over window are drawn with Unity's basic overlay, so they look plainer than the game's own windows.

## Config

`BepInEx/config/aom.playashistorical.cfg`:

| Setting | Default | Meaning |
|---|---|---|
| TakeOverKey | Ctrl+Shift+H | Open the take-over window for the selected officer |
| ShowGoneOfficersKey | Ctrl+Shift+G | Toggle listing officers who had left before the scenario start |
| ResetPersonality | true | Neutral personality and start weight on mid-career take-over |
| StartMoney | 1000 | Default money in the historical-officer setup window |

## Install

1. Install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases) (x64) into the game folder if you haven't already.
2. Unzip `PlayAsHistorical.zip` from the release into the game folder, so the DLL ends up in `BepInEx/plugins/`.
3. Start the game. `BepInEx/LogOutput.log` should contain "Play As Historical 1.1.0 loaded."

To remove it, delete `BepInEx/plugins/PlayAsHistorical.dll`. Careers already started as a historical officer keep working: the game saves who you are by itself.

## Build

Put the game's `Assembly-CSharp.dll` (from `Grand Tactician_Data/Managed/`) in `lib/`, then run `dotnet build -c Release`. Or point the build at the game folder: `dotnet build -c Release -p:GameManaged="<game>/Grand Tactician_Data/Managed"`. Never commit game files; `lib/` is git-ignored.

To check that every patch applies without starting the game, see [`tools/README.md`](tools/README.md).

## License

MIT – see [`LICENSE`](LICENSE).
