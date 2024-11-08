using System.Collections.Generic;

using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;
using Unity.Jobs;
using System.Linq;
using System;

namespace CookingAgriculture {
    [StaticConstructorOnStartup]
    class ProgressBar: IExposable {
        private Material barFilledCachedMat;
        private static readonly Vector2 BarSize = new Vector2(0.55f, 0.1f);
        private static readonly Color BarEmptyColor = new Color(0.4f, 0.27f, 0.22f);
        private static readonly Color BarCompleteColor = new Color(0.9f, 0.85f, 0.2f);
        private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f));

        private float progress = 0f;
        public int ticksToComplete = 60000;

        public float Progress {
            get => progress;
            set {
                if (value == progress)
                    return;
                progress = value;
                barFilledCachedMat = null;
            }
        }
        private Material BarFilledMat {
            get {
                if (barFilledCachedMat == null)
                    barFilledCachedMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.Lerp(BarEmptyColor, BarCompleteColor, Progress));
                return barFilledCachedMat;
            }
        }

        public ProgressBar() {}
        public ProgressBar(int duration) {
            ticksToComplete = duration;
        }

        public void Draw(Vector3 drawPos) {
            GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest() {
                center = drawPos,
                size = BarSize,
                fillPercent = Progress,
                filledMat = BarFilledMat,
                unfilledMat = BarUnfilledMat,
                margin = 0.1f,
                rotation = Rot4.North
            });
        }

        public void Draw(Vector3 drawPos, Vector2 drawScale) {
            GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest() {
                center = drawPos,
                size = BarSize * drawScale,
                fillPercent = Progress,
                filledMat = BarFilledMat,
                unfilledMat = BarUnfilledMat,
                margin = 0.1f,
                rotation = Rot4.North
            });
        }

        public Gizmo GetGizmo() {
            Command_Action gizmo = new Command_Action {
                defaultLabel = "Debug: Finish",
                defaultDesc = "Sets progress to 100%.",
            };
            gizmo.action = () => {
                Progress = 1f;
            };
            return gizmo;
        }

        public void ExposeData() {
            Scribe_Values.Look(ref progress, "progress");
            Scribe_Values.Look(ref ticksToComplete, "ticksToComplete");
        }
    }

    public class JobUtil {
        public static IEnumerable<Toil> CollectToils(TargetIndex processor, TargetIndex ingredient, TargetIndex ingredientPlaceCell) {
            Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue(ingredient);
            yield return extract;
            Toil skipQueued = Toils_Jump.JumpIfHaveTargetInQueue(ingredient, extract);
            Toil getToHaulTarget = Toils_Goto.GotoThing(ingredient, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(ingredient).FailOnSomeonePhysicallyInteracting(ingredient);
            yield return getToHaulTarget;
            yield return Toils_Haul.StartCarryThing(ingredient, true, reserve: false);
            yield return JobDriver_DoBill.JumpToCollectNextIntoHandsForBill(getToHaulTarget, TargetIndex.B);
            yield return Toils_Goto.GotoThing(processor, PathEndMode.InteractionCell).FailOnDestroyedOrNull(ingredient);
            Toil findPlaceTarget = Toils_JobTransforms.SetTargetToIngredientPlaceCell(processor, ingredient, ingredientPlaceCell);
            yield return findPlaceTarget;
            Toil placeHauledThing = new Toil();
            findPlaceTarget.initAction = delegate {
                Pawn actor = findPlaceTarget.actor;
                Job curJob = actor.jobs.curJob;
                IntVec3 cell = curJob.GetTarget(processor).Cell;
                if (actor.carryTracker.CarriedThing == null) {
                    Log.Error(string.Concat(actor, " tried to place hauled thing in cell but is not hauling anything. [Cooking and Agriculture]"));
                } else {
                    Action<Thing, int> placedAction = delegate (Thing t, int added) {
                        Log.Message("Placing thing");
                        HaulAIUtility.UpdateJobWithPlacedThings(curJob, t, added);
                    };
                    // Returning item on job cancel
                    if (!actor.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Direct, out var _, placedAction)) {
                        IntVec3 storeCell;
                        if (StoreUtility.TryFindBestBetterStoreCellFor(actor.carryTracker.CarriedThing, actor, actor.Map, StoragePriority.Unstored, actor.Faction, out var foundCell)) {
                            if (actor.CanReserve(foundCell)) {
                                actor.Reserve(foundCell, actor.CurJob);
                            }
                            actor.CurJob.SetTarget(processor, foundCell);
                            actor.jobs.curDriver.JumpToToil(findPlaceTarget);
                        } else if (HaulAIUtility.CanHaulAside(actor, actor.carryTracker.CarriedThing, out storeCell)) {
                            curJob.SetTarget(processor, storeCell);
                            curJob.count = int.MaxValue;
                            curJob.haulOpportunisticDuplicates = false;
                            curJob.haulMode = HaulMode.ToCellNonStorage;
                            actor.jobs.curDriver.JumpToToil(findPlaceTarget);
                        } else {
                            Log.Warning($"[Cooking and Agriculture] Incomplete haul for {actor}: Could not find anywhere to put {actor.carryTracker.CarriedThing} near {actor.Position}. Destroying. This should be very uncommon!");
                            actor.carryTracker.CarriedThing.Destroy();
                        }
                    }
                }
            };
            yield return placeHauledThing;   
            Toil physReserveToil = new Toil();
            physReserveToil.initAction = delegate {
                physReserveToil.actor.Map.physicalInteractionReservationManager.Reserve(physReserveToil.actor, physReserveToil.actor.CurJob, physReserveToil.actor.CurJob.GetTarget(ingredient));
            };
            yield return physReserveToil;
            yield return skipQueued;
        }
    }

    [StaticConstructorOnStartup]
    public class Plant_WinterFlowering : Plant {
        private static readonly Graphic GraphicSowing = GraphicDatabase.Get<Graphic_Single>("Things/Plant/Plant_Sowing", ShaderDatabase.Cutout, Vector2.one, Color.white);
        private static readonly Graphic GraphicWinter = GraphicDatabase.Get<Graphic_Random>("Things/Plant/CA_TreeCherry/Blossom", ShaderDatabase.CutoutPlant, Vector2.one, Color.white);

        public override Graphic Graphic {
            get {
                if (this.LifeStage == PlantLifeStage.Sowing) {
                    return GraphicSowing;
                }
                Vector2 vector = Find.WorldGrid.LongLatOf(this.Map.Tile);
                Season season = GenDate.Season(Find.TickManager.TicksAbs, vector);
                if (season == Season.Winter) {
                    return GraphicWinter;
                }
                if (this.def.plant.leaflessGraphic != null && this.LeaflessNow && (!this.sown || !this.HarvestableNow)) {
                    return this.def.plant.leaflessGraphic;
                }
                if (this.def.plant.immatureGraphic != null && !this.HarvestableNow) {
                    return this.def.plant.immatureGraphic;
                }
                return base.Graphic;
            }
        }
    }

    internal class WheatHayItemSpawner : Thing {
        protected void SpawnQuantity(ThingDef tDef, int numToSpawn, Map map) {
            Thing thing = ThingMaker.MakeThing(tDef);
            thing.stackCount = numToSpawn;
            GenPlace.TryPlaceThing(thing, this.Position, map, ThingPlaceMode.Near);
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad) {
            base.SpawnSetup(map, respawningAfterLoad);
            this.SpawnQuantity(ThingDef.Named("CA_Wheat"), 1, map);
            this.SpawnQuantity(ThingDef.Named("Hay"), 1, map);
            this.Destroy();
        }
    }

    [StaticConstructorOnStartup]
    public static class ChunkConstructor {
        static ChunkConstructor() {
            List<ThingDef> thingDefs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < thingDefs.Count; ++i) {
                ThingDef stoneChunk = thingDefs[i];
                if (stoneChunk.IsWithinCategory(ThingCategoryDefOf.StoneChunks) && !stoneChunk.butcherProducts.NullOrEmpty() && stoneChunk.butcherProducts.FirstOrDefault(t => t.thingDef.IsStuff)?.thingDef is ThingDef firstStuffProduct) {
                    ConstructChunkProperties(stoneChunk, firstStuffProduct.stuffProps);
                }
            }
            ResourceCounter.ResetDefs();
        }

        private static void ConstructChunkProperties(ThingDef stoneChunk, StuffProperties referenceProps) {
            stoneChunk.resourceReadoutPriority = ResourceCountPriority.Middle;
            stoneChunk.smeltable = false;
            var stuffProps = stoneChunk.stuffProps ?? new StuffProperties();
            stuffProps.stuffAdjective = referenceProps.stuffAdjective;
            stuffProps.commonality = referenceProps.commonality;
            var categories = stuffProps.categories ?? new List<StuffCategoryDef>();
            categories.Add(CA_DefOf.CA_ChunkStone);
            stuffProps.categories = categories;
            stuffProps.statOffsets = new List<StatModifier>();
            stuffProps.statFactors = new List<StatModifier>();
            stuffProps.color = referenceProps.color;
            stuffProps.constructEffect = referenceProps.constructEffect;
            stuffProps.appearance = referenceProps.appearance;
            stuffProps.soundImpactBullet = referenceProps.soundImpactBullet;
            stuffProps.soundImpactMelee = referenceProps.soundImpactMelee;
            stuffProps.soundMeleeHitSharp = referenceProps.soundMeleeHitSharp;
            stuffProps.soundMeleeHitBlunt = referenceProps.soundMeleeHitBlunt;
            if (referenceProps.statOffsets != null) {
                for (int i = 0; i < referenceProps.statOffsets.Count; i++) {
                    StatModifier statOffset = referenceProps.statOffsets[i];
                    stuffProps.statOffsets.Add(new StatModifier() {
                        stat = statOffset.stat,
                        value = statOffset.value
                    });
                }
            }
            if (referenceProps.statFactors != null) {
                for (int i = 0; i < referenceProps.statFactors.Count; i++) {
                    StatModifier statFactor = referenceProps.statFactors[i];
                    stuffProps.statFactors.Add(new StatModifier() {
                        stat = statFactor.stat,
                        value = statFactor.value
                    });
                }
            }
            stoneChunk.stuffProps = stuffProps;
            ModifyStatModifier(ref stoneChunk.stuffProps.statFactors, StatDefOf.WorkToMake, ToStringNumberSense.Factor, 1.5f);
            ModifyStatModifier(ref stoneChunk.stuffProps.statFactors, StatDefOf.WorkToBuild, ToStringNumberSense.Factor, 1.5f);
        }

        private static void ModifyStatModifier(ref List<StatModifier> modifierList, StatDef stat, ToStringNumberSense mode, float factor) {
            StatModifier statModifier = modifierList.FirstOrDefault((s => s.stat == stat));
            if (statModifier != null)
                statModifier.value *= factor;
            else {
                modifierList.Add(new StatModifier() {
                    stat = stat,
                    value = (mode == ToStringNumberSense.Factor ? 1f : 0.0f) * factor
                });
            }
        }
    }

    [DefOf]
    public static class CA_DefOf {
        public static JobDef CA_TakeFromSaltPan;
        public static JobDef CA_FillStewPot;
        public static JobDef CA_FeedYeastCulture;
        public static StuffCategoryDef CA_ChunkStone;
        public static ThingDef CA_Soup;
        public static ThingDef CA_RuinedFood;
        public static ThingDef CA_Yeast;
    }
}
