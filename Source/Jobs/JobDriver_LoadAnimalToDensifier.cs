using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace HighDensityAnimals
{
    public sealed class JobDriver_LoadAnimalToDensifier : JobDriver
    {
        private const TargetIndex AnimalInd = TargetIndex.A;
        private const TargetIndex DensifierInd = TargetIndex.B;
        private const TargetIndex CellInd = TargetIndex.C;

        private Pawn Animal => job.GetTarget(AnimalInd).Pawn;
        private Building_AnimalDensifier Densifier => job.GetTarget(DensifierInd).Thing as Building_AnimalDensifier;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Animal, job, 1, -1, null, errorOnFailed) &&
                   pawn.Reserve(Densifier, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(AnimalInd);
            this.FailOnDestroyedNullOrForbidden(DensifierInd);
            this.FailOn(() => Densifier == null || !Densifier.NeedsAnimal || !Densifier.AcceptsAnimal(Animal));

            yield return Toils_Goto.GotoThing(AnimalInd, PathEndMode.ClosestTouch);

            Toil pickUpAnimal = ToilMaker.MakeToil("PickUpAnimalForDensifier");
            pickUpAnimal.initAction = () =>
            {
                Pawn animal = Animal;
                if (animal == null || Densifier == null || !Densifier.AcceptsAnimal(animal))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (pawn.carryTracker.CarriedThing != null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (animal.Spawned)
                {
                    animal.DeSpawn();
                }

                if (!pawn.carryTracker.TryStartCarry(animal))
                {
                    if (!animal.Spawned && Map != null)
                    {
                        GenPlace.TryPlaceThing(animal, pawn.Position, Map, ThingPlaceMode.Near);
                    }

                    EndJobWith(JobCondition.Incompletable);
                }
            };
            pickUpAnimal.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pickUpAnimal;

            yield return Toils_Haul.CarryHauledThingToCell(CellInd);

            Toil insertAnimal = ToilMaker.MakeToil("InsertAnimalIntoDensifier");
            insertAnimal.initAction = () =>
            {
                Pawn carriedAnimal = pawn.carryTracker.CarriedThing as Pawn;
                if (carriedAnimal == null || Densifier == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (!Densifier.TryInsertAnimal(carriedAnimal))
                {
                    pawn.carryTracker.TryDropCarriedThing(pawn.PositionHeld, ThingPlaceMode.Near, out _);
                    EndJobWith(JobCondition.Incompletable);
                }
            };
            insertAnimal.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return insertAnimal;
        }
    }
}
