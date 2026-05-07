using RimWorld;
using Verse;

namespace HighDensityAnimals
{
    [DefOf]
    public static class HighDensityAnimalsDefOf
    {
        public static HediffDef HDA_DenseAnimalMetabolism;

        static HighDensityAnimalsDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HighDensityAnimalsDefOf));
        }
    }
}
