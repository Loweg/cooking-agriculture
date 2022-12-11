
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CookingAgriculture {
	public class CompUnfreezable : ThingComp {
        protected float ruinedPercent = 0;

        private CompProperties_Unfreezable Props => (CompProperties_Unfreezable)props;
        public override void PostExposeData() => Scribe_Values.Look(ref ruinedPercent, "ruinedPercent");
        public void Reset() => ruinedPercent = 0.0f;
        public override void CompTick() => DoTicks(1);
        public override void CompTickRare() => DoTicks(250);

        private void DoTicks(int ticks) {
            if (ruinedPercent >= 1.0) {
                var totalNutrition = parent.def.ingestible.CachedNutrition * parent.stackCount;
                var position = parent.Position;
                var map = parent.Map;
                Log.Message($"Getting nutrition as {totalNutrition}");
                parent.Destroy(DestroyMode.WillReplace);
                Thing ruinedFood = ThingMaker.MakeThing(ThingDefOf.Kibble);
                ruinedFood.stackCount = (int)((totalNutrition + 0.00001) / ThingDefOf.Kibble.ingestible.CachedNutrition);
                GenPlace.TryPlaceThing(ruinedFood, position, map, ThingPlaceMode.Near);
            } else if (parent.AmbientTemperature < 0f) {
                ruinedPercent -= parent.AmbientTemperature * Props.progressPerDegreePerTick * ticks;
            }
            ruinedPercent = Mathf.Clamp(ruinedPercent, 0f, 1f);
        }

        public override string CompInspectStringExtra() {
            string str = base.CompInspectStringExtra();
            if (parent.AmbientTemperature < 0f) {
                str += $"Freezing: {ruinedPercent.ToStringPercent()}";
            }
            return str;
        }
    }
    public class CompProperties_Unfreezable : CompProperties {
        public float progressPerDegreePerTick = 0.000003f;
        public CompProperties_Unfreezable() {
            compClass = typeof(CompUnfreezable);
        }
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef) {
            if (parentDef.ingestible == null) {
                yield return $"[CookingAgriculture] {parentDef} must be ingestible.";
            }
        }
    }
}