using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

using CookingAgriculture.Processors;
using System.Diagnostics;

namespace CookingAgriculture {
    [StaticConstructorOnStartup]
    public class Building_SaltPan : Building {
        private ProgressBar progressBar = new ProgressBar(6000);

        public JobDef Job => CA_DefOf.CA_TakeFromSaltPan;
        public float ProgressPerTick => Mathf.Max((1f / progressBar.ticksToComplete) * CurrentSpeedFactor, 0f);
        public int EstimatedTicksLeft => Mathf.RoundToInt((1f - progressBar.Progress) / ProgressPerTick);
        public float CurrentSpeedFactor => GenMath.LerpDouble(0f, 50f, 0f, 2f, AmbientTemperature);
        public bool ShouldEmpty => progressBar.Progress >= 1f;
        public override void TickRare() {
            base.TickRare();
            progressBar.Progress = Mathf.Min(progressBar.Progress + (ProgressPerTick * GenTicks.TickRareInterval), 1f);
        }

        public override IEnumerable<Gizmo> GetGizmos() {
            foreach (Gizmo c in base.GetGizmos()) yield return c;
            if (Prefs.DevMode) {
                yield return progressBar.GetGizmo();
            }
        }

        public override string GetInspectString() {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            if (stringBuilder.Length != 0)
                stringBuilder.AppendLine();
            if (ShouldEmpty) {
                stringBuilder.AppendLine("SaltPanReady".Translate());
            } else {
                stringBuilder.AppendLine("SaltPanProgress".Translate(progressBar.Progress.ToStringPercent(), EstimatedTicksLeft.ToStringTicksToPeriod()));
                if (AmbientTemperature <= 0) {
                    stringBuilder.AppendLine("SaltPanTooCold".Translate());
                } else {
                    stringBuilder.AppendLine("SaltPanSpeed".Translate(CurrentSpeedFactor.ToStringPercent()));
                    stringBuilder.AppendLine(("Temperature".Translate() + ": " + AmbientTemperature.ToStringTemperature("F0")));
                }
            }
            return stringBuilder.ToString().TrimEndNewlines();
        }
        public override void ExposeData() {
            base.ExposeData();
            progressBar.ExposeData();
        }

        public Thing Empty() {
            Thing outSalt = ThingMaker.MakeThing(ThingDef.Named("CA_Salt"));
            outSalt.stackCount = 25;
            progressBar.Progress = 0f;
            return outSalt;
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

    public class JobDriver_TakeFromSaltPan : JobDriver {
        private const TargetIndex PanInd = TargetIndex.A;

        private Building_SaltPan Pan => job.GetTarget(PanInd).Thing as Building_SaltPan;

        public override bool TryMakePreToilReservations(bool errorOnFailed) {
            return pawn.Reserve(job.GetTarget(PanInd), job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils() {
            this.FailOnDespawnedNullOrForbidden(PanInd);
            this.FailOnBurningImmobile(PanInd);

            yield return Toils_Goto.GotoThing(PanInd, PathEndMode.InteractionCell);
            yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(PanInd).FailOnCannotTouch(PanInd, PathEndMode.Touch).WithProgressBarToilDelay(PanInd);
            yield return new Toil {
                initAction = () => {
                    var salt = Pan.Empty();
                    GenPlace.TryPlaceThing(salt, pawn.Position, Map, ThingPlaceMode.Near);
                    StoragePriority storagePriority = StoreUtility.CurrentStoragePriorityOf(salt);
                    if (StoreUtility.TryFindBestBetterStoreCellFor(salt, pawn, Map, storagePriority, pawn.Faction, out IntVec3 c)) {
                        job.SetTarget(TargetIndex.B, salt);
                        job.count = salt.stackCount;
                        job.SetTarget(TargetIndex.C, c);
                    } else {
                        EndJobWith(JobCondition.Succeeded);
                    }

                },
                defaultCompleteMode = ToilCompleteMode.Instant,
            };
        }
    }

    public class WorkGiver_TakeFromSaltPan : WorkGiver_Scanner {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(ThingDef.Named("CA_SaltPan"));
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) {
            Log.Message("Scanning");
            return t is Building_SaltPan pan && pan.ShouldEmpty && !t.IsBurning() && !t.IsForbidden(pawn)
                && pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), ignoreOtherReservations: forced);
        }
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
            Building_SaltPan pan = (Building_SaltPan)t;
            return JobMaker.MakeJob(CA_DefOf.CA_TakeFromSaltPan, pan);
        }
    }
}
