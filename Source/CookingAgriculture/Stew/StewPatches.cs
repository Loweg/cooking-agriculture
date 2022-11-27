using System;

using HarmonyLib;
using UnityEngine;

using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Reflection;

namespace CookingAgriculture.Stew {
	class StewPatches {
		[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.GetFinalIngestibleDef))]
		class GetIngestiblePatch {
			[HarmonyPostfix]
			public static void Postfix(ref ThingDef __result, Thing foodSource) {
				if (foodSource is Building_StewPot) {
					__result = CA_DefOf.CA_Soup;
				}
			}
		}
		[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.GetMaxAmountToPickup))]
		class GetPickupPatch {
			[HarmonyPostfix]
			public static void Postfix(ref int __result, Thing food, Pawn pawn) {
				if (food is Building_StewPot) {
					__result = !pawn.CanReserve(food) ? 0 : -1;
				}
			}
		}
		[HarmonyPatch(typeof(FoodUtility), "SpawnedFoodSearchInnerScan")]
		class SearchInnerPatch {
			[HarmonyPrefix]
			static bool Prefix(ref Predicate<Thing> validator) {
				var originalValidator = validator;
				bool newValidator(Thing x) => x is Building_StewPot t ? StewUtility.StewPredicate(t) : originalValidator(x);
				validator = newValidator;
				return true;
			}
		}
		[HarmonyPatch(typeof(GenClosest), "ClosestThingReachable")]
		class ClosestThingPatch {
			[HarmonyPrefix]
			static bool Prefix(ref Predicate<Thing> validator) {
				var originalValidator = validator;
				bool newValidator(Thing x) => x is Building_StewPot t ? StewUtility.StewPredicate(t) : originalValidator(x);
				validator = newValidator;
				return true;
			}
		}
		[HarmonyPatch(typeof(JobDriver_Feed), "MakeNewToils")]
		class JobFeedPatch {
			[HarmonyPrefix]
			static bool Prefix(ref JobDriver_Feed __instance, ref IEnumerable<Toil> __result) {
				var targetThing = typeof(JobDriver_Feed).GetField("TargetThingA", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
				if (targetThing is Building_StewPot) {
					Log.Message("JobDriver_Feed: StewPot");
					__instance.FailOnDespawnedNullOrForbidden(TargetIndex.B);
					__result.AddItem(Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnForbidden(TargetIndex.A));
					__result.AddItem(Toils_Ingest.TakeMealFromDispenser(TargetIndex.A, __instance.pawn));
					__result.AddItem(Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch));
					var deliveree = typeof(JobDriver_Feed).GetField("Deliveree", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);
					__result.AddItem(Toils_Ingest.ChewIngestible((Pawn)deliveree, 1.5f, TargetIndex.A).FailOnCannotTouch(TargetIndex.B, PathEndMode.Touch));
					__result.AddItem(Toils_Ingest.FinalizeIngest((Pawn)deliveree, TargetIndex.A));
					return false;
				}
				return true;
			}
		}
		[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.BestFoodSourceOnMap_NewTemp))]
		public static class BestFoodSourcePatch {
			static void Prefix(ref Pawn getter, ref Pawn eater, ref bool allowDispenserFull, ref bool allowForbidden, ref bool allowSociallyImproper) {
				Utility.BestFoodSourceOnMap = true;
				Utility.getter = getter;
				Utility.eater = eater;
				Utility.allowDispenserFull = allowDispenserFull;
				Utility.allowForbidden = allowForbidden;
				Utility.allowSociallyImproper = allowSociallyImproper;
			}
			static void Postfix() {
				Utility.BestFoodSourceOnMap = false;
			}
		}
		[HarmonyPatch(typeof(ThingListGroupHelper), nameof(ThingListGroupHelper.Includes))]
		class FoodSourcePatch {
			[HarmonyPostfix]
			public static void Postfix(ref bool __result, ThingRequestGroup group, ThingDef def) {
				if (group == ThingRequestGroup.FoodSource) {
					if (def.thingClass == typeof(Building_StewPot)) {
						Log.Message("Looking for food: stew pot");
					}
					__result = def.IsNutritionGivingIngestible || def.thingClass == typeof(Building_NutrientPasteDispenser) || def.thingClass == typeof(Building_StewPot);
				} else if (group == ThingRequestGroup.FoodSourceNotPlantOrTree) {
					if (def.thingClass == typeof(Building_StewPot)) {
						Log.Message("Looking for food (non plant): stew pot");
					}
					__result = def.IsNutritionGivingIngestible && (def.ingestible.foodType & ~FoodTypeFlags.Plant & ~FoodTypeFlags.Tree) != FoodTypeFlags.None || def.thingClass == typeof(Building_NutrientPasteDispenser) || def.thingClass == typeof(Building_StewPot);
				}
			}
		}
		/*[HarmonyPatch(typeof(JobDriver_FoodDeliver), nameof(JobDriver_FoodDeliver.GetReport))]
		public static class DeliverReportPatch {
			static void Postfix(JobDriver_FoodDeliver __instance, ref string __result) {
				if (__instance.job.GetTarget(TargetIndex.A).Thing is Building_StewPot && (Pawn)__instance.job.targetB.Thing != null) {
					__result = __instance.job.def.reportString.Replace("TargetA", "soup").Replace("TargetB", __instance.job.targetB.Thing.LabelShort);
				}
			}
		}
		[HarmonyPatch(typeof(JobDriver_FoodFeedPatient), nameof(JobDriver_FoodFeedPatient.GetReport))]
		public static class FeedPatientReportPatch {
			static void Postfix(JobDriver_FoodFeedPatient __instance, ref string __result) {
				if (__instance.job.GetTarget(TargetIndex.A).Thing is Building_StewPot && (Pawn)__instance.job.targetB.Thing != null) {
					__result = __instance.job.def.reportString.Replace("TargetA", "soup").Replace("TargetB", __instance.job.targetB.Thing.LabelShort);
				}
			}
		}*/

	}
}
