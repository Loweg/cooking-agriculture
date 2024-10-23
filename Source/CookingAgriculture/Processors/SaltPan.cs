using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

using CookingAgriculture.Processors;

namespace CookingAgriculture {
    [StaticConstructorOnStartup]
    public class Building_SaltPan : Building_Processor {
        public override JobDef Job => CA_DefOf.CA_TakeFromSaltPan;
        public override float CurrentSpeedFactor() {
            return GenMath.LerpDouble(0f, 50f, 0f, 2f, this.AmbientTemperature);
        }

        public override string GetInspectString() {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            if (stringBuilder.Length != 0)
                stringBuilder.AppendLine();
            if (ShouldEmpty()) {
                stringBuilder.AppendLine("SaltPanReady".Translate());
            } else {
                stringBuilder.AppendLine("SaltPanProgress".Translate(Progress.ToStringPercent(), EstimatedTicksLeft.ToStringTicksToPeriod()));
                if (this.AmbientTemperature <= 0) {
                    stringBuilder.AppendLine("SaltPanTooCold".Translate());
                } else {
                    stringBuilder.AppendLine("SaltPanSpeed".Translate(CurrentSpeedFactor().ToStringPercent()));
                    stringBuilder.AppendLine(("Temperature".Translate() + ": " + this.AmbientTemperature.ToStringTemperature("F0")));
                }
            }
            return stringBuilder.ToString().TrimEndNewlines();
        }

        public override Thing Empty() {
            Thing outSalt = ThingMaker.MakeThing(ThingDef.Named("CA_Salt"));
            outSalt.stackCount = 25;
            this.Reset();
            return outSalt;
        }
        public override void Fill(Thing ingredient) {

        }
    }

    public class PlaceWorker_SaltPan : PlaceWorker {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null) {
            if (!WaterCellsPresent(loc, rot, map)) {
                return new AcceptanceReport("MustHaveOceanWater".Translate());
            }
            CellRect waterUse = WaterUseRect(loc, rot);
            foreach ((IntVec3, Rot4) pan in OtherSaltPansOnMap(Find.CurrentMap)) {
                if (waterUse.Overlaps(WaterUseRect(pan.Item1, pan.Item2))) {
                    return new AcceptanceReport("MustNotBlockOtherSaltPan".Translate());
                }
            }
            return true;
        }

        public override void DrawGhost(ThingDef def, IntVec3 loc, Rot4 rot, Color ghostCol, Thing thing = null) {
            CellRect waterUse = WaterUseRect(loc, rot);
            bool isOverlapping = false;
            foreach ((IntVec3, Rot4) pan in OtherSaltPansOnMap(Find.CurrentMap)) {
                var otherDrawColor = new Color(0.2f, 0.2f, 1f);
                if (waterUse.Overlaps(WaterUseRect(pan.Item1, pan.Item2))) {
                    isOverlapping = true;
                    if (Time.realtimeSinceStartup % 0.4 < 0.2) {
                        otherDrawColor = new Color(1f, 0.6f, 0.0f);
                    }
                }
                GenDraw.DrawFieldEdges(WaterUseRect(pan.Item1, pan.Item2).ToList(), otherDrawColor);
            }

            var drawColor = !isOverlapping && WaterCellsPresent(loc, rot, Find.CurrentMap) ? Designator_Place.CanPlaceColor.ToOpaque() : Designator_Place.CannotPlaceColor.ToOpaque();
            GenDraw.DrawFieldEdges(WaterUseRect(loc, rot).ToList(), drawColor);
        }

        private IEnumerable<(IntVec3, Rot4)> OtherSaltPansOnMap(Map map) {
            IEnumerable<(IntVec3, Rot4)> saltPans = from building in map.listerBuildings.allBuildingsColonist
                where building.def.defName == "CA_SaltPan"
                select (building.Position, building.Rotation);
            saltPans = saltPans.Concat(from building in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame)
                where building.def.entityDefToBuild.defName == "CA_SaltPan"
                select (building.Position, building.Rotation));
            return saltPans.Concat(from building in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint)
                where building.def.entityDefToBuild.defName == "CA_SaltPan"
                select (building.Position, building.Rotation));
        }

        private bool WaterCellsPresent(IntVec3 loc, Rot4 rot, Map map) {
            foreach (IntVec3 waterCell in CompPowerPlantWater.WaterCells(loc, rot)) {
                var terrain = map.terrainGrid.TerrainAt(waterCell);
                if (!(terrain.defName == "WaterOceanDeep" || terrain.defName == "WaterOceanShallow")) {
                    return false;
                };
            }
            return true;
        }

        private CellRect WaterUseRect(IntVec3 loc, Rot4 rot) {
            int width = rot.IsHorizontal ? 1 : 5;
            int height = rot.IsHorizontal ? 5 : 1;
            return CellRect.CenteredOn(loc + rot.FacingCell * 2, width, height);
        }
    }
}
