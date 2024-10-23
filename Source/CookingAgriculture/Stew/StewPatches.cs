using System;

using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Reflection;

namespace CookingAgriculture.Stew {
    class StewPatches {
        [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.BestFoodSourceOnMap))]
        public static class BestFoodSourcePatch {
            static void Prefix(ref Pawn getter, ref Pawn eater, ref bool allowDispenserFull, ref bool allowForbidden, ref bool allowSociallyImproper) {
                StewUtility.BestFoodSourceOnMap = true;
                StewUtility.getter = getter;
                StewUtility.eater = eater;
                StewUtility.allowDispenserFull = allowDispenserFull;
                StewUtility.allowForbidden = allowForbidden;
                StewUtility.allowSociallyImproper = allowSociallyImproper;
            }
            static void Postfix() {
                StewUtility.BestFoodSourceOnMap = false;
            }
        }

        [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.GetFinalIngestibleDef))]
        class GetIngestiblePatch {
            [HarmonyPrefix]
            public static bool Prefix(ref ThingDef __result, Thing foodSource) {
                if (foodSource is Building_StewPot && StewUtility.BestFoodSourceOnMap) {
                    __result = CA_DefOf.CA_Soup;
                    return false;
                }
                return true;
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

        [HarmonyPatch(typeof(ThingDef), nameof(ThingDef.IsFoodDispenser), MethodType.Getter)]
        public static class IsFoodDispenserPatch {
            [HarmonyPrefix]
            static bool Prefix(ThingDef __instance, ref bool __result) {
                if (__instance.thingClass == typeof(Building_StewPot)) {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(ThingListGroupHelper), nameof(ThingListGroupHelper.Includes))]
        public static class FoodSourcePatch {
            [HarmonyPrefix]
            static bool Prefix(ref ThingRequestGroup group, ref ThingDef def, ref bool __result) {
                if (group == ThingRequestGroup.FoodSource || group == ThingRequestGroup.FoodSourceNotPlantOrTree) {
                    if (def.thingClass == typeof(Building_StewPot)) {
                        __result = true;
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.TakeMealFromDispenser))]
        public static class TakeMealFromDispenserPatch {
            static bool Prefix(ref TargetIndex ind, ref Pawn eater, ref Toil __result) {
                if (eater.jobs.curJob.GetTarget(ind).Thing is Building_StewPot) {
                    var index = ind;
                    var toil = new Toil();
                    toil.initAction = delegate {
                        var actor = toil.actor;
                        var thing = ((Building_StewPot)actor.jobs.curJob.GetTarget(index).Thing).TryDispenseFood();
                        if (thing == null) {
                            actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                            return;
                        }

                        actor.carryTracker.TryStartCarry(thing);
                        actor.CurJob.SetTarget(index, actor.carryTracker.CarriedThing);
                    };
                    toil.FailOnCannotTouch(ind, PathEndMode.Touch);
                    toil.defaultCompleteMode = ToilCompleteMode.Delay;
                    __result = toil;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(JobDriver_FoodDeliver), nameof(JobDriver_FoodDeliver.GetReport))]
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
        }
        [HarmonyPatch(typeof(JobDriver_Ingest), nameof(JobDriver_Ingest.GetReport))]
        public static class Harmony_JobDriver_Ingest_GetReport {
            static void Postfix(JobDriver_Ingest __instance, ref string __result, bool ___usingNutrientPasteDispenser) {
                if (___usingNutrientPasteDispenser) {
                    if (__instance.job.GetTarget(TargetIndex.A).Thing is Building_StewPot) {
                        __result = __instance.job.def.reportString.Replace("TargetA", "soup");
                    } else {
                        __result = __instance.job.def.reportString.Replace("TargetA", __instance.job.GetTarget(TargetIndex.A).Thing.Label);
                    }
                }
            }
        }
    }
}
