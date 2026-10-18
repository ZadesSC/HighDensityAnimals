using UnityEngine;
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

            if (densifier.IsForbidden(pawn) || !pawn.CanReserveAndReach(densifier, PathEndMode.Touch, Danger.Some, 1, -1, null, forced))
            {
                return false;
            }

            return FindNutrition(pawn, densifier).Thing != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_AnimalDensifier densifier = t as Building_AnimalDensifier;
            if (densifier == null)
            {
                return null;
            }

            ThingCount nutrition = FindNutrition(pawn, densifier);
            if (nutrition.Thing == null)
            {
                return null;
            }

            Job job = HaulAIUtility.HaulToContainerJob(pawn, nutrition.Thing, densifier);
            if (job == null)
            {
                return null;
            }

            job.count = Mathf.Min(job.count, nutrition.Count);
            return job;
        }

        private ThingCount FindNutrition(Pawn pawn, Building_AnimalDensifier densifier)
        {
            Thing thing = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                t => IsValidNutrition(pawn, densifier, t));

            if (thing == null)
            {
                return default(ThingCount);
            }

            float nutritionPerUnit = thing.GetStatValue(StatDefOf.Nutrition);
            int neededCount = Mathf.CeilToInt(densifier.NutritionNeededForNextCycle / nutritionPerUnit);
            return new ThingCount(thing, Mathf.Min(thing.stackCount, neededCount));
        }

        private bool IsValidNutrition(Pawn pawn, Building_AnimalDensifier densifier, Thing thing)
        {
            return !thing.IsForbidden(pawn) &&
                   pawn.CanReserve(thing) &&
                   densifier.CanAcceptNutrition(thing) &&
                   thing.GetStatValue(StatDefOf.Nutrition) > 0f;
        }
    }
}
