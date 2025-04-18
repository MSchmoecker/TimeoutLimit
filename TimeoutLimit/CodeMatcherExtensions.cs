using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

// Credits to ASharpPen!
// https://github.com/ASharpPen/Valheim.SpawnThat/blob/a753ccc0776b87f9d9721915f5ff57a5f1503873/src/SpawnThat/Utilities/Extensions/CodeMatcherExtensions.cs

namespace TimeoutLimit {
    public static class CodeMatcherExtensions {
        public static CodeMatcher GetPosition(this CodeMatcher codeMatcher, out int position) {
            position = codeMatcher.Pos;
            return codeMatcher;
        }

        public static CodeMatcher AddLabel(this CodeMatcher codeMatcher, out Label label) {
            label = new Label();
            codeMatcher.AddLabels(new[] { label });
            return codeMatcher;
        }

        public static CodeMatcher GetLabels(this CodeMatcher codeMatcher, out List<Label> label) {
            label = codeMatcher.Labels;
            return codeMatcher;
        }

        public static CodeMatcher GetOperand(this CodeMatcher codeMatcher, out object operand) {
            operand = codeMatcher.Operand;
            return codeMatcher;
        }

        internal static CodeMatcher Print(this CodeMatcher codeMatcher, string message) {
            Debug.Log(message);
            return codeMatcher;
        }

        internal static CodeMatcher Print(this CodeMatcher codeMatcher, int before, int after) {
            for (int i = -before; i <= after; ++i) {
                int currentOffset = i;
                int index = codeMatcher.Pos + currentOffset;

                if (index <= 0) {
                    continue;
                }

                if (index >= codeMatcher.Length) {
                    break;
                }

                try {
                    var line = codeMatcher.InstructionAt(currentOffset);
                    Debug.Log($"[{currentOffset}] " + line.ToString());
                } catch (Exception e) {
                    Debug.Log(e.Message);
                }
            }

            return codeMatcher;
        }

        public static bool IsVirtCall(this CodeInstruction i, string declaringType, string name) {
            return i.opcode == OpCodes.Callvirt && i.operand is MethodInfo methodInfo && methodInfo.DeclaringType?.Name == declaringType && methodInfo.Name == name;
        }
    }
}
