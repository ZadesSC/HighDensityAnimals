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
                return;
            }

            denseStage.hungerRateFactor = comp.CachedHungerRateFactor;
            sharpArmorOffset.value = comp.CachedArmorSharpOffset;
            bluntArmorOffset.value = comp.CachedArmorBluntOffset;
            heatArmorOffset.value = comp.CachedArmorHeatOffset;
        }

        public override bool Visible => false;

        public override bool ShouldRemove => pawn == null || Severity <= 1f;
    }
}
