using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PlayAsHistorical
{
    // New career as a historical officer.
    //  Menu: a "Skip questionnaire" button marks the questionnaire finished with no answers,
    //        so the vanilla avatar becomes a blank stand-in (base prestige, neutral personality).
    //  Campaign frame 50: instead of the start-of-career unit list, the game stays paused and a
    //        picker opens. Pick an officer in the campaign officer window (or the picker's list).
    //  Pick: you become him in his historical command; the stand-in is retired. A garrison
    //        commander is replaced in his garrison and gets the vanilla start list (rank kept).
    internal static class StartPicker
    {
        internal static bool HistoricalStart;   // set in the menu, used at campaign start
        internal static bool Pending;           // picker open at campaign start
        internal static bool KeepRankOnce;      // next start-list appointment keeps his rank
        internal static int FileCommanders = -1; // officers from commanders.txt (manual avatars come after)
        internal static bool Bypass;

        internal static bool Pickable(int id, bool includeGone)
        {
            var all = GameVars.commander;
            int me = DLC_WL.dlc_chosencommander;
            if (id < 0 || id >= all.Count || id == me || me < 0) return false;
            if (FileCommanders > 0 && id >= FileCommanders) return false;
            var c = all[id];
            if (c.alliance != all[me].alliance || c.status > 0) return false;
            if (c.historicalbranch == 4 || (c.currentcommand != null && c.currentcommand.unittyp == 17)) return false;
            if (!includeGone && !c.IsAvailable(excludestatus: true)) return false;
            return true;
        }

        internal static bool IsGarrison(Regiment r) =>
            r != null && (r.isgarrisonbasicunit || r.garrisonreference != null);

        internal static void Pick(int id, CareerInformationPanel panel)
        {
            var all = GameVars.commander;
            int stub = DLC_WL.dlc_chosencommander;
            var c = all[id];

            if (!c.IsAvailable(excludestatus: true)) c.dateofunavailability = new Tools.Date(0, 0, 0);

            // Retire the stand-in: an exit date before the start hides him from lists and AI picks.
            all[stub].dateofunavailability = new Tools.Date(1, 1, 1850);
            all[stub].activeincampaign = false;

            DLC_WL.dlc_chosencommander = id;
            // The menu's questionnaire evaluation reset these, so apply the player's choices now.
            DLC_WL.prestige = MenuSetup.Prestige;
            DLC_WL.money = MenuSetup.Money;
            if (DLC_WL.personality != null)
                for (int i = 0; i < DLC_WL.personality.Length && i < MenuSetup.Personality.Length; i++)
                    DLC_WL.personality[i] = Mathf.Clamp01(MenuSetup.Personality[i]);

            var cmd = c.currentcommand;
            if (IsGarrison(cmd))
            {
                BattleUnits.ReplaceCommanderOfUnit(cmd);
                if (c.currentcommand == cmd) c.RemoveFromCommand();
                Plugin.Log.LogInfo("Historical start: " + c.combinedname + " left the garrison " + ((UnityEngine.Object)cmd).name + "; now commanded by id " + cmd.commander + ".");
            }

            if (MenuSetup.ExpPercent < 100f)
            {
                float before = c.exp;
                c.exp *= MenuSetup.ExpPercent / 100f;
                Plugin.Log.LogInfo("Historical start: experience " + before.ToString("0.00") + " → " + c.exp.ToString("0.00") + " (" + MenuSetup.ExpPercent + " %).");
            }

            c.refreshcommand = true;
            Refresh.AfterSwitch();
            Pending = false;
            HistoricalStart = false;
            Plugin.Log.LogInfo("Historical start: playing as " + c.combinedname + " (id " + id + "); stand-in id " + stub + " retired.");

            if (c.currentcommand == null && c.GetArmyGroup() == null)
            {
                KeepRankOnce = true;
                Bypass = true;
                try { panel.ShowStartUnitSelectionList(true); } finally { Bypass = false; }
            }
            else
            {
                string video = c.alliance != 0 ? "dlc_intro_csa" : "dlc_intro_union";
                try { MainMenuControls.videoscreen.LoadVideo(video, play: true, overridevideodeactivation: false, manualdrawingorder: 45); }
                catch (Exception e) { Plugin.Log.LogWarning("Intro video failed: " + e.Message); }
            }
        }
    }

    // Count officers read from commanders.txt, before manual avatars and the stand-in are appended.
    [HarmonyPatch(typeof(GameVars), nameof(GameVars.import_commanders))]
    static class CountFileCommanders_Patch
    {
        static void Postfix() { StartPicker.FileCommanders = GameVars.commander?.Count ?? -1; }
    }

    // Frame 50 of a new career: open the picker instead of the start-of-career unit list.
    [HarmonyPatch(typeof(CareerInformationPanel), nameof(CareerInformationPanel.ShowStartUnitSelectionList))]
    static class StartList_Patch
    {
        static bool Prefix(bool newshow)
        {
            if (StartPicker.Bypass || !newshow || !StartPicker.HistoricalStart || !DLC_WL.dlc_scenarioactive) return true;
            if (!CampaignController.newcampaign) { StartPicker.HistoricalStart = false; return true; }
            StartPicker.Pending = true;
            Plugin.Log.LogInfo("Historical start: officer picker opened.");
            return false;
        }
    }

    // A reassigned garrison officer keeps his rank when he takes his first field command.
    [HarmonyPatch(typeof(DLC_WL), nameof(DLC_WL.AppointCommand))]
    static class KeepRank_Patch
    {
        static void Prefix(ref bool resetrank)
        {
            if (StartPicker.KeepRankOnce) { resetrank = false; StartPicker.KeepRankOnce = false; }
        }

        // Units of the new command count as the player's at once, not after the next slow update.
        static void Postfix()
        {
            if (!DLC_WL.dlc_scenarioactive) return;
            BattleUnits.UpdateUnitUnderCommanderAll(false);
            if (BattleUnits.completeunitlist != null)
                foreach (var r in BattleUnits.completeunitlist)
                    if (r != null) r.dlcw_isundercommandercampaign = r.dlcw_isundercommander;
        }
    }

    internal class StartPickerUI : MonoBehaviour
    {
        static readonly AccessTools.FieldRef<CampaignMainPanel, int> chosenRef =
            AccessTools.FieldRefAccess<CampaignMainPanel, int>("currentcommanderchosen");
        CampaignMainPanel mainPanel;
        CampaignController controller;
        CareerInformationPanel careerPanel;
        string search = "";
        bool includeGone;
        int listPick = -1;
        int confirm = -1;
        Vector2 scroll;
        Rect win = new Rect(20, 120, 420, 10);
        List<int> cache;
        string cacheKey;

        void Update()
        {
            if (StartPicker.Pending)
            {
                // Left the campaign? (The main panel can be inactive, so test the controller.)
                if (controller == null) controller = FindObjectOfType<CampaignController>();
                if (!DLC_WL.dlc_scenarioactive || controller == null)
                {
                    Plugin.Log.LogInfo("Historical start: picker closed – campaign no longer running.");
                    StartPicker.Pending = false; StartPicker.HistoricalStart = false; return;
                }
                if (GameVars.gamespeed > 0f) GameVars.SetGameSpeed(0f, overrideminmax: false, manualset: true);
            }
            else if (StartPicker.HistoricalStart)
            {
                // Back in the menu: a normal questionnaire run cancels the skip.
                var am = MainMenuControls.avatarmanagement_creation;
                if (am != null && am.QuestionnaireRef != null && am.QuestionnaireRef.chosenanswers != null && am.QuestionnaireRef.chosenanswers.Count > 0)
                    StartPicker.HistoricalStart = false;
            }
        }

        void OnGUI()
        {
            if (StartPicker.Pending) { win = GUILayout.Window(0x5049, win, DrawPicker, "Choose your officer"); return; }
            MenuSetup.OnGUI();
        }

        static string Rank(GameVars.Commander c) =>
            c.rank >= 0 && c.rank < GameVars.ranktext.Length ? GameVars.ranktext[c.rank] : "";

        static string Line(int id)
        {
            var c = GameVars.commander[id];
            var cmd = c.currentcommand;
            string where = cmd == null ? (c.GetArmyGroup() != null ? "army group" : "no command")
                : ((UnityEngine.Object)cmd).name + (StartPicker.IsGarrison(cmd) ? " (garrison)" : "");
            return Rank(c) + " " + c.combinedname + " – " + where;
        }

        List<int> Candidates()
        {
            string key = search + "|" + includeGone + "|" + GameVars.commander.Count;
            if (cache != null && key == cacheKey) return cache;
            string s = search.Trim().ToLowerInvariant();
            cache = Enumerable.Range(0, GameVars.commander.Count)
                .Where(i => StartPicker.Pickable(i, includeGone)
                    && (s.Length == 0 || GameVars.commander[i].combinedname.ToLowerInvariant().Contains(s)))
                .OrderByDescending(i => GameVars.commander[i].rank)
                .ThenBy(i => GameVars.commander[i].name)
                .ToList();
            cacheKey = key;
            return cache;
        }

        void DrawPicker(int _)
        {
            if (mainPanel == null) mainPanel = FindObjectOfType<CampaignMainPanel>(true);
            if (careerPanel == null) careerPanel = FindObjectOfType<CareerInformationPanel>(true);

            if (confirm >= 0) { DrawConfirm(); GUI.DragWindow(); return; }

            GUILayout.Label("Pick an officer in the campaign officer window, or from this list. The game stays paused until you choose.");
            int winPick = mainPanel != null ? chosenRef(mainPanel) : -1;
            if (StartPicker.Pickable(winPick, true))
            {
                GUILayout.Label("Selected in the officer window: " + Line(winPick));
                if (GUILayout.Button("Play as him")) confirm = winPick;
            }
            else GUILayout.Label("Nothing selected in the officer window.");

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(50));
            search = GUILayout.TextField(search ?? "");
            GUILayout.EndHorizontal();
            includeGone = GUILayout.Toggle(includeGone, "Include officers who had left the war before this date");

            var list = Candidates();
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(260));
            foreach (int id in list.Take(200))
                if (GUILayout.Toggle(listPick == id, Line(id), "button")) listPick = id;
            if (list.Count > 200) GUILayout.Label("… " + (list.Count - 200) + " more – narrow the search.");
            GUILayout.EndScrollView();
            GUI.enabled = list.Contains(listPick);
            if (GUILayout.Button("Play as the officer picked in this list")) confirm = listPick;
            GUI.enabled = true;
            GUI.DragWindow();
        }

        void DrawConfirm()
        {
            var c = GameVars.commander[confirm];
            GUILayout.Label("Start your career as " + Line(confirm) + "?");
            if (!c.IsAvailable(excludestatus: true))
                GUILayout.Label("• Historically he had left the war by " + Tools.GetDateString(c.dateofunavailability.day, c.dateofunavailability.month, c.dateofunavailability.year) + ". He will be made available.");
            if (StartPicker.IsGarrison(c.currentcommand))
                GUILayout.Label("• He commands a garrison. Someone else takes it over, and you choose a field command next. You keep his rank.");
            else if (c.currentcommand == null && c.GetArmyGroup() == null)
                GUILayout.Label("• He has no command. You choose one next. You keep his rank.");
            GUILayout.Label("Experience " + Mathf.RoundToInt(c.exp * 100f) + (MenuSetup.ExpPercent < 100f ? " → " + Mathf.RoundToInt(c.exp * MenuSetup.ExpPercent) : "") + ". Prestige " + Mathf.RoundToInt(MenuSetup.Prestige) + ", money " + Mathf.RoundToInt(MenuSetup.Money) + ", personality as set in the menu.");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start"))
            {
                int id = confirm;
                confirm = -1;
                if (careerPanel == null) { Plugin.Log.LogError("Historical start: career panel not found."); return; }
                try { StartPicker.Pick(id, careerPanel); }
                catch (Exception e) { Plugin.Log.LogError("Historical start failed: " + e); }
            }
            if (GUILayout.Button("Back")) confirm = -1;
            GUILayout.EndHorizontal();
        }
    }
}
