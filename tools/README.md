# Headless patch test

Applies every Play As Historical patch to the real game assembly under Mono, without starting the game.

1. Make a folder containing: the game's `Grand Tactician_Data/Managed/*.dll` except `mscorlib.dll`, `System*.dll`, `Mono.*` and `netstandard.dll`; every DLL from `BepInEx/core`; and `PlayAsHistorical.dll`.
2. In that folder: `mcs -out:PatchTest.exe -r:0Harmony.dll -r:BepInEx.dll -r:Assembly-CSharp.dll -r:UnityEngine.CoreModule.dll -r:System.Core.dll PatchTest.cs`
3. `mono PatchTest.exe`

Each patch class prints OK or FAIL with the full exception, then the officer-list transpiler is checked, and the last line sums up. "cant resolve internal call" warnings are expected – those are Unity engine calls with no engine present.

Limits: this uses stock Mono, not Unity's Mono fork, so a runtime quirk specific to Unity can still slip through. It checks that patches apply, not that the windows work – that needs the game.

The game's Mono has no `System.Reflection.Emit.ILGeneration` facade, so mod code must not use `LocalBuilder`, `ILGenerator`, `Label`, or HarmonyX's `IsLdloc()`/`IsStloc()`. The one transpiler here only swaps a call.
