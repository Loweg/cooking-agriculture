using System;

using HarmonyLib;
using UnityEngine;

using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

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
}
