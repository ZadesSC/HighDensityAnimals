using RimWorld;
using Verse;
using Verse.AI;

namespace HighDensityAnimals
{
    public sealed class WorkGiver_HaulNutritionToAnimalDensifier : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(DefDatabase<ThingDef>.GetNamed("HDA_AnimalDensifier"));

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_AnimalDensifier densifier = t as Building_AnimalDensifier;
            if (densifier == null || !densifier.Spawned || !densifier.AutomationEnabled || !densifier.NeedsNutrition)
            {
                return false;
            }

            if (!pawn.CanReserve(densifier, 1, -1, null, forced))
            {
                return false;
            }

            return densifier.FindBestNutrition(pawn) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_AnimalDensifier densifier = t as Building_AnimalDensifier;
            Thing food = densifier?.FindBestNutrition(pawn);
            if (food == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("HDA_HaulNutritionToDensifier"), food, densifier);
            job.targetC = densifier.InteractionCell;
            job.count = 1;
            return job;
        }
    }
}
