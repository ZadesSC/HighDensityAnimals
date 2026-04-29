using RimWorld;
using Verse;

namespace HighDensityAnimals
{
    public static class DenseEggUtility
    {
        public static CompDenseEgg GetComp(Thing egg)
        {
            return (egg as ThingWithComps)?.GetComp<CompDenseEgg>();
        }

        public static bool IsDenseEgg(Thing egg)
        {
            return GetEggDensity(egg) > 1;
        }

        public static int GetEggDensity(Thing egg)
        {
            return GetComp(egg)?.Density ?? 1;
        }

        public static ThingDef GetEggSourceRace(Thing egg)
        {
            return GetComp(egg)?.SourceRace;
        }

        public static string GetHatchLabel(Thing egg, ThingDef sourceRace)
        {
            ThingDef raceDef = sourceRace;
            if (raceDef != null)
            {
                PawnKindDef kindDef = raceDef.race?.AnyPawnKind;
                if (kindDef != null)
                {
                    return kindDef.label;
                }

                return raceDef.label;
            }

            CompHatcher hatcher = (egg as ThingWithComps)?.GetComp<CompHatcher>();
            return hatcher?.Props?.hatcherPawn?.label ?? "offspring";
        }
    }
}
