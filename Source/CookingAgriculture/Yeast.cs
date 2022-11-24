using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CookingAgriculture {
	[StaticConstructorOnStartup]
	public class Building_YeastCulture : Building {
		private float growth = 0f;
		private float food = 0f;

		public static readonly SimpleCurve GrowthByTemp = new SimpleCurve() {
			{new CurvePoint(-30f, -1f), true},
			{new CurvePoint(30, 1f),  true},
			{new CurvePoint(60, -1f),  true},
		};
		public static readonly SimpleCurve FoodModifierByTemp = new SimpleCurve() {
			{new CurvePoint(-30f, 0f), true},
			{new CurvePoint(30, 1f),  true},
		};

		public bool ShouldFeed => food <= 0.1f;
		public int WantedFeedOf(ThingDef feed) {
			if (food <= 0f) {
				return 20;
			} else {
				return (int)((1f - food) / 0.05f);
			}
		}
		public override void TickRare() {
			base.TickRare();
			food = Mathf.Clamp(food - 0.01f * FoodModifierByTemp.Evaluate(this.AmbientTemperature), 0f, 1f);
			if (food > 0f) {
				growth = Mathf.Clamp(growth + GrowthByTemp.Evaluate(this.AmbientTemperature) * 0.01f, 0f, 100f);
			} else {
				growth = Mathf.Clamp(growth - 0.05f, 0f, 100f);
			}
		}

		public override string GetInspectString() {
			var currentSpeed = GrowthByTemp.Evaluate(this.AmbientTemperature);
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(base.GetInspectString());
			if (stringBuilder.Length != 0) stringBuilder.AppendLine();
			stringBuilder.AppendLine("YeastCultureGrowth".Translate((int)growth));
			stringBuilder.AppendLine("YeastCultureNutrition".Translate(food.ToStringPercent()));
			if (food <= 0f) {
				if (growth >= 0f) {
					stringBuilder.AppendLine("YeastCultureStarving".Translate());
				} else {
					stringBuilder.AppendLine("YeastCultureEmpty".Translate());
				}
			}
			if (currentSpeed < 0f) {
				stringBuilder.AppendLine("YeastCultureBadTemperature".Translate(Mathf.Abs(currentSpeed).ToStringPercent()));
			} else if (growth >= 100f && food > 0f) {
				stringBuilder.AppendLine("YeastCultureFull".Translate());
			} else if (food > 0f) {
				stringBuilder.AppendLine("YeastCultureGoodTemperature".Translate(currentSpeed.ToStringPercent()));
			}
			return stringBuilder.ToString().TrimEndNewlines();
		}

		public Thing TakeOutYeast() {
			if (growth < 10f) {
				Log.Warning("Tried to get yeast but growth < 10.");
				return null;
			}
			Thing outYeast = ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);
			outYeast.stackCount = 1;
			growth -= 10f;
			return outYeast;
		}

		public void Feed(Thing feed) {
			float nutrition = feed.stackCount * 0.05f;
			food = Mathf.Clamp(food + nutrition, 0f, 1f);
		}

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look(ref growth, "growth");
			Scribe_Values.Look(ref food, "food");
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
			Log.Message("Found job...");
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
			Log.Message("Checking culturing jobs");
			List<Thing> cultures = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("CA_YeastCulture"));
			for (int i = 0; i < cultures.Count; i++) {
				if (((Building_YeastCulture)cultures[i]).ShouldFeed) {
					Log.Message("Found job...");
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
			Log.Message("Finding feed...");
			bool validator(Thing x) => !x.IsForbidden(pawn) && (x.def.defName == "CA_Flour" || x.def.defName == "CA_Wheat") && pawn.CanReserve(x);
			return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways), PathEndMode.ClosestTouch, TraverseParms.For(pawn), validator: validator);
		}
	}
}
