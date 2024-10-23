using System;
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

        protected Building_Processor Processor => job.GetTarget(ProcessorInd).Thing as Building_Processor;
        protected Thing Ingredient => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) {
            return pawn.Reserve(Ingredient, job, stackCount: job.count) && pawn.Reserve(Processor, job, errorOnFailed: errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils() {
            this.FailOnDespawnedNullOrForbidden(ProcessorInd);
            this.FailOnBurningImmobile(ProcessorInd);
            AddEndCondition(delegate {
                if (Processor.WantedOf(Ingredient.def) < 1) {
                    return JobCondition.Succeeded;
                }
                return JobCondition.Ongoing;
            });

            foreach (Toil collectToil in CollectToils(TargetIndex.A, TargetIndex.B))
                yield return collectToil;
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            yield return Toils_General.Wait(Processor.FillDuration).FailOnDestroyedNullOrForbidden(IngredientInd)
                .FailOnDestroyedNullOrForbidden(ProcessorInd).FailOnCannotTouch(ProcessorInd, PathEndMode.Touch).WithProgressBarToilDelay(ProcessorInd);
            yield return new Toil {
                initAction = () => {
                    Processor.Fill(job.GetTarget(IngredientInd).Thing);
                    job.GetTarget(IngredientInd).Thing.Destroy(DestroyMode.Vanish);
                },
                defaultCompleteMode = ToilCompleteMode.Instant,
            };
        }

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

    public class WorkGiver_FillProcessor : WorkGiver_Scanner {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) {
            return t is Building_Processor processor && !processor.Running() && processor.CanStartAnyBill() && !t.IsBurning() && !t.IsForbidden(pawn) &&
                pawn.CanReserve(t, ignoreOtherReservations: forced) && pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct) == null;
        }
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) {
            Building_Processor processor = (Building_Processor)t;
            for (int i = 0; i < processor.processes.Count; i++) {
                if (processor.processes[i].ShouldStart(t.Map)) {
                    ProcessSettings settings = processor.processes[i];
                    var chosen = new List<ThingCount>();
                    var missing = new List<IngredientCount>();
                    var found = settings.FindIngredients(pawn, chosen, missing);
                    if (found) {
                        Job job = JobMaker.MakeJob(CA_DefOf.CA_FillProcessor, processor);
                        job.targetQueueB = new List<LocalTargetInfo>(chosen.Count);
                        job.countQueue = new List<int>(chosen.Count);
                        for (int j = 0; j < chosen.Count; ++j) {
                            job.targetQueueB.Add(chosen[j].Thing);
                            job.countQueue.Add(chosen[j].Count);
                        }
                        job.haulMode = HaulMode.ToCellNonStorage;
                        return job;
                    } else {
                        if (FloatMenuMakerMap.makingFor == pawn) {
                            JobFailReason.Is("MissingMaterials".Translate(missing.Select(m => m.Summary).ToCommaList()), settings.def.label);
                        }
                    }
                }
            }
            return null;
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false) {
            foreach (var building in pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Processor>()) {
                if (!building.Running() && building.CanStartAnyBill()) {
                    return false;
                }
            }
            return true;
        }
    }
}
