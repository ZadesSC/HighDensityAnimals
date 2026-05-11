using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace HighDensityAnimals
{
    public sealed class CompDenseAnimal : ThingComp
    {
        private int density = 1;

        public float CachedHealthScaleFactor { get; private set; } = 1f;
        public float CachedMeleeDamageFactor { get; private set; } = 1f;
        public float CachedVisualScaleFactor { get; private set; } = 1f;
        public float CachedArmorSharpOffset { get; private set; }
        public float CachedArmorBluntOffset { get; private set; }
        public float CachedArmorHeatOffset { get; private set; }
        public float CachedHungerRateFactor { get; private set; } = 1f;

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

            RecalculateCachedValues();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            RecalculateCachedValues();
            EnsureDensityHediff();
        }

        public void SetDensity(int value)
        {
            int oldDensity = Density;
            density = value < 1 ? 1 : value;
            if (Density != oldDensity)
            {
                RecalculateCachedValues();
                EnsureDensityHediff();
            }
        }

        public void AddDensity(int amount)
        {
            SetDensity(Density + amount);
        }

        public void RecalculateCachedValues()
        {
            int currentDensity = Density;
            CachedHealthScaleFactor = DenseAnimalUtility.HealthScaleFactor(currentDensity);
            CachedMeleeDamageFactor = DenseAnimalUtility.MeleeDamageFactor(currentDensity);
            CachedVisualScaleFactor = DenseAnimalUtility.VisualScaleFactor(currentDensity);
            CachedHungerRateFactor = currentDensity;

            if (HighDensityAnimalsMod.Settings == null || HighDensityAnimalsMod.Settings.scaleCombatStats)
            {
                CachedArmorSharpOffset = DenseAnimalUtility.ArmorOffset(currentDensity);
                CachedArmorBluntOffset = DenseAnimalUtility.ArmorOffset(currentDensity);
                CachedArmorHeatOffset = DenseAnimalUtility.ArmorOffset(currentDensity, 0.5f);
            }
            else
            {
                CachedArmorSharpOffset = 0f;
                CachedArmorBluntOffset = 0f;
                CachedArmorHeatOffset = 0f;
            }

            SyncExistingDensityHediff();
        }

        private void EnsureDensityHediff()
        {
            if (!(parent is Pawn pawn) || pawn.health?.hediffSet == null || HighDensityAnimalsDefOf.HDA_DenseAnimalMetabolism == null)
            {
                return;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(HighDensityAnimalsDefOf.HDA_DenseAnimalMetabolism);
            if (Density <= 1)
            {
                if (existing != null)
                {
                    pawn.health.RemoveHediff(existing);
                }

                return;
            }

            if (existing == null)
            {
                existing = HediffMaker.MakeHediff(HighDensityAnimalsDefOf.HDA_DenseAnimalMetabolism, pawn);
                pawn.health.AddHediff(existing);
            }

            existing.Severity = Density;
            if (existing is Hediff_DenseAnimalMetabolism denseHediff)
            {
                denseHediff.ConfigureFromComp(this);
            }
        }

        private void SyncExistingDensityHediff()
        {
            if (!(parent is Pawn pawn) || pawn.health?.hediffSet == null || HighDensityAnimalsDefOf.HDA_DenseAnimalMetabolism == null)
            {
                return;
            }

            if (pawn.health.hediffSet.GetFirstHediffOfDef(HighDensityAnimalsDefOf.HDA_DenseAnimalMetabolism) is Hediff_DenseAnimalMetabolism existing)
            {
                existing.Severity = Density;
                existing.ConfigureFromComp(this);
            }
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

            if ((HighDensityAnimalsMod.Settings == null || HighDensityAnimalsMod.Settings.scaleCombatStats) && stat == StatDefOf.MeleeDamageFactor)
            {
                return CachedMeleeDamageFactor;
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
                ((HighDensityAnimalsMod.Settings == null || HighDensityAnimalsMod.Settings.scaleCombatStats) && stat == StatDefOf.MeleeDamageFactor);

            if (!applies)
            {
                return;
            }

            float factor = stat == StatDefOf.MeleeDamageFactor ? CachedMeleeDamageFactor : Density;
            sb.AppendLine(whitespace + "High density: x" + factor.ToString("0.##"));
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
