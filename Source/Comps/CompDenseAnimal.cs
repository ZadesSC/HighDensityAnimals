using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace HighDensityAnimals
{
    public sealed class CompDenseAnimal : ThingComp
    {
        private int density = 1;

        public int Density
        {
            get
            {
                return density < 1 ? 1 : density;
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref density, "density", 1);
            if (density < 1)
            {
                density = 1;
            }
        }

        public void SetDensity(int value)
        {
            density = value < 1 ? 1 : value;
        }

        public void AddDensity(int amount)
        {
            SetDensity(Density + amount);
        }

        public override string CompInspectStringExtra()
        {
            if (Density <= 1 || !(parent is Pawn pawn))
            {
                return null;
            }

            return "Density: " + Density + "\nEquivalent to " + Density + " " + pawn.GetKindLabelPlural(Density);
        }

        public override float GetStatFactor(StatDef stat)
        {
            if (Density <= 1)
            {
                return 1f;
            }

            if (stat == StatDefOf.MeatAmount || stat == StatDefOf.LeatherAmount)
            {
                return Density;
            }

            if (HighDensityAnimalsMod.Settings != null && HighDensityAnimalsMod.Settings.scaleMarketValue && stat == StatDefOf.MarketValue)
            {
                return Density;
            }

            if (HighDensityAnimalsMod.Settings != null && HighDensityAnimalsMod.Settings.scaleHealth && stat == StatDefOf.MaxHitPoints)
            {
                return Density;
            }

            return 1f;
        }

        public override void GetStatsExplanation(StatDef stat, StringBuilder sb, string whitespace)
        {
            if (Density <= 1)
            {
                return;
            }

            bool applies =
                stat == StatDefOf.MeatAmount ||
                stat == StatDefOf.LeatherAmount ||
                (HighDensityAnimalsMod.Settings != null && HighDensityAnimalsMod.Settings.scaleMarketValue && stat == StatDefOf.MarketValue) ||
                (HighDensityAnimalsMod.Settings != null && HighDensityAnimalsMod.Settings.scaleHealth && stat == StatDefOf.MaxHitPoints);

            if (!applies)
            {
                return;
            }

            sb.AppendLine(whitespace + "High density: x" + Density);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            if (Density <= 1 || parent is not Pawn pawn)
            {
                yield break;
            }

            yield return new StatDrawEntry(
                StatCategoryDefOf.BasicsPawn,
                "Density",
                Density.ToString(),
                "This animal represents multiple fused animals.",
                9990,
                null,
                null,
                false);

            yield return new StatDrawEntry(
                StatCategoryDefOf.AnimalProductivity,
                "Equivalent animals",
                Density.ToString(),
                "Most outputs are scaled to this many animals' worth of production.",
                9990,
                null,
                null,
                false);
        }
    }
}
