using System.Collections.Generic;

using UnityEngine;
using RimWorld;
using Verse;
using Verse.AI;

namespace CookingAgriculture {
    [StaticConstructorOnStartup]
    class ProgressBar {
        private Material barFilledCachedMat;
        private static readonly Vector2 BarSize = new Vector2(0.55f, 0.1f);
        private static readonly Color BarEmptyColor = new Color(0.4f, 0.27f, 0.22f);
        private static readonly Color BarCompleteColor = new Color(0.9f, 0.85f, 0.2f);
        private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f));

        private float progress = 0f;
        public readonly int ticksToComplete;

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
        }
    }

    public class JobUtil {
        public static IEnumerable<Toil> CollectToils(TargetIndex processor, TargetIndex ingredient) {
            Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue(ingredient);
            yield return extract;
            Toil skipQueued = Toils_Jump.JumpIfHaveTargetInQueue(ingredient, extract);
            Toil skipStored = new Toil();
            skipStored.initAction = () => {
                Thing p = skipStored.actor.CurJob.GetTarget(processor).Thing;
                if (p == null || !p.Spawned) return;
                var ing = skipStored.actor.jobs.curJob.GetTarget(ingredient).Thing;
                if (ing == null) return;
                ThingOwner interactableThingOwner = p.TryGetInnerInteractableThingOwner();
                if (interactableThingOwner == null || !interactableThingOwner.Contains(ing)) return;
                HaulAIUtility.UpdateJobWithPlacedThings(skipStored.actor.jobs.curJob, ing, ing.stackCount);
                skipStored.actor.jobs.curDriver.JumpToToil(skipQueued);
            };
            yield return skipStored;
            Toil getToHaulTarget = Toils_Goto.GotoThing(ingredient, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(ingredient).FailOnSomeonePhysicallyInteracting(ingredient);
            yield return getToHaulTarget;
            yield return Toils_Haul.StartCarryThing(ingredient, true, reserve: false);
            yield return JobDriver_DoBill.JumpToCollectNextIntoHandsForBill(getToHaulTarget, TargetIndex.B);
            yield return Toils_Goto.GotoThing(processor, PathEndMode.InteractionCell).FailOnDestroyedOrNull(ingredient);
            yield return Toils_Haul.DepositHauledThingInContainer(processor, ingredient);
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
            this.SpawnQuantity(ThingDef.Named("CA_Wheat"), 2, map);
            this.SpawnQuantity(ThingDef.Named("Hay"), 3, map);
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
            stoneChunk.stuffProps = new StuffProperties() {
                stuffAdjective = referenceProps.stuffAdjective,
                commonality = referenceProps.commonality,
                categories = new List<StuffCategoryDef> { CA_DefOf.CA_ChunkStone },
                statOffsets = new List<StatModifier>(),
                statFactors = new List<StatModifier>(),
                color = referenceProps.color,
                constructEffect = referenceProps.constructEffect,
                appearance = referenceProps.appearance,
                soundImpactBullet = referenceProps.soundImpactBullet,
                soundImpactMelee = referenceProps.soundImpactMelee,
                soundMeleeHitSharp = referenceProps.soundMeleeHitSharp,
                soundMeleeHitBlunt = referenceProps.soundMeleeHitBlunt
            };
            StuffProperties chunkProps = stoneChunk.stuffProps;
            if (referenceProps.statOffsets != null) {
                for (int i = 0; i < referenceProps.statOffsets.Count; i++) {
                    StatModifier statOffset = referenceProps.statOffsets[i];
                    chunkProps.statOffsets.Add(new StatModifier() {
                        stat = statOffset.stat,
                        value = statOffset.value
                    });
                }
            }
            if (referenceProps.statFactors != null) {
                for (int i = 0; i < referenceProps.statFactors.Count; i++) {
                    StatModifier statFactor = referenceProps.statFactors[i];
                    chunkProps.statFactors.Add(new StatModifier() {
                        stat = statFactor.stat,
                        value = statFactor.value
                    });
                }
            }
            ModifyStatModifier(ref chunkProps.statFactors, StatDefOf.WorkToMake, ToStringNumberSense.Factor, 1.5f);
            ModifyStatModifier(ref chunkProps.statFactors, StatDefOf.WorkToBuild, ToStringNumberSense.Factor, 1.5f);
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
