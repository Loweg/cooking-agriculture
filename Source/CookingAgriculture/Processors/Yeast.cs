using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CookingAgriculture {
	[StaticConstructorOnStartup]
	public class Building_YeastCulture : Building, IThingHolder, IHaulSource, IHaulDestination, IHaulEnroute, IThingHolderEvents<Thing> {
		private readonly StorageSettings StorageSettings;

		private ThingOwner<Thing> yeast;
		private float feedThreshold = 0.1f;
		private float growth = 0f;
		private float food = 0f;
		private bool established = false;

		private int Count() {
			try { return yeast.First().stackCount; } catch { return 0; }
		}

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

        public Building_YeastCulture() {
			yeast = new ThingOwner<Thing>(this, true);
			var s = new StorageSettings();
			s.Priority = StoragePriority.Important;
			var f = new ThingFilter();
			f.SetAllow(CA_DefOf.CA_Yeast, true);
			s.filter = f;
			StorageSettings = s;
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
				food = Mathf.Clamp(food - 0.005f * FoodModifierByTemp.Evaluate(this.AmbientTemperature), 0f, 1f);
				growth += GrowthByTemp.Evaluate(this.AmbientTemperature) / 10f;
			} else {
				food = Mathf.Clamp(food - 0.01f * FoodModifierByTemp.Evaluate(this.AmbientTemperature), 0f, 1f);
				if (food > 0f) {
					growth += GrowthByTemp.Evaluate(this.AmbientTemperature);
				} else {
					growth = Mathf.Max(growth - 5f, 0f);
				}
			}
			if (growth > 100f) {
				if (yeast.Count == 0) {
					yeast.TryAdd(ThingMaker.MakeThing(CA_DefOf.CA_Yeast));
					established = true;
                } else {
					yeast.First().stackCount += 1;
                }
				growth -= 100f;
			} else if (growth <= 0f && established) {
				if (Count() <= 0) {
					yeast.Clear();
					established = false;
					growth = 0f;
				} else {
                    yeast.First().stackCount -= 1;
                    growth = 50f;
                }
            }
			growth = Mathf.Clamp(growth, 0f, 100f);
		}

		public override string GetInspectString() {
			var currentSpeed = GrowthByTemp.Evaluate(this.AmbientTemperature);
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(base.GetInspectString());
			if (stringBuilder.Length != 0) stringBuilder.AppendLine();
			stringBuilder.AppendLine("YeastCultureGrowth".Translate(Count()));
			stringBuilder.AppendLine("YeastCultureNutrition".Translate(food.ToStringPercent()));
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
				} else if (Count() >= CA_DefOf.CA_Yeast.stackLimit && food > 0f) {
					stringBuilder.AppendLine("YeastCultureFull".Translate());
				} else if (food > 0f) {
					stringBuilder.AppendLine("YeastCultureGoodTemperature".Translate(currentSpeed.ToStringPercent()));
				}
			}
			return stringBuilder.ToString().TrimEndNewlines();
		}

		public override IEnumerable<Gizmo> GetGizmos() {
			foreach (Gizmo c in base.GetGizmos()) {
				yield return c;
			}
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
			Scribe_Values.Look(ref growth, "growth");
			Scribe_Values.Look(ref food, "food");
            Scribe_Values.Look(ref established, "established");
            Scribe_Deep.Look(ref yeast, "yeast", this);
        }

        public bool StorageTabVisible => true;
        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		public ThingOwner GetDirectlyHeldThings() => yeast;
		public StorageSettings GetStoreSettings() => StorageSettings;
		public StorageSettings GetParentStoreSettings() => StorageSettings;
		public void Notify_SettingsChanged() { }
        public bool Accepts(Thing t) => t.def == CA_DefOf.CA_Yeast && (Count() < CA_DefOf.CA_Yeast.stackLimit || yeast.Contains(t));
        public int SpaceRemainingFor(ThingDef stuff) => CA_DefOf.CA_Yeast.stackLimit - Count();
        public void Notify_ItemAdded(Thing item) {
			if (!established) established = true;
			food += .04f * item.stackCount;
		}
        public void Notify_ItemRemoved(Thing item) {}
    }
	/*
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

		public override bool ShouldSkip(Pawn pawn, bool forced = false) {
			List<Thing> cultures = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("CA_YeastCulture"));
			for (int i = 0; i < cultures.Count; i++) {
				if (((Building_YeastCulture)cultures[i]).ShouldFeed) {
					return false;
				}
			}
			return true;
		}

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => t is Building_YeastCulture culture && culture.ShouldFeed  && !t.IsBurning() && !t.IsForbidden(pawn) && pawn.CanReserve(t, ignoreOtherReservations: forced) && pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) == null && FindFeed(pawn) != null;
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
	}*/
}
