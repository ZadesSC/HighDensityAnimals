using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HighDensityAnimals
{
    public static class HighDensityAnimalsStartup
    {
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            InjectDenseComps();
        }

        private static void InjectDenseComps()
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.race != null && def.race.Animal)
                {
                    EnsureComp(def, typeof(CompProperties_DenseAnimal), new CompProperties_DenseAnimal());
                }

                if (def.IsEgg)
                {
                    EnsureComp(def, typeof(CompProperties_DenseEgg), new CompProperties_DenseEgg());
                }
            }
        }

        private static void EnsureComp(ThingDef def, System.Type compType, CompProperties props)
        {
            if (def.comps == null)
            {
                def.comps = new List<CompProperties>();
            }

            for (int i = 0; i < def.comps.Count; i++)
            {
                if (compType.IsInstanceOfType(def.comps[i]))
                {
                    return;
                }
            }

            def.comps.Add(props);
        }
    }
}
