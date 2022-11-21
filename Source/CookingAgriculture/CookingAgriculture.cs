using RimWorld;
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

	[DefOf]
	public static class CA_DefOf {
		public static JobDef CA_TakeFromSaltPan;
	}
}
