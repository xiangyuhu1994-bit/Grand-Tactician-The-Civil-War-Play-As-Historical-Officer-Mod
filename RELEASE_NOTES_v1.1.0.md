Play As Historical 1.1.0 – play career mode as a historical officer. First release, alpha.

Start a new career as any officer of your side – Sherman, Lee, or a colonel nobody remembers – in his historical command and with his own attributes. Or take one over in a career you're already playing. It works on its own and does not need bugFixMod.

## New career

- **A setup window replaces the questionnaire.** Press the yellow **Play as a historical officer** button in the career menu. Set the six personality values, starting prestige and money, and how much of the officer's recorded experience he keeps. Or press **Skip quiz** for neutral personality, 1,000 prestige, $1,000 and full experience.
- **Pick your officer when the campaign opens.** The game stays paused. Click an officer in the officer window, or use the picker's searchable list.
- **Garrison commanders and officers without a command** can be picked too. A garrison is handed to another officer, and you choose a field command from the normal start list. You keep his rank.
- **Officers who had left the war before the start date** can be listed and picked; their exit date is cleared.

## Mid-career take-over

Select an officer in the officer window and press **Ctrl+Shift+H**. Your prestige, money, companions and relations carry over; your old avatar stays in the army as an ordinary officer.

## Also in every career

The engagement window now rechecks whether you have units in the battle each time it updates. In vanilla, an engagement already running when your command changed could say none of your units were in it, and once all your units had been absent, **Play manually** stayed off even after they arrived.

## Install

Unzip `PlayAsHistorical.zip` into the game folder so the DLL ends up in `BepInEx/plugins/` (BepInEx 5 required). `BepInEx/LogOutput.log` should contain "Play As Historical 1.1.0 loaded."

## Status

Alpha. New careers were tested in game with Sherman and Lee; the latest fixes (career button portrait, **Play manually** in an engagement that was already running) and the mid-career take-over haven't been tested in game yet. Every patch applies in the headless test harness (7 of 7). Known issue: if you save and reload before picking your officer, you get the normal start list for the stand-in avatar instead of the picker. The mod's windows use Unity's basic overlay, so they look plainer than the game's. Back up your saves, and report problems with your `LogOutput.log` attached.
