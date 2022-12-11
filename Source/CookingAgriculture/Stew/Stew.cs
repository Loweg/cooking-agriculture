using System.Collections.Generic;
using System.Text;

using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CookingAgriculture {
	[StaticConstructorOnStartup]
	class Building_StewPot : Building_NutrientPasteDispenser {
		private int storedMeals = 0;
		private List<ThingDef> ingredients;
		private ProgressBar progressBar = new ProgressBar(2000);

		private float ProgressPerTickAtCurrentTemp => Mathf.Max((1f / progressBar.ticksToComplete) * CurrentTempSpeedFactor, 0f);

		public bool IsComplete => progressBar.Progress >= 1f;
		public bool IsEmpty => storedMeals <= 0;
		private float CurrentTempSpeedFactor {
			get {
				return GenMath.LerpDouble(-35f, 0f, 0f, 1f, this.AmbientTemperature);
			}
		}
		public override ThingDef DispensableDef => ThingDefOf.MealNutrientPaste;

		public override Building AdjacentReachableHopper(Pawn reacher) { return null; }

		public override void Draw() {
			base.Draw();

			Vector3 drawPos = this.DrawPos;
			drawPos.y += 0.04054054f;
			drawPos.z += 0.25f;
			progressBar.Draw(drawPos);
		}

		public override void ExposeData() {
			base.ExposeData();
			progressBar.ExposeData();
			Scribe_Values.Look(ref storedMeals, "storedMeals");
			Scribe_Values.Look(ref ingredients, "ingredients");
		}

		public override bool HasEnoughFeedstockInHoppers() { return !IsEmpty; }

		public override string GetInspectString() {
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(base.GetInspectString());
			stringBuilder.AppendLine(progressBar.Progress.ToStringPercent());
			if (!this.IsSociallyProper(null, false))
				stringBuilder.AppendLine("InPrisonCell".Translate());
			return stringBuilder.ToString().Trim();
		}

		public override void Tick() {
			base.Tick();
			progressBar.Progress = Mathf.Min(progressBar.Progress + ProgressPerTickAtCurrentTemp, 1f);

			if (IsComplete) {
				storedMeals = 10;
			}
		}
		public override IEnumerable<Gizmo> GetGizmos() {
			foreach (Gizmo c in base.GetGizmos()) {
				yield return c;
			}
			if (Prefs.DevMode) {
				yield return progressBar.GetGizmo();
			}
		}

		public override Thing TryDispenseFood() {
			if (IsEmpty) { return null; }
			storedMeals -= 1;
			Thing meal = ThingMaker.MakeThing(CA_DefOf.CA_Soup);
			/*CompIngredients comp = meal.TryGetComp<CompIngredients>();
			foreach (var ingredient in ingredients) {
				comp.RegisterIngredient(ingredient);
			}*/
			return meal;
		}
	}

	class StewUtility {
		public static bool allowForbidden;
		public static bool allowDispenserFull;
		public static Pawn getter;
		public static Pawn eater;
		public static bool allowSociallyImproper;
		public static bool BestFoodSourceOnMap;

		public static bool StewPredicate(Building_StewPot t) {
			return (
				getter.RaceProps.ToolUser && getter.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
				(allowForbidden || !t.IsForbidden(getter)) &&
				!t.IsEmpty && t.InteractionCell.Standable(t.Map) && !getter.IsWildMan() &&
				(t.Faction == getter.Faction || t.Faction == getter.HostFaction) &&
				IsFoodSourceOnMapSociallyProper(t, getter, eater, allowSociallyImproper) &&
				getter.Map.reachability.CanReachNonLocal(getter.Position, new TargetInfo(t.InteractionCell, t.Map), PathEndMode.OnCell, TraverseParms.For(getter, Danger.Some))
			);
		}
		private static bool IsFoodSourceOnMapSociallyProper(Thing t, Pawn getter, Pawn eater, bool allowSociallyImproper) {
			return allowSociallyImproper || t.IsSociallyProper(getter) || t.IsSociallyProper(eater, eater.IsPrisonerOfColony, !getter.RaceProps.Animal);
		}
	}
}
