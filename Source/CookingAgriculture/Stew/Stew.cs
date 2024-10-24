using System.Collections.Generic;
using System.Text;

using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using CookingAgriculture.Processors;
using System.Linq;

namespace CookingAgriculture {
    // Somewhat based on the Replimat
    [StaticConstructorOnStartup]
    class Building_StewPot : Building_NutrientPasteDispenser, IStoreSettingsParent, IThingHolder {
        private ThingOwner ingredients;
        private List<ThingDef> ingredientsDef = new List<ThingDef>();
        private ProgressBar progressBar;
        private int storedMeals = 0;
        private bool cooking = false;

        public ProcessSettings recipe;

        private float ProgressPerTick => 1f / progressBar.ticksToComplete;
        public bool IsComplete => progressBar.Progress >= 1f;
        public bool IsEmpty => storedMeals == 0;
        public bool ShouldFill => !cooking && IsEmpty && !GetComp<CompForbiddable>().Forbidden;
        public bool IsCooking => cooking;
        public override ThingDef DispensableDef => ThingDef.Named("CA_Soup");

        public bool StorageTabVisible => Faction == Faction.OfPlayerSilentFail;

        public void Start() => cooking = true;
        public bool IsReady() {
            var n = ingredients.Sum(i => i.def.GetStatValueAbstract(StatDefOf.Nutrition));
            return n >= recipe.def.ingredients.First().GetBaseCount();
        }
        public float Nutrition() {
            return ingredients.Sum(i => i.def.GetStatValueAbstract(StatDefOf.Nutrition));
        }
        public override Building AdjacentReachableHopper(Pawn reacher) { return null; }

        public override void PostMake() {
            base.PostMake();
            ingredients = new ThingOwner<Thing>(this);

            var p = GetComp<CompProcessor>();
            if (p != null && p.Props.processes.Count > 0) {
                recipe = new ProcessSettings(p.Props.processes[0], this);
                recipe.def.valueType = ValueType.Nutrition;
                progressBar = new ProgressBar((int)(recipe.def.days * 60000));
            }
            else {
                Log.Error("Stew pot failed to construct recipe");
            }
            if (def.inspectorTabsResolved == null) def.inspectorTabsResolved = new List<InspectTabBase>();
            if (!def.inspectorTabsResolved.Any(t => t is ITab_IngredientSelection)) {
                def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(typeof(ITab_IngredientSelection)));
            }
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
            recipe.ExposeData();
            ingredients.ExposeData();
            Scribe_Values.Look(ref storedMeals, "storedMeals");
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
                progressBar.Progress = Mathf.Min(progressBar.Progress + ProgressPerTick, 1f);
            }
            if (IsComplete) {
                storedMeals = 10;
                progressBar.Progress = 0f;
                foreach (var ingredient in ingredients) ingredientsDef.Add(ingredient.def);
                ingredients.Clear();
                cooking = false;
            }
        }
        public override IEnumerable<Gizmo> GetGizmos() {
            foreach (Gizmo c in base.GetGizmos()) {
                if (c == BuildCopyCommandUtility.FindAllowedDesignator(ThingDefOf.Hopper)) continue;
                yield return c;
            }
            foreach (Gizmo gizmo in StorageSettingsClipboard.CopyPasteGizmosFor(recipe.storageSettings))
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
            foreach (var ingredient in ingredientsDef) {
                comp.RegisterIngredient(ingredient);
            }
            if (IsEmpty) ingredientsDef.Clear();
            return meal;
        }

        public StorageSettings GetStoreSettings() => recipe.storageSettings;
        public StorageSettings GetParentStoreSettings() => GetParentSS();
        private StorageSettings GetParentSS() {
            var s = new StorageSettings();
            s.filter = recipe.def.GetFixedIngredientFilter();
            return s;
        }
        public void Notify_SettingsChanged() {}

        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        public ThingOwner GetDirectlyHeldThings() => ingredients;
    }

    public class JobDriver_FillStewPot : JobDriver {
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

            AddEndCondition(delegate {
                if (StewPot.IsReady()) {
                    return JobCondition.Succeeded;
                }
                return JobCondition.Ongoing;
            });

            foreach (Toil collectToil in JobUtil.CollectToils(PotInd, FoodInd)) {
                Log.Message(StewPot.Nutrition());
                yield return collectToil;
            }
            yield return Toils_Goto.GotoThing(PotInd, PathEndMode.InteractionCell);
            yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(FoodInd).FailOnDestroyedNullOrForbidden(PotInd).FailOnCannotTouch(PotInd, PathEndMode.Touch).WithProgressBarToilDelay(PotInd);
            yield return new Toil {
                initAction = () => StewPot.Start(),
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
