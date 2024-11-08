using System;

using HarmonyLib;
using UnityEngine;

using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Reflection.Emit;

namespace CookingAgriculture {
    [StaticConstructorOnStartup]
    public static class HarmonyBase {
        private static Harmony harmony = null;
        static internal Harmony instance {
            get {
                if (harmony == null)
                    harmony = new Harmony("CookingAgriculture.Harmony");
                return harmony;
            }
        }

        static HarmonyBase() {
            Harmony.DEBUG = true;
            instance.PatchAll();
        }
    }

    [HarmonyPatch(typeof(GenTemperature), nameof(GenTemperature.RotRateAtTemperature))]
    class RotRatePatch {
        [HarmonyPrefix]
        static bool Prefix(ref float __result, ref float temperature) {
            var settings = LoadedModManager.GetMod<CookingAgriculture>().GetSettings<Settings>();
            if (settings != null && settings.ineffective_freezing) {
                __result = GenMath.LerpDoubleClamped(0f, 10f, 0.2f, 1f, temperature);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.CompInspectStringExtra))]
    class RotStringPatch {
        [HarmonyPostfix]
        static void Postfix(ref string __result, ref CompRottable __instance) {
            var settings = LoadedModManager.GetMod<CookingAgriculture>().GetSettings<Settings>();
            if (settings != null && settings.ineffective_freezing && __result != null && __instance.PropsRot.TicksToRotStart - __instance.RotProgress > 0.0 && GenTemperature.RotRateAtTemperature(Mathf.RoundToInt(__instance.parent.AmbientTemperature)) <= 0.21) {
                StringBuilder stringBuilder = new StringBuilder();
                switch (__instance.Stage) {
                    case RotStage.Fresh:
                        stringBuilder.Append("RotStateFresh".Translate() + ".");
                        break;
                    case RotStage.Rotting:
                        stringBuilder.Append("RotStateRotting".Translate() + ".");
                        break;
                    case RotStage.Dessicated:
                        stringBuilder.Append("RotStateDessicated".Translate() + ".");
                        break;
                }
                stringBuilder.AppendLine();
                stringBuilder.Append("FrozenImperfect".Translate(__instance.TicksUntilRotAtCurrentTemp.ToStringTicksToPeriod()) + ".");
                __result = stringBuilder.ToString();
            }
        }
    }

    // From LWM Deep Storage
    [HarmonyPatch(typeof(SectionLayer_Things), "Regenerate")]
    public static class PatchYeastDrawing {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il) {
            var code = new List<CodeInstruction>(instructions);
            Label continueLabel = il.DefineLabel();
            int i = 0;
            var thingField = AccessTools.Field(typeof(Thing), "def");
            for (; i < code.Count; i++) {
                if (code[i].opcode != OpCodes.Ldfld || (FieldInfo)code[i].operand != thingField) {
                    yield return code[i];
                    continue;
                }
                var loadThingInstruction = code[i-1];

                // if (IsYeast(t)) continue;
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method("CookingAgriculture.PatchYeastDrawing:IsYeast"));
                yield return new CodeInstruction(OpCodes.Brtrue, continueLabel);
                // put t back on the stack
                yield return loadThingInstruction;
                break;
            }
            for (; i < code.Count; i++) {
                yield return code[i];
                if (code[i].opcode == OpCodes.Pop) code[i+1].labels.Add(continueLabel);
            }
            yield break;
        }

        static public bool IsYeast(Thing t) {
            var building = t.Position.GetFirstBuilding(t.Map);
            bool cellHasCulture = building != null && building is Building_YeastCulture;
            return cellHasCulture && t.def.EverStorable(false);
        }
    }
}
