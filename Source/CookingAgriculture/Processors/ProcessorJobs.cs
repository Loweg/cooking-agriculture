using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace CookingAgriculture.Processors {
	public class JobDriver_EmptyProcessor : JobDriver {
		private const TargetIndex ProcessorInd = TargetIndex.A;
		private const TargetIndex ProductInd = TargetIndex.B;
		private const TargetIndex StorageCellInd = TargetIndex.C;

		protected Building_Processor Processor => this.job.GetTarget(ProcessorInd).Thing as Building_Processor;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => this.pawn.Reserve(Processor, this.job, errorOnFailed: errorOnFailed);

		protected override IEnumerable<Toil> MakeNewToils() {
			this.FailOnDespawnedNullOrForbidden(ProcessorInd);
			this.FailOnBurningImmobile(ProcessorInd);

			yield return Toils_Goto.GotoThing(ProcessorInd, PathEndMode.Touch);

			yield return new Toil() {
				defaultDuration = Processor.EmptyDuration,
				defaultCompleteMode = ToilCompleteMode.Delay,
				handlingFacing = true
			}.FailOnDestroyedNullOrForbidden(ProcessorInd).FailOnCannotTouch(ProcessorInd, PathEndMode.Touch).WithProgressBarToilDelay(ProductInd, false, -0.5f);

			yield return new Toil() {
				initAction = delegate {
					Thing product = Processor.Empty();
					GenPlace.TryPlaceThing(product, pawn.Position, Map, ThingPlaceMode.Near);
					job.SetTarget(ProductInd, product);
					job.count = product.stackCount;
					StoragePriority currentPriority = StoreUtility.CurrentStoragePriorityOf(product);
					StoreUtility.TryFindBestBetterStorageFor(product, pawn, Map, currentPriority, pawn.Faction, out IntVec3 c, out IHaulDestination haulDestination, true);
					job.SetTarget(StorageCellInd, c);
				},
				defaultCompleteMode = ToilCompleteMode.Instant
			};

			yield return Toils_Reserve.Reserve(ProductInd);
			yield return Toils_Reserve.Reserve(StorageCellInd);
			yield return Toils_Goto.GotoThing(ProductInd, PathEndMode.ClosestTouch);
			yield return Toils_Haul.StartCarryThing(ProductInd);
			Toil carryToCell = Toils_Haul.CarryHauledThingToCell(StorageCellInd);
			yield return carryToCell;
			yield return Toils_Haul.PlaceHauledThingInCell(StorageCellInd, carryToCell, true);
		}
	}

	public class WorkGiver_EmptyProcessor : WorkGiver_Scanner {
		public override PathEndMode PathEndMode => PathEndMode.Touch;
		public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);
		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => t is Building_Processor processor && processor.ShouldEmpty() && !t.IsBurning() && !t.IsForbidden(pawn) && pawn.CanReserve(t, ignoreOtherReservations: forced);
		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
			Building_Processor building = (Building_Processor)t;
			return JobMaker.MakeJob(building.Job, t);
		}

		public override bool ShouldSkip(Pawn pawn, bool forced = false) {
			foreach (var building in pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Processor>()) {
				if (building.ShouldEmpty()) {
					return false;
				}
			}
			return true;
		}
	}

	public class JobDriver_FillProcessor : JobDriver {
		private const TargetIndex ProcessorInd = TargetIndex.A;
		private const TargetIndex IngredientInd = TargetIndex.B;

		protected Building_Processor Processor => this.job.GetTarget(ProcessorInd).Thing as Building_Processor;
		protected Thing Ingredient => job.GetTarget(TargetIndex.B).Thing;


		public override bool TryMakePreToilReservations(bool errorOnFailed) {
			return pawn.Reserve(Ingredient, job, stackCount: job.count) &&
				pawn.Reserve(Processor, job, errorOnFailed: errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils() {
			this.FailOnDespawnedNullOrForbidden(ProcessorInd);
			this.FailOnBurningImmobile(ProcessorInd);
			this.FailOnBurningImmobile(IngredientInd);

			AddEndCondition(delegate {
				if (Processor.WantedOf(Ingredient.def) < 1) {
					return JobCondition.Succeeded;
				}
				return JobCondition.Ongoing;
			});

			Toil reserve = Toils_Reserve.Reserve(IngredientInd, stackCount: job.count);
			yield return reserve;
			yield return Toils_Goto.GotoThing(IngredientInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(IngredientInd).FailOnSomeonePhysicallyInteracting(IngredientInd);
			yield return Toils_Haul.StartCarryThing(IngredientInd, subtractNumTakenFromJobCount: true).FailOnDestroyedNullOrForbidden(IngredientInd);
			yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserve, IngredientInd, TargetIndex.None, true);
			yield return Toils_Goto.GotoThing(ProcessorInd, PathEndMode.Touch);
			yield return Toils_General.Wait(Processor.FillDuration).FailOnDestroyedNullOrForbidden(IngredientInd).FailOnDestroyedNullOrForbidden(ProcessorInd).FailOnCannotTouch(ProcessorInd, PathEndMode.Touch).WithProgressBarToilDelay(ProcessorInd);
			yield return new Toil {
				initAction = () => {
					Processor.Fill(job.GetTarget(IngredientInd).Thing);
					job.GetTarget(IngredientInd).Thing.Destroy(DestroyMode.Vanish);
				},
				defaultCompleteMode = ToilCompleteMode.Instant,
			};
		}
	}

	public class WorkGiver_FillProcessor : WorkGiver_Scanner {
		public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);
		public override PathEndMode PathEndMode => PathEndMode.Touch;
		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => t is Building_Processor processor && processor.ShouldFill() && processor.CanStartAnyBill() && !t.IsBurning() && !t.IsForbidden(pawn) && pawn.CanReserve(t, ignoreOtherReservations: forced) && pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) == null; //&& FindFeed(pawn) != null;
		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
			Building_Processor processor = (Building_Processor)t;
			for (int i = 0; i < processor.bills.Count; i++) {
				if (processor.bills[i].CanStart(t.Map)) {
					ProcessBill bill = processor.bills[i];
					var ingredient = bill.FindIngredient(pawn);
					if (ingredient != null) {
						return new Job(CA_DefOf.CA_FillProcessor, t, ingredient) {
							count = Mathf.Min((t as Building_Processor).WantedOf(ingredient.def), ingredient.stackCount, pawn.carryTracker.AvailableStackSpace(ingredient.def))
						};
					}
				}
			}
			return null;
		}

		public override bool ShouldSkip(Pawn pawn, bool forced = false) {
			foreach (var building in pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Processor>()) {
				if (building.ShouldFill() && building.CanStartAnyBill()) {
					return false;
				}
			}
			return true;
		}
	}
}
