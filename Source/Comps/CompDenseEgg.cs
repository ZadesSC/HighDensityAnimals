using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HighDensityAnimals
{
    public sealed class CompDenseEgg : ThingComp
    {
        private int density = 1;
        private ThingDef sourceRace;

        public int Density
        {
            get
            {
                return density < 1 ? 1 : density;
            }
        }

        public ThingDef SourceRace => sourceRace;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref density, "density", 1);
            Scribe_Defs.Look(ref sourceRace, "sourceRace");
            if (density < 1)
            {
                density = 1;
            }
        }

        public void Configure(int newDensity, ThingDef newSourceRace)
        {
            density = newDensity < 1 ? 1 : newDensity;
            sourceRace = newSourceRace;
        }

        public override bool AllowStackWith(Thing other)
        {
            if (Density <= 1)
            {
                return true;
            }

            return false;
        }

        public override string CompInspectStringExtra()
        {
            if (Density <= 1)
            {
                return null;
            }

            string hatchLabel = DenseEggUtility.GetHatchLabel(parent, sourceRace);
            return "Dense embryo density: " + Density + "\nHatches into: 1 dense " + hatchLabel + " (Density " + Density + ")";
        }

        public override float GetStatFactor(StatDef stat)
        {
            if (Density <= 1)
            {
                return 1f;
            }

            if (HighDensityAnimalsMod.Settings != null && HighDensityAnimalsMod.Settings.scaleMarketValue && stat == StatDefOf.MarketValue)
            {
                return Density;
            }

            return 1f;
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            if (Density <= 1)
            {
                yield break;
            }

            yield return new StatDrawEntry(
                StatCategoryDefOf.BasicsNonPawn,
                "Dense embryo density",
                Density.ToString(),
                "This fertilized egg contains one dense embryo rather than multiple normal eggs.",
                9990,
                null,
                null,
                false);

            yield return new StatDrawEntry(
                StatCategoryDefOf.AnimalProductivity,
                "Hatch result",
                "1 dense " + DenseEggUtility.GetHatchLabel(parent, sourceRace) + " (Density " + Density + ")",
                "When this egg hatches, it produces one offspring with the same density.",
                9985,
                null,
                null,
                false);
        }
    }
}
