using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HighDensityAnimals
{
    public static class DenseAnimalUtility
    {
        public static bool IsEligibleFusionAnimal(Pawn pawn, bool requireSpawned)
        {
            if (pawn == null || pawn.DestroyedOrNull() || pawn.Dead)
            {
                return false;
            }

            if (requireSpawned && (!pawn.Spawned || pawn.MapHeld == null))
            {
                return false;
            }

            if (!pawn.IsColonyAnimal)
            {
                return false;
            }

            if (!pawn.RaceProps.Animal || pawn.Downed || pawn.InMentalState)
            {
                return false;
            }

            if (PregnancyUtility.GetPregnancyHediff(pawn) != null)
            {
                return false;
            }

            if (HighDensityAnimalsMod.Settings != null && HighDensityAnimalsMod.Settings.requireAdultsOnly)
            {
                if (pawn.ageTracker.CurLifeStageIndex < pawn.RaceProps.lifeStageAges.Count - 1)
                {
                    return false;
                }
            }

            return true;
        }

        public static int GetDensity(Pawn pawn)
        {
            return GetComp(pawn)?.Density ?? 1;
        }

        public static bool IsDense(Pawn pawn)
        {
            return GetDensity(pawn) > 1;
        }

        public static CompDenseAnimal GetComp(Pawn pawn)
        {
            return pawn?.GetComp<CompDenseAnimal>();
        }

        public static int ScaleCount(Pawn pawn, int baseCount)
        {
            if (pawn == null)
            {
                return baseCount;
            }

            return baseCount * GetDensity(pawn);
        }

        public static float ScaleFloat(Pawn pawn, float baseValue)
        {
            if (pawn == null)
            {
                return baseValue;
            }

            return baseValue * GetDensity(pawn);
        }

        public static float ScaleHealth(Pawn pawn, float baseValue)
        {
            if (pawn == null)
            {
                return baseValue;
            }

            return baseValue * HealthScaleFactor(GetDensity(pawn));
        }

        public static float HealthScaleFactor(int density)
        {
            return Mathf.Sqrt(Mathf.Max(1, density));
        }

        public static float MeleeDamageFactor(int density)
        {
            density = Mathf.Max(1, density);
            if (density <= 1)
            {
                return 1f;
            }

            return 1f + Mathf.Log(density, 2f) * 0.25f;
        }

        public static float VisualScaleFactor(int density)
        {
            density = Mathf.Max(1, density);
            if (density <= 1)
            {
                return 1f;
            }

            float maxScale = HighDensityAnimalsMod.Settings?.maxVisualScale ?? 1.75f;
            return Mathf.Min(maxScale, 1f + Mathf.Log(density, 2f) * 0.1f);
        }

        public static float ArmorOffset(int density, float multiplier = 1f)
        {
            density = Mathf.Max(1, density);
            if (density <= 1)
            {
                return 0f;
            }

            return Mathf.Min(0.5f, Mathf.Log(density, 2f) * 0.04f) * multiplier;
        }

        public static void RefreshAllDenseAnimalCaches()
        {
            if (Current.Game == null) return;
            HashSet<Pawn> pawns = new HashSet<Pawn>(PawnsFinder.AllMapsWorldAndTemporary_Alive);
            List<Thing> things = new List<Thing>();
            foreach (Map map in Find.Maps)
            {
                ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForGroup(ThingRequestGroup.Everything), things);
                foreach (Thing thing in things)
                {
                    if (thing is Pawn pawn) pawns.Add(pawn);
                    else if (DenseEggUtility.GetComp(thing) != null) StatDefOf.MarketValue.Worker.ClearCacheForThing(thing);
                }
            }
            foreach (Pawn pawn in pawns)
                if (pawn.inventory != null)
                    foreach (Thing item in pawn.inventory.innerContainer)
                        if (DenseEggUtility.GetComp(item) != null) StatDefOf.MarketValue.Worker.ClearCacheForThing(item);
            foreach (Pawn pawn in pawns) GetComp(pawn)?.RecalculateCachedValues();
        }

        public static bool CanBeFused(Pawn pawn)
        {
            return IsEligibleFusionAnimal(pawn, true);
        }

        public static bool CanFuseTogether(Pawn first, Pawn second)
        {
            if (!IsEligibleFusionAnimal(first, false) || !IsEligibleFusionAnimal(second, false))
            {
                return false;
            }

            if (first.def != second.def)
            {
                return false;
            }

            if (first.gender != second.gender)
            {
                return false;
            }

            return true;
        }

        public static List<Pawn> FindFusionCandidates(Map map, ThingDef raceDef, int requiredCount)
        {
            if (map == null || raceDef == null || requiredCount < 2)
            {
                return null;
            }

            List<Pawn> ordered = map.mapPawns.AllPawnsSpawned.Where(p => p.def == raceDef && CanBeFused(p))
                .OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat)
                .ToList();

            if (ordered.Count < requiredCount)
            {
                return null;
            }

            foreach (Pawn seed in ordered)
            {
                List<Pawn> selection = ordered.Where(p => CanFuseTogether(seed, p)).Take(requiredCount).ToList();
                if (selection.Count == requiredCount) return selection;
            }
            return null;
        }

        public static Pawn ChoosePrimaryPawn(IEnumerable<Pawn> pawns)
        {
            if (pawns == null)
            {
                return null;
            }

            return pawns
                .Where(CanBeFused)
                .OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat)
                .FirstOrDefault();
        }

        public static string DescribeSpecies(ThingDef raceDef, int count)
        {
            if (raceDef == null)
            {
                return "HDA_animals".Translate();
            }

            string label = count == 1 ? raceDef.label : raceDef.label + "s";
            return label.CapitalizeFirst();
        }
    }
}
