using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PlayAsHistorical
{
    // Everything that caches "who the player is" and must be refreshed after
    // DLC_WL.dlc_chosencommander changes.
    internal static class Refresh
    {
        internal static void AfterSwitch()
        {
            // Units under the player: battle flag and campaign flag (the campaign one is
            // otherwise only refreshed in each regiment's periodic update).
            BattleUnits.UpdateUnitUnderCommanderAll(false);
            if (BattleUnits.completeunitlist != null)
                foreach (var r in BattleUnits.completeunitlist)
                    if (r != null) r.dlcw_isundercommandercampaign = r.dlcw_isundercommander;

            // Running engagements cache whether the player has units in them; that decides
            // whether "Play manually" is offered.
            Try("engagements", () =>
            {
                var m = AccessTools.Method(typeof(Autocalc), "UpdatePlayerControlledUnits");
                int n = 0;
                foreach (var a in UnityEngine.Object.FindObjectsOfType<Autocalc>()) { m.Invoke(a, null); n++; }
                Plugin.Log.LogInfo("Refreshed player units in " + n + " engagement(s).");
            });

            // Hall of fame row for the player (built from dlc_chosencommander).
            Try("hall of fame", DLCWL_Commander.ImportEntries);

            // Career button (top left): vanilla fills portrait, tooltip, money and prestige
            // only at campaign frame 50, i.e. before the pick. Run the same updates again.
            Try("career button", () =>
            {
                string[] steps = { "UpdatePortrait", "UpdateMoneyAndPrestige", "UpdateTooltip",
                                   "UpdateMoveToCharacterButton", "UpdateCurrentOrderButton", "UpdateSickWounded" };
                foreach (var b in UnityEngine.Object.FindObjectsOfType<AvatarButton>(true))
                    foreach (var name in steps)
                        AccessTools.Method(typeof(AvatarButton), name)?.Invoke(b, null);
            });
        }

        static void Try(string what, Action a)
        {
            try { a(); } catch (Exception e) { Plugin.Log.LogWarning("Refresh " + what + " failed: " + e.Message); }
        }
    }

    // Hall of fame: AOM's list contains historical officers. When the player is one of them,
    // keep only the player's own row (added last by ImportEntries), not the historical one too.
    [HarmonyPatch(typeof(DLCWL_Commander), nameof(DLCWL_Commander.ImportEntries))]
    static class HallOfFameDuplicate_Patch
    {
        static readonly AccessTools.FieldRef<List<DLCWL_Commander.RankingEntry>> entries =
            AccessTools.StaticFieldRefAccess<List<DLCWL_Commander.RankingEntry>>(AccessTools.Field(typeof(DLCWL_Commander), "rankingentry"));

        static void Postfix()
        {
            var list = entries();
            int me = DLC_WL.dlc_chosencommander;
            if (list == null || me < 0) return;
            int last = list.FindLastIndex(e => e.commanderid == me);
            for (int i = last - 1; i >= 0; i--)
                if (list[i].commanderid == me)
                {
                    list[i].RemoveObject();
                    list.RemoveAt(i);
                }
        }
    }

    // In-campaign career window: its portrait comes from the avatar portrait picker, which has
    // no entry for a historical officer, so it kept showing the stand-in's picture.
    [HarmonyPatch(typeof(AvatarManagement), "ApplyVariableToCommanderData")]
    static class CareerPortrait_Patch
    {
        static void Postfix(AvatarManagement __instance, GameVars.Commander commander)
        {
            if (commander == null || !__instance.IsCareerPanelSessions) return;
            if (!string.IsNullOrEmpty(commander.portraitfilename)) return; // an avatar: the picker handles it
            int id = GameVars.commander.IndexOf(commander);
            if (id < 0) return;
            var s = commander.LoadCommanderSprite(id);
            // Via reflection so the mod needn't reference UnityEngine.UI.
            var img = Traverse.Create(__instance).Field("PortraitImage");
            if (s != null && img.GetValue() != null) img.Property("sprite").SetValue(s);
        }
    }
}

namespace PlayAsHistorical
{
    // An engagement works out "does the player have units here" only on certain events, so an
    // engagement already running when the player's command changes keeps the old answer
    // ("none of your own units … are under your control"). Re-check whenever the engagement
    // panel updates its buttons (on open, every 50 frames, on stance change).
    [HarmonyLib.HarmonyPatch(typeof(AutoresolvePanel), nameof(AutoresolvePanel.UpdateSiegeButtons))]
    static class EngagementPlayerUnits_Patch
    {
        static readonly System.Reflection.MethodInfo update =
            HarmonyLib.AccessTools.Method(typeof(Autocalc), "UpdatePlayerControlledUnits");

        static void Prefix(Autocalc ___autocalcref)
        {
            if (!DLC_WL.dlc_scenarioactive) return;
            var a = ___autocalcref;
            if (a == null) return;
            // Vanilla only ever sets allplayerunitsabsent (never clears it), so a re-check would
            // latch "all your units are absent" for good. Evaluate it fresh each time.
            a.allplayerunitsabsent = false;
            try { update.Invoke(a, null); }
            catch (System.Exception e) { Plugin.Log.LogWarning("Engagement refresh failed: " + e.Message); }
        }
    }
}
