using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CookingAgriculture {
    [StaticConstructorOnStartup]
    public class Building_YeastCulture : Building_Storage, IStorageGroupMember {
        private bool established = false;
        private float feedThreshold = 0.5f;
        private float food = 0f;
        private float growth = 0f;
        private int yeast = 0;

        public static readonly SimpleCurve GrowthByTemp = new SimpleCurve() {
            {new CurvePoint(-30f, -1f), true},
            {new CurvePoint(25, 1f), true},
            {new CurvePoint(35, 1f), true},
            {new CurvePoint(60, -1f), true},
        };
        public static readonly SimpleCurve FoodModifierByTemp = new SimpleCurve() {
            {new CurvePoint(-30f, 0f), true},
            {new CurvePoint(25, 1f), true},
        };
        public bool ShouldFeed => food <= 0.1f;

        public override void PostMake() {
            base.PostMake();
            settings.Priority = StoragePriority.Important;
            var f = new ThingFilter();
            f.SetAllow(CA_DefOf.CA_Yeast, true);
            settings.filter = f;
        }

        public int WantedFeedOf(ThingDef feed) {
            if (food <= 0f) {
                return 20;
            } else {
                return (int)((1f - food) / 0.05f);
            }
        }
        public void Feed(Thing feed) {
            float nutrition = feed.stackCount * 0.05f;
            food = Mathf.Clamp(food + nutrition, 0f, 1f);
        }

        public override void TickRare() {
            base.TickRare();
            if (!established) {
                food = Mathf.Clamp(food - 0.005f * FoodModifierByTemp.Evaluate(AmbientTemperature), 0f, 1f);
                growth += GrowthByTemp.Evaluate(AmbientTemperature) / 10f;
            } else {
                food = Mathf.Clamp(food - 0.01f * FoodModifierByTemp.Evaluate(AmbientTemperature), 0f, 1f);
                if (food > 0f) {
                    growth += GrowthByTemp.Evaluate(AmbientTemperature);
                } else {
                    growth = Mathf.Max(growth - 5f, 0f);
                }
            }
            if (growth > 100f) {
                established = true;
                yeast += 1;
                growth -= 100f;
            } else if (growth <= 0f && established) {
                if (yeast <= 0) {
                    established = false;
                    growth = 0f;
                } else {
                    yeast -= 1;
                    growth = 50f;
                }
            }
            growth = Mathf.Clamp(growth, 0f, 100f);
        }

        public override string GetInspectString() {
            var currentSpeed = GrowthByTemp.Evaluate(AmbientTemperature);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("YeastCultureNutrition".Translate(food.ToStringPercent()));
            stringBuilder.AppendLine("YeastCultureFeedThreshold".Translate(feedThreshold.ToStringPercent()));
            if (!established) {
                stringBuilder.AppendLine("YeastCultureEmpty".Translate());
                if (currentSpeed < 0f) {
                    stringBuilder.AppendLine("YeastCultureBadTemperature".Translate(Mathf.Abs(currentSpeed).ToStringPercent()));
                } else if (food > 0f) {
                    stringBuilder.AppendLine("YeastCultureGrowing".Translate((growth / 100f).ToStringPercent()));
                    stringBuilder.AppendLine("YeastCultureGoodTemperature".Translate(currentSpeed.ToStringPercent()));
                } else {
                    stringBuilder.AppendLine("YeastCultureInsertFood".Translate());
                }
            } else {
                stringBuilder.AppendLine("YeastCultureProgress".Translate((growth / 100f).ToStringPercent()));
                if (food <= 0f) {
                    stringBuilder.AppendLine("YeastCultureStarving".Translate());
                }
                if (currentSpeed < 0f) {
                    stringBuilder.AppendLine("YeastCultureBadTemperature".Translate(Mathf.Abs(currentSpeed).ToStringPercent()));
                } else if (yeast >= CA_DefOf.CA_Yeast.stackLimit && food > 0f) {
                    stringBuilder.AppendLine("YeastCultureFull".Translate());
                } else if (food > 0f) {
                    stringBuilder.AppendLine("YeastCultureGoodTemperature".Translate(currentSpeed.ToStringPercent()));
                }
            }
            return stringBuilder.ToString().TrimEndNewlines();
        }

        public override IEnumerable<Gizmo> GetGizmos() {
            foreach (Gizmo c in base.GetGizmos()) {
                if (!c.ToString().Contains("setting")) yield return c;
            }
            Command_Action thresholdUp = new Command_Action {
                defaultLabel = "Feed Threshold Up",
                defaultDesc = "Increase feed threshold by 10%.",
            };
            thresholdUp.action = () => {
                feedThreshold = Mathf.Min(feedThreshold + 0.1f, 0.9f);
            };
            thresholdUp.icon = ContentFinder<Texture2D>.Get("UI/Commands/CA_FeedThresholdRaise");
            yield return thresholdUp;
            Command_Action thresholdDown = new Command_Action {
                defaultLabel = "Feed Threshold Down",
                defaultDesc = "Decrease feed threshold by 10%.",
            };
            thresholdDown.action = () => {
                feedThreshold = Mathf.Max(feedThreshold - 0.1f, 0f);
            };
            thresholdDown.icon = ContentFinder<Texture2D>.Get("UI/Commands/CA_FeedThresholdLower");
            yield return thresholdDown;
            if (Prefs.DevMode) {
                Command_Action gizmo = new Command_Action {
                    defaultLabel = "Debug: Fill",
                    defaultDesc = "Increase growth amount by 100%.",
                };
                gizmo.action = () => {
                    growth += 100f;
                };
                yield return gizmo;
                Command_Action feed = new Command_Action {
                    defaultLabel = "Debug: Feed",
                    defaultDesc = "Increase food to 100%.",
                };
                feed.action = () => {
                    food = 1f;
                };
                yield return feed;
            }
        }
        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref established, "established");
            Scribe_Values.Look(ref feedThreshold, "feedThreshold");
            Scribe_Values.Look(ref food, "food");
            Scribe_Values.Look(ref growth, "growth");
            Scribe_Values.Look(ref yeast, "yeast");
        }
        public override void Notify_ReceivedThing(Thing item) {
            base.Notify_ReceivedThing(item);
            if (item.def != CA_DefOf.CA_Yeast) return;
            if (!established) established = true;
            food += .04f * item.stackCount;
            yeast += item.stackCount;
        }
        public override void Notify_LostThing(Thing item) {
            base.Notify_LostThing(item);
            if (item.def != CA_DefOf.CA_Yeast) return;
            yeast = Math.Max(yeast - item.stackCount, 0);
        }
    }

    public class JobDriver_FeedYeastCulture : JobDriver {
        private const TargetIndex CultureInd = TargetIndex.A;
        private const TargetIndex FoodInd = TargetIndex.B;

        protected Building_YeastCulture YeastCulture => job.GetTarget(CultureInd).Cell.GetThingList(Map).Find(q => q is Building_YeastCulture) as Building_YeastCulture;

        public override bool TryMakePreToilReservations(bool errorOnFailed) {
            return pawn.Reserve(job.GetTarget(FoodInd).Thing, job, stackCount: job.count) &&
                   pawn.Reserve(YeastCulture, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils() {
            this.FailOnDespawnedNullOrForbidden(CultureInd);
            this.FailOnBurningImmobile(CultureInd);
            this.FailOnBurningImmobile(FoodInd);

            Toil reserve = Toils_Reserve.Reserve(FoodInd, stackCount: job.count);
            yield return reserve;
            yield return Toils_Goto.GotoThing(FoodInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(FoodInd).FailOnSomeonePhysicallyInteracting(FoodInd);
            yield return Toils_Haul.StartCarryThing(FoodInd, subtractNumTakenFromJobCount: true).FailOnDestroyedNullOrForbidden(FoodInd);
            yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserve, FoodInd, TargetIndex.None, true);
            yield return Toils_Goto.GotoThing(CultureInd, PathEndMode.Touch);
            yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(FoodInd).FailOnDestroyedNullOrForbidden(CultureInd).FailOnCannotTouch(CultureInd, PathEndMode.Touch).WithProgressBarToilDelay(CultureInd);
            yield return new Toil {
                initAction = () => {
                    YeastCulture.Feed(job.GetTarget(FoodInd).Thing);
                    job.GetTarget(FoodInd).Thing.Destroy(DestroyMode.Vanish);
                },
                defaultCompleteMode = ToilCompleteMode.Instant,
            };
        }
    }

    public class WorkGiver_FeedYeastCulture : WorkGiver_Scanner {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(ThingDef.Named("CA_YeastCulture"));
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) {
            if (!(t is Building_YeastCulture culture)) return false;
            if (!culture.ShouldFeed) {
                JobFailReason.Is("YeastCultureNoFeedNeeded".Translate());
                return false;
            };
            if (t.IsBurning() || t.IsForbidden(pawn) || !pawn.CanReserve(t, ignoreOtherReservations: forced) || pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null) return false;
            if (FindFeed(pawn) != null) return true;
            JobFailReason.Is("YeastCultureNoFeed".Translate());
            return false;
        }
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
            var feed = FindFeed(pawn);
            return new Job(CA_DefOf.CA_FeedYeastCulture, t, feed) {
                count = Mathf.Min((t as Building_YeastCulture).WantedFeedOf(feed.def), feed.stackCount, pawn.carryTracker.AvailableStackSpace(feed.def))
            };
        }

        private Thing FindFeed(Pawn pawn) {
            bool validator(Thing x) => !x.IsForbidden(pawn) && (x.def.defName == "CA_Flour" || x.def.defName == "CA_Wheat") && pawn.CanReserve(x);
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways), PathEndMode.ClosestTouch, TraverseParms.For(pawn), validator: validator);
        }
    }
}
