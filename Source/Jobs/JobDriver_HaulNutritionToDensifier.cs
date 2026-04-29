using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HighDensityAnimals
{
    public sealed class JobDriver_HaulNutritionToDensifier : JobDriver
    {
        private const TargetIndex NutritionInd = TargetIndex.A;
        private const TargetIndex DensifierInd = TargetIndex.B;
        private const TargetIndex CellInd = TargetIndex.C;

        private Thing NutritionThing => job.GetTarget(NutritionInd).Thing;
        private Building_AnimalDensifier Densifier => job.GetTarget(DensifierInd).Thing as Building_AnimalDensifier;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(NutritionThing, job, 1, -1, null, errorOnFailed) &&
                   pawn.Reserve(Densifier, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(NutritionInd);
            this.FailOnDestroyedNullOrForbidden(DensifierInd);
            this.FailOn(() => Densifier == null || !Densifier.NeedsNutrition);

            yield return Toils_Goto.GotoThing(NutritionInd, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(NutritionInd);
            yield return Toils_Haul.CarryHauledThingToCell(CellInd);
            yield return Toils_Haul.PlaceHauledThingInCell(CellInd, null, false);
        }
    }
}
