using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using Debug = UnityEngine.Debug;

namespace TimeoutLimit {
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    [HarmonyPatch]
    public class Plugin : BaseUnityPlugin {
        public const string ModName = "TimeoutLimit";
        public const string ModGuid = "com.maxsch.valheim.TimeoutLimit";
        public const string ModVersion = "1.0.0";

        public static Harmony harmony;

        public static ConfigEntry<float> Timeout { get; set; }

        public static HashSet<Assembly> patchedAssemblies = new HashSet<Assembly>();

        public void Awake() {
            Timeout = Config.Bind("General", "Timeout", 90f, "Timeout in seconds");
            Timeout.SettingChanged += (sender, e) => {
                if (Chainloader.PluginInfos.TryGetValue("com.jotunn.jotunn", out PluginInfo info)) {
                    SetJotunnTimeout(info);
                }
            };

            harmony = new Harmony(ModGuid);
            harmony.PatchAll();
        }

        public void Start() {
            foreach (PluginInfo plugin in Chainloader.PluginInfos.Values) {
                if (plugin == null || !plugin.Instance) {
                    continue;
                }

                Logger.LogInfo($"Patching {plugin.Metadata.Name} [{plugin.Metadata.GUID}]");

                if (plugin.Metadata.GUID == "com.jotunn.jotunn") {
                    SetJotunnTimeout(plugin);
                }

                Assembly assembly = plugin.Instance.GetType().Assembly;

                if (!patchedAssemblies.Add(assembly)) {
                    continue;
                }

                List<Type> serverSyncClasses = assembly.GetTypes()
                    .Where(t => t.IsClass && (t.Name == "ConfigSync" || t.Name == "ServerSync"))
                    .ToList();

                List<Type> waitForQueueClasses = serverSyncClasses
                    .SelectMany(t => t.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance))
                    .SelectMany(t => t.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance))
                    .Where(t => t.IsClass && t.Name.Contains("waitForQueue"))
                    .ToList();

                bool matchedAzuAntiCheat = false;

                if (plugin.Metadata.GUID == "Azumatt.AzuAntiCheat" && waitForQueueClasses.Count == 0) {
                    waitForQueueClasses = assembly.GetTypes()
                        .SelectMany(t => t.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance))
                        .SelectMany(t => t.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
                        .Where(m => m.Name.Contains("waitForQueue"))
                        .SelectMany(m => m.DeclaringType?.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance))
                        .ToList();

                    matchedAzuAntiCheat = waitForQueueClasses.Count == 1;
                    Logger.LogInfo($"Found special waitForQueue in AzuAntiCheat: {matchedAzuAntiCheat}");
                }

