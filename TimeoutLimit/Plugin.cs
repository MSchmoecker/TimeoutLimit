using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace TimeoutLimit {
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [HarmonyPatch]
    public class Plugin : BaseUnityPlugin {
        public const string ModName = "TimeoutLimit";
        public const string ModGuid = "com.maxsch.valheim.TimeoutLimit";
        public const string ModVersion = "1.0.0";

        private static ConfigEntry<float> Timeout { get; set; }

        private void Awake() {
            Timeout = Config.Bind("General", "Timeout", 90f, "Timeout in seconds");

            Harmony harmony = new Harmony(ModGuid);
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(ZRpc), nameof(ZRpc.SetLongTimeout)), HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SetLongTimeoutTranspiler(IEnumerable<CodeInstruction> instructions) {
            CodeMatch[] loadFloat = new CodeMatch[] {
                new CodeMatch(OpCodes.Ldc_R4),
                new CodeMatch(OpCodes.Stsfld)
            };

            CodeInstruction[] loadTimeout = new CodeInstruction[] {
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Plugin), nameof(Timeout))),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ConfigEntry<float>), nameof(ConfigEntry<float>.Value)))
            };

            return new CodeMatcher(instructions)
                   // match 30s timeout
                   .MatchForward(false, loadFloat)
                   .RemoveInstructions(1)
                   .InsertAndAdvance(loadTimeout)
                   // match 90s timeout and preserve labels
                   .MatchForward(false, loadFloat)
                   .GetLabels(out List<Label> labels)
                   .RemoveInstructions(1)
                   .Insert(loadTimeout)
                   .AddLabels(labels)
                   .InstructionEnumeration();
        }
    }
}
