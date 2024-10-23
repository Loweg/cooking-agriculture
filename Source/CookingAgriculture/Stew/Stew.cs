using System.Collections.Generic;
using System.Text;

using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using CookingAgriculture.Processors;
using System.Linq;
using System.Runtime;
namespace CookingAgriculture {
    // Somewhat based on the Replimat
    [StaticConstructorOnStartup]
    class Building_StewPot : Building_NutrientPasteDispenser, IStoreSettingsParent {
        private List<ThingDef> ingredients = new List<ThingDef>();
        private ProgressBar progressBar = new ProgressBar(2000);
        private int storedMeals = 0;
        public StorageSettings allowedIngredients;
        public ProcessSettings recipe;

        private float ProgressPerTickAtCurrentTemp => 1f / progressBar.ticksToComplete;

        public bool IsComplete => progressBar.Progress >= 1f;
        public bool IsEmpty => storedMeals == 0;
        public bool ShouldFill => ingredients.Count == 0 && IsEmpty && !GetComp<CompForbiddable>().Forbidden;
        public bool IsCooking => ingredients.Count >= 1 && IsEmpty;
        public override ThingDef DispensableDef => ThingDef.Named("CA_Soup");

        public bool StorageTabVisible => Faction == Faction.OfPlayerSilentFail;

        public override Building AdjacentReachableHopper(Pawn reacher) { return null; }

        public override void PostMake() {
            base.PostMake();
            var p = GetComp<CompProcessor>();
            if (p != null && p.Props.processes.Count > 0) {
                recipe = new ProcessSettings(p.Props.processes[0], this);
                recipe.def.valueType = ValueType.Nutrition;
            } else {
                Log.Warning("Stew pot failed to construct recipe");
            }
            if (def.inspectorTabsResolved == null) def.inspectorTabsResolved = new List<InspectTabBase>();
            if (!def.inspectorTabsResolved.Any(t => t is ITab_IngredientSelection)) {
                def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(typeof(ITab_IngredientSelection)));
            }
            allowedIngredients = new StorageSettings(this);
            if (def.building.defaultStorageSettings != null)
                allowedIngredients.CopyFrom(def.building.defaultStorageSettings);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false) {
            base.DrawAt(drawLoc, flip);
            if (IsCooking) {
                drawLoc.y += 0.04054054f;
                drawLoc.z += 0.25f;
                progressBar.Draw(drawLoc);
            }
        }

        public override void ExposeData() {
            base.ExposeData();
            progressBar.ExposeData();
            Scribe_Values.Look(ref ingredients, "ingredients");
            Scribe_Values.Look(ref storedMeals, "storedMeals");
            Scribe_Deep.Look(ref allowedIngredients, "allowedIngredients");
        }

        public override bool HasEnoughFeedstockInHoppers() { return !IsEmpty; }

        public override string GetInspectString() {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(base.GetInspectString());
            if (IsCooking) {
                stringBuilder.AppendLine("StewPotCooking".Translate());
            } else {
                stringBuilder.AppendLine("StewPotMeals".Translate(storedMeals));
            }
            if (!this.IsSociallyProper(null, false))
                stringBuilder.AppendLine("InPrisonCell".Translate());
            return stringBuilder.ToString().Trim();
        }

        public override void Tick() {
            base.Tick();
            if (IsCooking) {
                progressBar.Progress = Mathf.Min(progressBar.Progress + ProgressPerTickAtCurrentTemp, 1f);
            }
            if (IsComplete) {
                storedMeals = 10;
                progressBar.Progress = 0f;
            }
        }
        public override IEnumerable<Gizmo> GetGizmos() {
            foreach (Gizmo c in base.GetGizmos()) {
                if (c == BuildCopyCommandUtility.FindAllowedDesignator(ThingDefOf.Hopper)) continue;
                yield return c;
            }
            foreach (Gizmo gizmo in StorageSettingsClipboard.CopyPasteGizmosFor(allowedIngredients))
                yield return gizmo;
            if (Prefs.DevMode) {
                yield return progressBar.GetGizmo();
            }
        }

        public override Thing TryDispenseFood() {
            if (IsEmpty) { return null; }
            storedMeals -= 1;
            Thing meal = ThingMaker.MakeThing(CA_DefOf.CA_Soup);
            CompIngredients comp = meal.TryGetComp<CompIngredients>();
            foreach (var ingredient in ingredients) {
                comp.RegisterIngredient(ingredient);
            }
            if (IsEmpty) ingredients.Clear();
            return meal;
        }
        public void Fill(Thing ingredient) {
            ingredients.Add(ingredient.def);
        }

        public StorageSettings GetStoreSettings() => allowedIngredients;
        public StorageSettings GetParentStoreSettings() => def.building.fixedStorageSettings;
        public void Notify_SettingsChanged() {}
    }

    public class JobDriver_FillStewPot : JobDriver_FillProcessor {
        private const TargetIndex PotInd = TargetIndex.A;
        private const TargetIndex FoodInd = TargetIndex.B;

        private Building_StewPot StewPot => job.GetTarget(PotInd).Thing as Building_StewPot;

        public override bool TryMakePreToilReservations(bool errorOnFailed) {
            if (!pawn.Reserve(job.GetTarget(PotInd), job, errorOnFailed: errorOnFailed))
                return false;
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(FoodInd), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils() {
            this.FailOnDespawnedNullOrForbidden(PotInd);
            this.FailOnBurningImmobile(PotInd);

            foreach (Toil collectToil in CollectToils(PotInd, FoodInd))
                yield return collectToil;
            yield return Toils_Goto.GotoThing(PotInd, PathEndMode.InteractionCell);
            yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(FoodInd).FailOnDestroyedNullOrForbidden(PotInd).FailOnCannotTouch(PotInd, PathEndMode.Touch).WithProgressBarToilDelay(PotInd);
            yield return new Toil {
                initAction = () => {
                    StewPot.Fill(job.GetTarget(FoodInd).Thing);
                    job.GetTarget(FoodInd).Thing.Destroy(DestroyMode.Vanish);
                },
                defaultCompleteMode = ToilCompleteMode.Instant,
            };
        }
    }

    public class WorkGiver_FillStewPot : WorkGiver_Scanner {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(ThingDef.Named("CA_StewPot"));
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
            Building_StewPot pot = (Building_StewPot)t;
            if (pot == null) return null;
            pot.recipe.ingredientFilter = pot.allowedIngredients.filter;
            if (!pot.ShouldFill || t.IsBurning() || t.IsForbidden(pawn) || !pawn.CanReserve(t, ignoreOtherReservations: forced) || pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) != null) return null;
            var chosen = new List<ThingCount>();
            var missing = new List<IngredientCount>();
            var found = pot.recipe.FindIngredients(pawn, chosen, missing);
            if (found) {
                Job job = JobMaker.MakeJob(CA_DefOf.CA_FillStewPot, pot);
                job.targetQueueB = new List<LocalTargetInfo>(chosen.Count);
                job.countQueue = new List<int>(chosen.Count);
                for (int j = 0; j < chosen.Count; ++j) {
                    job.targetQueueB.Add(chosen[j].Thing);
                    job.countQueue.Add(chosen[j].Count);
                }
                job.haulMode = HaulMode.ToCellNonStorage;
                return job;
            } else if (FloatMenuMakerMap.makingFor == pawn) {
                JobFailReason.Is("MissingMaterials".Translate(missing.Select(m => m.Summary).ToCommaList()));
            }
            return null;
        }
    }

    class StewUtility {
        public static bool allowForbidden;
        public static Pawn getter;
        public static Pawn eater;
        public static bool allowDispenserFull;
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
