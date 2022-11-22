using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CookingAgriculture {
	[StaticConstructorOnStartup]
	public class Plant_WinterFlowering : Plant {
		private static Graphic GraphicSowing = GraphicDatabase.Get<Graphic_Single>("Things/Plant/Plant_Sowing", ShaderDatabase.Cutout, Vector2.one, Color.white);
		private static Graphic GraphicWinter = GraphicDatabase.Get<Graphic_Random>("Trees/Cherry/Blossom", ShaderDatabase.CutoutPlant, Vector2.one, Color.white);

		public override Graphic Graphic {
			get {
				if (this.LifeStage == PlantLifeStage.Sowing) {
					return GraphicSowing;
				}
				Vector2 vector = Find.WorldGrid.LongLatOf(this.Map.Tile);
				Season season = GenDate.Season((long)Find.TickManager.TicksAbs, vector);
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
                soundImpactStuff = referenceProps.soundImpactStuff,
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
            else
                modifierList.Add(new StatModifier() {
                    stat = stat,
                    value = (mode == ToStringNumberSense.Factor ? 1f : 0.0f) * factor
                });
        }
    }

    [DefOf]
	public static class CA_DefOf {
		public static JobDef CA_TakeFromSaltPan;
        public static StuffCategoryDef CA_ChunkStone;
    }
}
