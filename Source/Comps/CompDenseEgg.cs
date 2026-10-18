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
            StatDefOf.MarketValue.Worker.ClearCacheForThing(parent);
        }

        public override void PostSplitOff(Thing piece)
        {
            base.PostSplitOff(piece);
            DenseEggUtility.GetComp(piece)?.Configure(Density, sourceRace);
        }

        public override bool AllowStackWith(Thing other)
        {
            return Density <= 1 && DenseEggUtility.GetEggDensity(other) <= 1;
        }

        public override string CompInspectStringExtra()
        {
            if (Density <= 1)
            {
                return null;
            }

            string hatchLabel = DenseEggUtility.GetHatchLabel(parent, sourceRace);
            return "HDA_EggInspection".Translate(Density, "HDA_DenseHatchResult".Translate(hatchLabel, Density));
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            if (Density <= 1)
            {
                yield break;
            }

            yield return new StatDrawEntry(
                StatCategoryDefOf.BasicsNonPawn,
                "HDA_Denseembryodensity".Translate(),
                Density.ToString(),
                "HDA_Thisfertilizedeggcontainsonedenseembryoratherthanmultiplenormaleggs".Translate(),
                9990,
                null,
                null,
                false);

            yield return new StatDrawEntry(
                StatCategoryDefOf.AnimalProductivity,
                "HDA_Hatchresult".Translate(),
                "HDA_DenseHatchResult".Translate(DenseEggUtility.GetHatchLabel(parent, sourceRace), Density),
                "HDA_Whenthisegghatchesitproducesoneoffspringwiththesamedensity".Translate(),
                9985,
                null,
                null,
                false);
        }
    }
}
