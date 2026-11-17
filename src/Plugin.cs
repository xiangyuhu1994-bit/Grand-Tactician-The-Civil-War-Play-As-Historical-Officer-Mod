using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PlayAsHistorical
{
    // Career mode: take over a historical officer in the middle of a career.
    // Select him in the campaign officer window, press the hotkey, confirm.
    // The career (prestige, money, companions, biography, relations) carries over;
    // the old avatar stays in the army as an ordinary officer.
    [BepInPlugin(Guid, "Play As Historical", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "aom.playashistorical";
        public const string Version = "1.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> TakeOverKey;
        internal static ConfigEntry<KeyboardShortcut> ShowGoneKey;
        internal static ConfigEntry<bool> ResetPersonality;
        internal static ConfigEntry<float> StartMoney;
        internal static bool ShowGone;

        void Awake()
        {
            Log = Logger;
            TakeOverKey = Config.Bind("General", "TakeOverKey", new KeyboardShortcut(KeyCode.H, KeyCode.LeftControl, KeyCode.LeftShift),
                "Open the take-over window for the officer selected in the campaign officer window.");
            ShowGoneKey = Config.Bind("General", "ShowGoneOfficersKey", new KeyboardShortcut(KeyCode.G, KeyCode.LeftControl, KeyCode.LeftShift),
                "Toggle listing officers who had left the war before this scenario's start date (career mode only).");
            ResetPersonality = Config.Bind("General", "ResetPersonality", true,
                "On take-over, set the six personality values to neutral (0.5, the game's own default) and weight to the career start weight.");
            StartMoney = Config.Bind("HistoricalStart", "StartMoney", 1000f,
                "Money when you start a career as a historical officer (the questionnaire is skipped, so AOM's base of 100 would apply).");
            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            gameObject.AddComponent<StartPickerUI>();
            Log.LogInfo("Play As Historical " + Version + " loaded.");
        }

        // ---- selection -------------------------------------------------------

        static readonly AccessTools.FieldRef<CampaignMainPanel, int> chosenRef =
            AccessTools.FieldRefAccess<CampaignMainPanel, int>("currentcommanderchosen");
        CampaignMainPanel panel;

        int SelectedOfficer()
        {
            if (panel == null) panel = FindObjectOfType<CampaignMainPanel>(true);
            int id = panel != null ? chosenRef(panel) : -1;
            if (id < 0)
            {
                var list = FindObjectOfType<CommandersList>();
                if (list != null) id = list.selectedcommanderid;
            }
            return id;
        }

        // ---- checks ----------------------------------------------------------

        // Returns a reason he can't be taken over, or null. Fills warnings for the confirm window.
        internal static string Check(int id, List<string> warn)
        {
            var all = GameVars.commander;
            int me = DLC_WL.dlc_chosencommander;
            if (all == null || me < 0 || me >= all.Count) return "No career is running.";
            if (id < 0 || id >= all.Count) return "Select an officer in the officer window first.";
            if (id == me) return "You already play as him.";
            var c = all[id];
            if (c.alliance != all[me].alliance) return "Pick an officer from your own side.";
            if (c.status > 0) return "He is not fit for duty (wounded, captured, dead or on leave).";
            var cmd = c.currentcommand;
            if (c.historicalbranch == 4 || (cmd != null && cmd.unittyp == 17))
                return "He is a naval officer. Career mode has no naval career.";
            if (cmd != null && (cmd.isgarrisonbasicunit || cmd.garrisonreference != null))
                return "He commands a garrison. Career mode never offers garrison commands, so that state is untested.";

            if (!c.IsAvailable(excludestatus: true))
                warn.Add("Historically he had left the war by " + DateText(c.dateofunavailability) + ". He will be made available.");
            if (cmd == null && c.GetArmyGroup() == null)
                warn.Add("He has no command right now. Career command offers will follow as usual.");
            if (DLC_WL.IsCommanderInChief(id))
                warn.Add("He is general-in-chief. Career mode handles this, but normal careers rarely get there.");
            else if (c.GetArmyGroup() != null || (cmd != null && cmd.unittyp >= 15))
                warn.Add("Corps command or higher. Normal careers reach this only through promotion.");
            return null;
        }

        static string DateText(Tools.Date d) =>
            d == null || d.year <= 0 ? "?" : Tools.GetDateString(d.day, d.month, d.year);

        static string RankText(GameVars.Commander c)
        {
            var t = (c.historicalbranch == 4 ? GameVars.navyranktext : GameVars.ranktext);
            return t != null && c.rank >= 0 && c.rank < t.Length ? t[c.rank] : "";
        }

        // ---- take-over -------------------------------------------------------

        internal static void TakeOver(int id)
        {
            var all = GameVars.commander;
            int old = DLC_WL.dlc_chosencommander;
            var c = all[id];

            if (!c.IsAvailable(excludestatus: true)) c.dateofunavailability = new Tools.Date(0, 0, 0);

            DLC_WL.dlc_chosencommander = id;

            if (ResetPersonality.Value && DLC_WL.personality != null)
            {
                for (int i = 0; i < DLC_WL.personality.Length; i++) DLC_WL.personality[i] = 0.5f;
                DLC_WL.weight = GamePrefs.dlcwl_startupweight;
            }

            // What AppointCommand does when the player's command changes.
            if (DLC_WL.staffcorps != null)
                for (int i = 0; i < DLC_WL.staffcorps.Length; i++)
                    if (DLC_WL.staffcorps[i] >= 0) DLC_WL.staffcorps[i] = -1;
            DLC_WL.currentcommandoffer = null;

            c.refreshcommand = true;
            if (old >= 0 && old < all.Count) all[old].refreshcommand = true;
            Refresh.AfterSwitch();

            Log.LogInfo("Career taken over: now playing as " + c.combinedname + " (id " + id + "), previously id " + old + ".");
        }

        // ---- input and UI ----------------------------------------------------

        int pending = -1;
        string pendingError;
        readonly List<string> pendingWarn = new List<string>();
        string toast;
        float toastUntil;
        Rect win = new Rect(0, 0, 460, 10);

        void Toast(string s) { toast = s; toastUntil = Time.unscaledTime + 5f; }

        void Update()
        {
            if (!DLC_WL.dlc_scenarioactive) { pending = -1; return; }
            if (ShowGoneKey.Value.IsDown())
            {
                ShowGone = !ShowGone;
                Toast(ShowGone
                    ? "Officer list now includes officers who had left before this scenario. Reopen the list to refresh."
                    : "Officer list back to normal. Reopen the list to refresh.");
            }
            if (TakeOverKey.Value.IsDown())
            {
                pendingWarn.Clear();
                int id = SelectedOfficer();
                pendingError = Check(id, pendingWarn);
                pending = id;
                win = new Rect((Screen.width - 460) / 2f, Screen.height / 3f, 460, 10);
            }
        }

        void OnGUI()
        {
            if (toast != null && Time.unscaledTime < toastUntil)
                GUI.Box(new Rect(Screen.width / 2f - 300, 40, 600, 28), toast);
            if (pending == -1 || !DLC_WL.dlc_scenarioactive) return;
            win = GUILayout.Window(0x5048, win, DrawWindow, "Play as historical officer");
        }

        void DrawWindow(int _)
        {
            var all = GameVars.commander;
            if (pendingError != null)
            {
                GUILayout.Label(pendingError);
                if (GUILayout.Button("Close")) pending = -1;
                return;
            }
            var c = all[pending];
            var cmd = c.currentcommand;
            GUILayout.Label("Play the rest of this career as " + RankText(c) + " " + c.combinedname + "?");
            GUILayout.Label(cmd != null ? "Command: " + ((UnityEngine.Object)cmd).name : "Command: none");
            GUILayout.Label("Your prestige, money, companions and relations carry over. "
                + all[DLC_WL.dlc_chosencommander].combinedname + " stays in the army as an ordinary officer.");
            if (ResetPersonality.Value) GUILayout.Label("Personality is set to neutral and weight to the starting weight.");
            foreach (var w in pendingWarn) GUILayout.Label("• " + w);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Take over"))
            {
                try { TakeOver(pending); Toast("You now play as " + c.combinedname + "."); }
                catch (Exception e) { Log.LogError("Take-over failed: " + e); Toast("Take-over failed – see the BepInEx log."); }
                pending = -1;
            }
            if (GUILayout.Button("Cancel")) pending = -1;
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }
    }

    // While ShowGone is on (career only), the officer list keeps officers whose
    // historical exit date is before the scenario start.
    // Matches:  callvirt GameVars/Commander::IsAvailable(bool)   in CommandersList.UpdateList
    // Becomes:  call     ListAvailable_Patch::Available(Commander, bool)
    [HarmonyPatch(typeof(CommandersList), nameof(CommandersList.UpdateList))]
    static class ListAvailable_Patch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
        {
            var target = AccessTools.Method(typeof(GameVars.Commander), nameof(GameVars.Commander.IsAvailable));
            var helper = AccessTools.Method(typeof(ListAvailable_Patch), nameof(Available));
            int n = 0;
            foreach (var ins in code)
            {
                if ((ins.opcode == OpCodes.Callvirt || ins.opcode == OpCodes.Call) && Equals(ins.operand, target))
                {
                    ins.opcode = OpCodes.Call;
                    ins.operand = helper;
                    n++;
                }
                yield return ins;
            }
            Plugin.Log?.LogInfo("Officer list availability hook: " + n + " site(s).");
        }

        static bool Available(GameVars.Commander c, bool excludestatus) =>
            (Plugin.ShowGone && DLC_WL.dlc_scenarioactive && excludestatus) || c.IsAvailable(excludestatus);
    }
}
