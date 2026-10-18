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
        public float CachedMaxNutritionFactor { get; private set; } = 1f;
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

            if (Scribe.mode == LoadSaveMode.PostLoadInit) RecalculateCachedValues();
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
            CachedVisualScaleFactor = DenseAnimalUtility.VisualScaleFactor(currentDensity);
            CachedHungerRateFactor = currentDensity;
            CachedMaxNutritionFactor = currentDensity;

            if (HighDensityAnimalsMod.Settings == null || HighDensityAnimalsMod.Settings.scaleCombatStats)
            {
                CachedMeleeDamageFactor = DenseAnimalUtility.MeleeDamageFactor(currentDensity);
                CachedArmorSharpOffset = DenseAnimalUtility.ArmorOffset(currentDensity);
                CachedArmorBluntOffset = DenseAnimalUtility.ArmorOffset(currentDensity);
                CachedArmorHeatOffset = DenseAnimalUtility.ArmorOffset(currentDensity, 0.5f);
            }
            else
            {
                CachedMeleeDamageFactor = 1f;
                CachedArmorSharpOffset = 0f;
                CachedArmorBluntOffset = 0f;
                CachedArmorHeatOffset = 0f;
            }

            SyncExistingDensityHediff();
            (parent as Pawn)?.health?.hediffSet?.DirtyCache();
            ClearMaxNutritionCache();
            foreach (StatDef stat in new[] { StatDefOf.MeleeDamageFactor, StatDefOf.ArmorRating_Sharp,
                StatDefOf.ArmorRating_Blunt, StatDefOf.ArmorRating_Heat, StatDefOf.MarketValue })
                stat.Worker.ClearCacheForThing(parent);
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
                    ClearMaxNutritionCache();
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
                ClearMaxNutritionCache();
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

        private void ClearMaxNutritionCache()
        {
            if (parent is not Pawn pawn)
            {
                return;
            }

            StatDefOf.MaxNutrition.Worker.ClearCacheForThing(pawn);
            if (pawn.needs?.food != null && pawn.needs.food.CurLevel > pawn.needs.food.MaxLevel)
            {
                pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
            }
        }

        public override string CompInspectStringExtra()
        {
            if (Density <= 1 || !(parent is Pawn pawn))
            {
                return null;
            }

            return "HDA_AnimalInspection".Translate(Density, pawn.GetKindLabelPlural(Density));
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
                (HighDensityAnimalsMod.Settings != null && HighDensityAnimalsMod.Settings.scaleMarketValue && stat == StatDefOf.MarketValue);

            if (!applies)
            {
                return;
            }

            sb.AppendLine(whitespace + "HDA_Highdensityx".Translate() + Density.ToString("0.##"));
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            if (Density <= 1 || parent is not Pawn pawn)
            {
                yield break;
            }

            yield return new StatDrawEntry(
                StatCategoryDefOf.BasicsPawn,
                "HDA_Density".Translate(),
                Density.ToString(),
                "HDA_Thisanimalrepresentsmultiplefusedanimals".Translate(),
                9990,
                null,
                null,
                false);

            yield return new StatDrawEntry(
                StatCategoryDefOf.AnimalProductivity,
                "HDA_Equivalentanimals".Translate(),
                Density.ToString(),
                "HDA_Mostoutputsarescaledtothismanyanimalsworthofproduction".Translate(),
                9990,
                null,
                null,
                false);
        }
    }
}
