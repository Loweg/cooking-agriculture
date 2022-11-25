using System.Collections.Generic;
using System.Text;

using RimWorld;
using UnityEngine;
using Verse;

namespace CookingAgriculture {
	[StaticConstructorOnStartup]
	class Building_StewPot : Building {
		private int storedMeals = 0;
		private List<ThingDef> ingredients;
		private ProgressBar progressBar = new ProgressBar(2000);

		public bool IsComplete => progressBar.Progress >= 1f;
		public bool IsEmpty => storedMeals <= 0;
		private float CurrentTempSpeedFactor {
			get {
				return GenMath.LerpDouble(-35f, 0f, 0f, 1f, this.AmbientTemperature);
			}
		}
		private float ProgressPerTickAtCurrentTemp => Mathf.Max((1f / progressBar.ticksToComplete) * CurrentTempSpeedFactor, 0f);

		public virtual Thing TryDispenseFood() {
			if (storedMeals <= 0) return null;
			storedMeals -= 1;
			Thing meal = ThingMaker.MakeThing(CA_DefOf.CA_Soup);
			CompIngredients comp = meal.TryGetComp<CompIngredients>();
			foreach (var ingredient in ingredients) {
				comp.RegisterIngredient(ingredient);
			}
			return meal;
		}
		public override string GetInspectString() {
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(base.GetInspectString());
			if (!this.IsSociallyProper(null, false))
				stringBuilder.AppendLine("InPrisonCell".Translate());
			return stringBuilder.ToString().Trim();
		}

		public override void TickRare() {
			base.TickRare();
			progressBar.Progress = Mathf.Min(progressBar.Progress + 250f * ProgressPerTickAtCurrentTemp, 1f);

			if (IsComplete) {
				storedMeals = 10;
			}
		}

	}

	class StewUtility {
		public static bool StewPredicate(Building_StewPot t) {
			var getter = Utility.getter;
			return (
				getter.RaceProps.ToolUser && getter.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) &&
				(Utility.allowForbidden || !t.IsForbidden(getter)) &&
				!t.IsEmpty && t.InteractionCell.Standable(t.Map) && !getter.IsWildMan() &&
				(t.Faction == getter.Faction || t.Faction == getter.HostFaction) &&
				IsFoodSourceOnMapSociallyProper(t, getter, Utility.eater, Utility.allowSociallyImproper) &&
				getter.Map.reachability.CanReachNonLocal(getter.Position, new TargetInfo(t.InteractionCell, t.Map), PathEndMode.OnCell, TraverseParms.For(getter, Danger.Some))
			);
		}
		private static bool IsFoodSourceOnMapSociallyProper(Thing t, Pawn getter, Pawn eater, bool allowSociallyImproper) {
			return allowSociallyImproper || t.IsSociallyProper(getter) || t.IsSociallyProper(eater, eater.IsPrisonerOfColony, !getter.RaceProps.Animal);
		}
	}
}
