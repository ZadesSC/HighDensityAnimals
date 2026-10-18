using RimWorld;
using Verse;
using Verse.AI;

namespace HighDensityAnimals
{
    public sealed class WorkGiver_LoadAnimalToDensifier : WorkGiver_Scanner
    {
        private static readonly ThingDef DensifierDef = DefDatabase<ThingDef>.GetNamed("HDA_AnimalDensifier");
        private static readonly JobDef LoadJobDef = DefDatabase<JobDef>.GetNamed("HDA_LoadAnimalToDensifier");

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(DensifierDef);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_AnimalDensifier densifier = t as Building_AnimalDensifier;
            if (densifier == null || !densifier.Spawned || !densifier.AutomationEnabled || !densifier.NeedsAnimal)
            {
                return false;
            }

            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                return false;
            }

            if (densifier.IsForbidden(pawn) || !pawn.CanReserveAndReach(densifier, PathEndMode.Touch, Danger.Some, 1, -1, null, forced))
            {
                return false;
            }

            Pawn animal = densifier.FindNextAnimal(pawn);
            return animal != null && pawn.CanReserve(animal, 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_AnimalDensifier densifier = t as Building_AnimalDensifier;
            Pawn animal = densifier?.FindNextAnimal(pawn);
            if (animal == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(LoadJobDef, animal, densifier);
            job.targetC = densifier.InteractionCell;
            job.count = 1;
            job.ignoreDesignations = true;
            return job;
        }
    }
}
