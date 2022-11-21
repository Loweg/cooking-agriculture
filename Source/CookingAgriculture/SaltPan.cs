using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CookingAgriculture {
	[StaticConstructorOnStartup]
	public class Building_SaltPan : Building {
		private float progress = 0f;
		private Material barFilledCachedMat;
		private const int ticksToComplete = 3600;
		private static readonly Vector2 BarSize = new Vector2(0.55f, 0.1f);
		private static readonly Color BarEmptyColor = new Color(0.4f, 0.27f, 0.22f);
		private static readonly Color BarCompleteColor = new Color(0.9f, 0.85f, 0.2f);
		private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f));
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
		private float CurrentTempSpeedFactor {
			get {
				return GenMath.LerpDouble(0f, 50f, 0f, 2f, this.AmbientTemperature);
			}
		}
		private float ProgressPerTickAtCurrentTemp => Mathf.Max((1f / ticksToComplete) * CurrentTempSpeedFactor, 0f);
		private int EstimatedTicksLeft => Mathf.RoundToInt((1f - Progress) / ProgressPerTickAtCurrentTemp);

		private void Reset() {
			Progress = 0.0f;
		}

		public override void TickRare() {
			base.TickRare();
			Progress = Mathf.Min(Progress + 250f * ProgressPerTickAtCurrentTemp, 1f);
		}

		public override string GetInspectString() {
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(base.GetInspectString());
			if (stringBuilder.Length != 0)
				stringBuilder.AppendLine();
			if (Progress >= 1.0) {
				stringBuilder.AppendLine("SaltPanReady".Translate());
			} else {
				stringBuilder.AppendLine("SaltPanProgress".Translate(Progress.ToStringPercent(), EstimatedTicksLeft.ToStringTicksToPeriod()));
				if (this.AmbientTemperature <= 0) {
					stringBuilder.AppendLine("SaltPanTooCold".Translate());
				} else {
					stringBuilder.AppendLine("SaltPanSpeed".Translate(CurrentTempSpeedFactor.ToStringPercent()));
					stringBuilder.AppendLine(("Temperature".Translate() + ": " + this.AmbientTemperature.ToStringTemperature("F0")));
				}
			}
			return stringBuilder.ToString().TrimEndNewlines();
		}

		public Thing TakeOutSalt() {
			if (Progress < 1.0) {
				Log.Warning("Tried to get salt but it's not yet ready.");
				return null;
			}
			Thing outSalt = ThingMaker.MakeThing(ThingDef.Named("CA_Salt"));
			outSalt.stackCount = 25;
			this.Reset();
			return outSalt;
		}

		public override void Draw() {
			base.Draw();
			Vector3 drawPos = this.DrawPos;
			drawPos.y += 0.04054054f;
			drawPos.z += 0.25f;
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

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look(ref progress, "progress");
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
		private const TargetIndex SaltPanInd = TargetIndex.A;
		private const TargetIndex SaltInd = TargetIndex.B;
		private const TargetIndex StorageCellInd = TargetIndex.C;

		protected Building_SaltPan SaltPan => this.job.GetTarget(SaltPanInd).Cell.GetThingList(Map).Find(q => q is Building_SaltPan) as Building_SaltPan;
		protected Thing Salt => this.job.GetTarget(SaltInd).Thing;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => this.pawn.Reserve(SaltPan, this.job, errorOnFailed: errorOnFailed);

		protected override IEnumerable<Toil> MakeNewToils() {
			this.FailOnDespawnedNullOrForbidden(SaltPanInd);
			this.FailOnBurningImmobile(SaltPanInd);
			yield return Toils_Goto.GotoThing(SaltPanInd, PathEndMode.Touch);
			yield return new Toil() {
				tickAction = delegate {
					/*if (ticksToPickHit < -100) {
						ResetTicksToPickHit();
					}
					ticksToPickHit--;
					if (ticksToPickHit <= 0) {
						if (effecter == null) {
							effecter = EffecterDefOf.Mine.Spawn();
						}
						effecter.Trigger(pawn, Quarry);

						ResetTicksToPickHit();
					}*/
				},
				defaultDuration = 500,
				defaultCompleteMode = ToilCompleteMode.Delay,
				handlingFacing = true
			}.FailOnDestroyedNullOrForbidden(SaltPanInd).FailOnCannotTouch(SaltPanInd, PathEndMode.Touch).WithProgressBarToilDelay(SaltInd, false, -0.5f);
			yield return new Toil() {
				initAction = delegate {
					Thing salt = SaltPan.TakeOutSalt();
					GenPlace.TryPlaceThing(salt, pawn.Position, Map, ThingPlaceMode.Near);
					job.SetTarget(SaltInd, salt);
					job.count = salt.stackCount;
					StoragePriority currentPriority = StoreUtility.CurrentStoragePriorityOf(salt);
					StoreUtility.TryFindBestBetterStorageFor(salt, pawn, Map, currentPriority, pawn.Faction, out IntVec3 c, out IHaulDestination haulDestination, true);
					job.SetTarget(StorageCellInd, c);
				},
				defaultCompleteMode = ToilCompleteMode.Instant
			};
			yield return Toils_Reserve.Reserve(SaltInd);
			yield return Toils_Reserve.Reserve(StorageCellInd);
			yield return Toils_Goto.GotoThing(SaltInd, PathEndMode.ClosestTouch);
			yield return Toils_Haul.StartCarryThing(SaltInd);
			Toil carryToCell = Toils_Haul.CarryHauledThingToCell(StorageCellInd);
			yield return carryToCell;
			yield return Toils_Haul.PlaceHauledThingInCell(StorageCellInd, carryToCell, true);
		}
	}

	public class WorkGiver_TakeFromSaltPan : WorkGiver_Scanner {
		public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(ThingDef.Named("CA_SaltPan"));

		public override bool ShouldSkip(Pawn pawn, bool forced = false) {
			List<Thing> pans = pawn.Map.listerThings.ThingsOfDef(ThingDef.Named("CA_SaltPan"));
			for (int i = 0; i < pans.Count; i++) {
				if (((Building_SaltPan)pans[i]).Progress >= 1f)
					return false;
			}
			return true;
		}

		public override PathEndMode PathEndMode => PathEndMode.Touch;
		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => t is Building_SaltPan saltPan && saltPan.Progress >= 1f && !t.IsBurning() && !t.IsForbidden(pawn) && pawn.CanReserve(t, ignoreOtherReservations: forced);
		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) => JobMaker.MakeJob(CA_DefOf.CA_TakeFromSaltPan, t);
	}
}
