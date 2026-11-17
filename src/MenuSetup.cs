using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PlayAsHistorical
{
    // Career menu: a large "Play as a historical officer" button over the questionnaire.
    // It opens a setup window in place of the quiz: personality, starting prestige and money.
    // "Done" or "Skip quiz (defaults)" marks the questionnaire finished with no answers.
    // The values are applied when the officer is picked at campaign start, because the
    // vanilla questionnaire evaluation resets prestige, money and personality in the menu.
    internal static class MenuSetup
    {
        internal static readonly string[] Names = { "Loyalty", "Piety", "Social Life", "Heroism", "Stress Control", "Political Views" };
        internal static float[] Personality = { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
        internal static float Prestige = 1000f;
        internal static float Money = 1000f;
        internal static float ExpPercent = 100f;   // share of the officer's recorded experience kept

        static bool open;
        static Rect win;
        static GUIStyle bigButton, note;
        static List<KeyValuePair<int, float[]>> bands;   // personality id → (from, to), names below
        static List<string> bandNames;

        static float BasePrestige => GamePrefs.dlcwl_startupprestige > 0f ? GamePrefs.dlcwl_startupprestige : 1000f;
        const float MaxPrestige = 10000f, MaxMoney = 20000f;

        internal static void SetDefaults()
        {
            for (int i = 0; i < Personality.Length; i++) Personality[i] = 0.5f;
            Prestige = BasePrestige;
            Money = Plugin.StartMoney.Value;
            ExpPercent = 100f;
        }

        static Questionnaire ActiveQuestionnaire()
        {
            var am = MainMenuControls.avatarmanagement_creation;
            if (am == null || !am.gameObject.activeInHierarchy || !am.IsCareerPanelMainMenu) return null;
            var q = am.QuestionnaireRef;
            return q != null && q.gameObject.activeInHierarchy ? q : null;
        }

        internal static void OnGUI()
        {
            var q = ActiveQuestionnaire();
            if (q == null) { open = false; return; }
            if (bigButton == null)
            {
                bigButton = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
                note = new GUIStyle(GUI.skin.box) { fontSize = 18, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            }
            var top = new Rect(Screen.width / 2f - 260, 24, 520, 70);

            if (q.finished)
            {
                open = false;
                if (StartPicker.HistoricalStart)
                    GUI.Box(top, "Historical officer start set. Enter a name and nickname, then Sign up.", note);
                return;
            }

            if (!open)
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
                if (GUI.Button(top, "Play as a historical officer", bigButton))
                {
                    SetDefaults();
                    open = true;
                    win = new Rect(Screen.width / 2f - 280, Screen.height / 2f - 260, 560, 10);
                }
                GUI.backgroundColor = old;
                return;
            }
            win = GUILayout.Window(0x504A, win, Draw, "Historical officer – starting values");
        }

        static void Draw(int _)
        {
            GUILayout.Label("This replaces the questionnaire. You pick the officer when the campaign opens; his attributes come from the game's records.");
            GUILayout.Space(4);
            for (int i = 0; i < Personality.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Names[i], GUILayout.Width(120));
                Personality[i] = Mathf.Round(GUILayout.HorizontalSlider(Personality[i], 0f, 1f, GUILayout.Width(220)) * 20f) / 20f;
                GUILayout.Label(Personality[i].ToString("0.00") + "  " + Band(i, Personality[i]), GUILayout.Width(190));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Prestige", GUILayout.Width(120));
            Prestige = Mathf.Round(GUILayout.HorizontalSlider(Prestige, BasePrestige, MaxPrestige, GUILayout.Width(220)) / 100f) * 100f;
            GUILayout.Label(Prestige.ToString("#,0"), GUILayout.Width(190));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Money", GUILayout.Width(120));
            Money = Mathf.Round(GUILayout.HorizontalSlider(Money, 0f, MaxMoney, GUILayout.Width(220)) / 100f) * 100f;
            GUILayout.Label("$" + Money.ToString("#,0"), GUILayout.Width(190));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Experience", GUILayout.Width(120));
            ExpPercent = Mathf.Round(GUILayout.HorizontalSlider(ExpPercent, 0f, 100f, GUILayout.Width(220)) / 5f) * 5f;
            GUILayout.Label(ExpPercent.ToString("0") + " % of his record", GUILayout.Width(190));
            GUILayout.EndHorizontal();
            GUILayout.Label("Prestige can't go below the base of " + BasePrestige.ToString("#,0") + ", so the start-of-career list is never emptied by it.");
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Done")) Finish();
            if (GUILayout.Button("Skip quiz (defaults)")) { SetDefaults(); Finish(); }
            if (GUILayout.Button("Cancel")) open = false;
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        static void Finish()
        {
            var q = ActiveQuestionnaire();
            open = false;
            if (q == null) return;
            // The vanilla evaluation pairs each queued question with an answer (Questionnaire
            // ConvertAnswersToID); with question 0 queued and no answer it throws every frame.
            q.chosenanswers.Clear();
            q.currentquestionqueue.Clear();
            q.finished = true;
            try { q.ShowFinishScreen(); } catch (Exception e) { Plugin.Log.LogWarning("ShowFinishScreen: " + e.Message); }
            StartPicker.HistoricalStart = true;
            Plugin.Log.LogInfo("Historical start set in the menu: prestige " + Prestige + ", money " + Money
                + ", experience " + ExpPercent + " %, personality " + string.Join(" ", Array.ConvertAll(Personality, v => v.ToString("0.00"))) + ".");
        }

        // Band name from the game's personality lines (personality.dat), e.g. "Contrarian".
        static string Band(int pid, float v)
        {
            if (bands == null) LoadBands();
            for (int i = 0; i < bands.Count; i++)
                if (bands[i].Key == pid && v >= bands[i].Value[0] && v <= bands[i].Value[1]) return bandNames[i];
            return "";
        }

        static void LoadBands()
        {
            bands = new List<KeyValuePair<int, float[]>>();
            bandNames = new List<string>();
            try
            {
                var f = AccessTools.Field(typeof(DLC_WL), "personalitylines");
                if (f.GetValue(null) == null) AccessTools.Method(typeof(DLC_WL), "ImportPersonalityLines").Invoke(null, null);
                if (!(f.GetValue(null) is IEnumerable lines)) return;
                foreach (var l in lines)
                {
                    var t = Traverse.Create(l);
                    bands.Add(new KeyValuePair<int, float[]>(t.Field<int>("personalityid").Value,
                        new[] { t.Field<float>("valuefrom").Value, t.Field<float>("valueto").Value }));
                    bandNames.Add(t.Field<string>("tooltipheadline").Value ?? "");
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Personality names unavailable: " + e.Message); }
        }
    }
}
