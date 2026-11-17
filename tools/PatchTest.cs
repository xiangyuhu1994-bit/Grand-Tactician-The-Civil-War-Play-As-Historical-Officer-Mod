// Headless patch test: applies every Play As Historical Harmony patch to the game's real
// Assembly-CSharp under Mono with the HarmonyX/MonoMod that BepInEx 5 ships.
using System;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

static class Harness
{
    static int Main()
    {
        var mod = Assembly.LoadFrom("PlayAsHistorical.dll");
        var log = new ManualLogSource("PlayAsHistorical");
        log.LogEvent += (s, e) => Console.WriteLine("[" + e.Level + "] " + e.Data);
        mod.GetType("PlayAsHistorical.Plugin")
           .GetField("Log", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
           .SetValue(null, log);

        var harmony = new Harmony("aom.playashistorical.test");
        int ok = 0, failed = 0;
        foreach (var type in AccessTools.GetTypesFromAssembly(mod))
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
            try
            {
                var patched = harmony.CreateClassProcessor(type).Patch();
                ok++;
                Console.WriteLine("OK   " + type.Name + " (" + (patched?.Count ?? 0) + " method(s))");
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine("FAIL " + type.Name + ":\n" + e);
            }
        }

        // The officer-list transpiler must replace exactly one IsAvailable call.
        var list = AccessTools.Method(typeof(CommandersList), "UpdateList");
        var code = PatchProcessor.GetCurrentInstructions(list);
        int helper = code.Count(i => i.operand is MethodInfo m && m.Name == "Available");
        int left = code.Count(i => i.operand is MethodInfo m && m.Name == "IsAvailable");
        Console.WriteLine("CommandersList.UpdateList: helper calls " + helper + ", IsAvailable calls left " + left);
        if (helper != 1 || left != 0) failed++;

        Console.WriteLine("RESULT applied=" + ok + " failed=" + failed);
        return failed == 0 ? 0 : 1;
    }
}
