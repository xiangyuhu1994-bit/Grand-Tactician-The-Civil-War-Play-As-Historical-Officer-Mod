# How it works

Notes for anyone changing the mod. Line numbers refer to a decompile of the game's `Assembly-CSharp.dll` and are approximate.

## The player is an index

Career mode identifies the player only by `DLC_WL.dlc_chosencommander`, an index into `GameVars.commander`. Everything player-specific keys off that index: `Regiment.UpdateCommanderCasualtyState` (the player is only ever wounded, never killed), `BattleUnits.IsUnitUnderCommander`, `DLC_WL.IsCommanderInChief`, command offers, promotions. There is no separate "is player" flag on the officer.

The index is saved as the first line of `DLC_WL.dat` and restored by `DLC_WL.GetDLCCommanderID` on load, so a switch persists with no extra file.

Career-only state is static on `DLC_WL`, not on the officer: `personality[6]` (Loyalty, Piety, Social Life, Heroism, Stress Control, Political Views; 0–1, default 0.5), `weight`, `fitness`, `prestige`, `money`, companions, biography, staff. The officer's attributes (experience, fame, initiative, leadership, administration, cunning) and flags (veteran, West Point, political, branch) are ordinary `Commander` fields, the same for avatars and historical officers.

## New career (`StartPicker.cs`, `MenuSetup.cs`)

1. **Menu.** `MenuSetup` draws a button over `MainMenuControls.avatarmanagement_creation` while its questionnaire is open. Done/Skip clears `Questionnaire.chosenanswers` **and** `currentquestionqueue`, then sets `finished`. Both lists must be cleared: `SavedQuestionnaires.ConvertAnswersToID` pairs each queued question with an answer, and with question 0 queued and no answer it throws on every frame, which stops `AvatarManagement.Update` before it enables Sign up.
2. **Values.** `AvatarManagement.EvaluateQuestionnaire` calls `DLC_WL.ResetEnhancedData`, which resets prestige (AOM `dlcwl_startupprestige` 1,000), money (AOM 100), personality and weight. So the chosen values are kept by the mod and applied at pick time.
3. **Campaign start.** `CampaignController` frame 0 adds the avatar (`AddHandoverCommanderToDatabase`) as the last officer and points the index at it. Frame 50 calls `CareerInformationPanel.ShowStartUnitSelectionList`; a prefix replaces it with the picker while a historical start is pending. The game stays paused (`GameVars.SetGameSpeed(0)` each frame). "Still in the campaign" is tested with `CampaignController`, because `CampaignMainPanel` can be inactive.
4. **Pick.** The stand-in avatar gets an exit date in 1850, which hides it from lists and from AI replacement (`Commander.IsAvailable`, `CanBePickedForAIReplacement`). The index moves to the chosen officer. A garrison commander is replaced with `BattleUnits.ReplaceCommanderOfUnit` (`GetSuitableCommander` excludes the player). If he then has no command, the vanilla start list opens; a prefix on `DLC_WL.AppointCommand` keeps his rank once (the start list normally passes `resetrank: true`).
5. **Officer list.** `GameVars.import_commanders` is counted in a postfix so the picker can leave out manual avatars appended after the file's officers.

## Mid-career take-over (`Plugin.cs`)

Reads the officer selected in `CampaignMainPanel.currentcommanderchosen` (fallback `CommandersList.selectedcommanderid`). Like `AppointCommand`, it clears `staffcorps` and `currentcommandoffer`. Ctrl+Shift+G replaces the `IsAvailable(true)` call in `CommandersList.UpdateList` so the list includes officers whose exit date is before the scenario start.

## Refresh after a switch (`Refresh.cs`)

The game works these out once and caches them:

| Cache | Normally refreshed | Mod |
|---|---|---|
| `Regiment.dlcw_isundercommander` / `…campaign` | each regiment's slow periodic update | `BattleUnits.UpdateUnitUnderCommanderAll` + copy to the campaign flag; also after every `AppointCommand` |
| `Autocalc.hasplayercontrolledunits` (decides **Play manually**) | only on certain engagement events | `UpdatePlayerControlledUnits` on every running engagement after a switch, and in a prefix on `AutoresolvePanel.UpdateSiegeButtons` (open, every 50 frames, stance change) |
| `Autocalc.allplayerunitsabsent` | set, never cleared | cleared before each recheck |
| Hall of fame (`DLCWL_Commander.rankingentry`) | `ImportEntries`, adds a row for the current index | re-imported; a postfix drops the historical row (AOM `dlcwl_rankings.dat`) for the player's own officer |
| Career button (`AvatarButton`) | portrait, tooltip, money, prestige only at frame 50 | the same six private updates rerun |
| Career window portrait (`AvatarManagement.ApplyVariableToCommanderData`) | avatar portrait picker, which has no entry for historical officers | postfix sets `Commander.LoadCommanderSprite` when `portraitfilename` is empty |

## Facts behind the design

- **Exit dates** (`dateofunavailability`) only hide officers when a scenario starts after them (`Commander.IsAvailable` compares with `BattleUnits.scenariostartdate`). Nothing removes an officer when the date passes in play. AOM's `commanders.txt`: 1,905 officers, 542 with an exit date inside the war (352 Union, 190 CSA), 13 before it (mostly placeholder and duplicate records).
- **Start list** (`DLC_WL.GetUnitListForStartSelection`): unit types 0–2 and divisions (14), never garrisons, only units whose prestige cost is at most your prestige. If it is empty, **Continue** does nothing, hence the prestige floor of 1,000.
- **Nickname** (`DLC_WL.GetNickname`) shows only at fame ≥ AOM `dlcwl_fametriggernickname` (0.6).
- **Career commands:** unit types 14 division, 15 corps, 16 army, 17 fleet; army groups count as 18. Career code has paths for corps, army, army group and general-in-chief. It never offers garrisons or fleets.