                List<MethodInfo> moveNextMethods = waitForQueueClasses
                    .SelectMany(t => t.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance))
                    .Where(m => m.Name == "MoveNext")
                    .ToList();

                foreach (MethodInfo moveNextMethod in moveNextMethods) {
                    try {
                        if (matchedAzuAntiCheat) {
                            harmony.Patch(moveNextMethod, transpiler: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(AzuAntiCheatTranspiler))));
                        } else {
                            harmony.Patch(moveNextMethod, transpiler: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(ServerSyncTranspiler))));
                        }
                    } catch (Exception e) {
                        Logger.LogError($"Failed to patch {moveNextMethod.DeclaringType.FullName}.{moveNextMethod.Name}: {e}");
                    }
                }

                if (moveNextMethods.Count == 0) {
                    Logger.LogInfo($"No waitForQueue found in {plugin.Metadata.Name} [{plugin.Metadata.GUID}]");
                }
            }
        }

        public void SetJotunnTimeout(PluginInfo plugin) {
            if (plugin != null && plugin.Instance != null && plugin.Metadata.GUID == "com.jotunn.jotunn" && plugin.Metadata.Version >= new System.Version("2.24.0")) {
                Type customRPC = AccessTools.TypeByName("Jotunn.Entities.CustomRPC, Jotunn");
                AccessTools.Field(customRPC, "Timeout").SetValue(null, Timeout.Value);
            }
        }

        public static CodeInstruction[] loadTimeout = new CodeInstruction[] {
            new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Plugin), nameof(Timeout))),
            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ConfigEntry<float>), nameof(ConfigEntry<float>.Value)))
        };

        [HarmonyPatch(typeof(ZRpc), nameof(ZRpc.SetLongTimeout)), HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SetLongTimeoutTranspiler(IEnumerable<CodeInstruction> instructions) {
            CodeMatch[] loadFloat = new CodeMatch[] {
                new CodeMatch(OpCodes.Ldc_R4),
                new CodeMatch(OpCodes.Stsfld)
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

        [HarmonyPatch(typeof(Debug), nameof(Debug.Log), new Type[] { typeof(object) }), HarmonyPrefix]
        public static bool DebugContext(object message) {
            if (message is string str && str.Contains("seconds config sending timeout") && !str.StartsWith("[")) {
                Assembly callingAssembly;

                try {
                    callingAssembly = (new StackTrace().GetFrames() ?? Array.Empty<StackFrame>())
                        .First(x => x.GetMethod().ReflectedType?.Assembly != typeof(Plugin).Assembly && x.GetMethod().ReflectedType?.Assembly != typeof(Debug).Assembly)
                        .GetMethod()
                        .ReflectedType?
                        .Assembly;
                } catch (Exception) {
                    return true;
                }

                if (callingAssembly != null) {
                    Debug.Log($"[{callingAssembly.GetName().Name}] {message}");
                    return false;
                }
            }

            return true;
        }

        public static IEnumerable<CodeInstruction> ServerSyncTranspiler(IEnumerable<CodeInstruction> instructions) {
            return new CodeMatcher(instructions)
                .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 30f))
                .ThrowIfNotMatch("Failed to match 30s timeout")
                .RemoveInstructions(1)
                .InsertAndAdvance(loadTimeout)
                .MatchForward(false, new CodeMatch(OpCodes.Ldstr, "Disconnecting {0} after 30 seconds config sending timeout"))
                .ThrowIfNotMatch("Failed to match disconnect message")
                .RemoveInstructions(1)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldstr, "Disconnecting {0} after {1} seconds config sending timeout"))
                .MatchForward(false, new CodeMatch(i => i.opcode == OpCodes.Call && i.operand is MethodInfo method && method.Name == "Format"))
                .ThrowIfNotMatch("Failed to match string.Format")
                .RemoveInstructions(1)
                .InsertAndAdvance(loadTimeout)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Box, typeof(float)))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(string), nameof(string.Format), new Type[] { typeof(string), typeof(object), typeof(object) })))
                // .MatchForward(false, new CodeMatch(OpCodes.Ldc_I4, 20000))
                // .RemoveInstructions(1)
                // .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4, 0))
                .InstructionEnumeration();
        }

        public static IEnumerable<CodeInstruction> AzuAntiCheatTranspiler(IEnumerable<CodeInstruction> instructions) {
            return new CodeMatcher(instructions)
                .Start()
                .MatchForward(false, new CodeMatch(OpCodes.Ldc_R4, 30f))
                .ThrowIfNotMatch("Failed to match 30s timeout")
                .RemoveInstructions(1)
                .InsertAndAdvance(loadTimeout)
                .Start()
                .MatchForward(true, new CodeMatch(i => i.opcode == OpCodes.Call && i.operand is MethodInfo method && method.Name == "Format"))
                .ThrowIfNotMatch("Failed to match string.Format")
                .Advance(1)
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Pop),
                    new CodeInstruction(OpCodes.Ldstr, "Disconnecting peer after {0} seconds config sending timeout"),
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Plugin), nameof(Timeout))),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(ConfigEntry<float>), nameof(ConfigEntry<float>.Value))),
                    new CodeInstruction(OpCodes.Box, typeof(float)),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(string), nameof(string.Format), new Type[] { typeof(string), typeof(object) }))
                )
                // .Start()
                // .MatchForward(false, new CodeMatch(OpCodes.Ldc_I4, 20000))
                // .RemoveInstructions(1)
                // .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4, 0))
                .InstructionEnumeration();
        }
    }
}
