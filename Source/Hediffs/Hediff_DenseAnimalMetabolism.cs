using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HighDensityAnimals
{
    public sealed class Hediff_DenseAnimalMetabolism : Hediff
    {
        private readonly StatModifier sharpArmorOffset = new StatModifier { stat = StatDefOf.ArmorRating_Sharp };
        private readonly StatModifier bluntArmorOffset = new StatModifier { stat = StatDefOf.ArmorRating_Blunt };
        private readonly StatModifier heatArmorOffset = new StatModifier { stat = StatDefOf.ArmorRating_Heat };
        private readonly StatModifier meleeDamageFactor = new StatModifier { stat = StatDefOf.MeleeDamageFactor, value = 1f };
        private readonly StatModifier maxNutritionFactor = new StatModifier { stat = StatDefOf.MaxNutrition, value = 1f };

        private readonly HediffStage denseStage = new HediffStage
        {
            becomeVisible = false,
            hungerRateFactor = 1f,
        };

        public Hediff_DenseAnimalMetabolism()
        {
            denseStage.statOffsets = new List<StatModifier>
            {
                sharpArmorOffset,
                bluntArmorOffset,
                heatArmorOffset
            };
            denseStage.statFactors = new List<StatModifier>
            {
                meleeDamageFactor,
                maxNutritionFactor
            };
        }

        public override HediffStage CurStage
        {
            get
            {
                return denseStage;
            }
        }

        public void ConfigureFromComp(CompDenseAnimal comp)
        {
            if (comp == null)
            {
                denseStage.hungerRateFactor = 1f;
                sharpArmorOffset.value = 0f;
                bluntArmorOffset.value = 0f;
                heatArmorOffset.value = 0f;
                meleeDamageFactor.value = 1f;
                maxNutritionFactor.value = 1f;
                return;
            }

            denseStage.hungerRateFactor = comp.CachedHungerRateFactor;
            sharpArmorOffset.value = comp.CachedArmorSharpOffset;
            bluntArmorOffset.value = comp.CachedArmorBluntOffset;
            heatArmorOffset.value = comp.CachedArmorHeatOffset;
            meleeDamageFactor.value = comp.CachedMeleeDamageFactor;
            maxNutritionFactor.value = comp.CachedMaxNutritionFactor;
        }

        public override bool Visible => false;

        public override bool ShouldRemove => pawn == null || Severity <= 1f;
    }
}
