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
            __result = GenMath.LerpDoubleClamped(0f, 10f, 0.2f, 1f, temperature);
            return false;
        }
    }

    [HarmonyPatch(typeof(CompRottable), nameof(CompRottable.CompInspectStringExtra))]
    class RotStringPatch {
        [HarmonyPostfix]
        static void Postfix(ref string __result, ref CompRottable __instance) {
            if (__result != null && __instance.PropsRot.TicksToRotStart - __instance.RotProgress > 0.0 && GenTemperature.RotRateAtTemperature(Mathf.RoundToInt(__instance.parent.AmbientTemperature)) <= 0.21) {
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
        // We change thingGrid.ThingsListAt(c) to DisplayThingList(map, c):
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var code = new List<CodeInstruction>(instructions);
            int i = 0;
            var lookingForThisFieldCall = HarmonyLib.AccessTools.Field(typeof(Verse.Map), "thingGrid");
            for (; i < code.Count; i++) {
                if (code[i].opcode != OpCodes.Ldfld || (System.Reflection.FieldInfo)code[i].operand != lookingForThisFieldCall) {
                    yield return code[i];
                    continue;
                }
                // found middle of List<Thing> list = base.Map.thingGrid.ThingsListAt(c);
                // We are at the original instruction .thingGrid
                //   and we have the Map on the stack
                i++;  // go past thingGrid instruction
                // Need the location c on the stack, but that's what happens next in original code - loading c
                yield return code[i];
                i++; // now past c
                // Next code instruction is to call ThingsListAt.
                i++; // We want our own list
                yield return new CodeInstruction(OpCodes.Call, HarmonyLib.AccessTools.Method("CookingAgriculture.PatchYeastDrawing:ThingListToDisplay"));
                break; // that's all we need to change!
            }
            for (; i < code.Count; i++) {
                yield return code[i];
            }
            yield break;
        }

        static public List<Thing> ThingListToDisplay(Map map, IntVec3 loc) {
            ThingWithComps building;
            SlotGroup slotGroup = loc.GetSlotGroup(map);
            if (slotGroup == null || (building = slotGroup.parent as Building_YeastCulture) == null) {
                return map.thingGrid.ThingsListAt(loc);
            }
            return map.thingGrid.ThingsListAt(loc).FindAll(t => t.def != CA_DefOf.CA_Yeast);
        }
    }
}
