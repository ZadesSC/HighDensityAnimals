using System.Collections.Generic;
using RimWorld;
using UnityEngine;
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
                int density = Mathf.Max(1, DenseAnimalUtility.GetDensity(pawn));
                denseStage.hungerRateFactor = density;
                if (HighDensityAnimalsMod.Settings == null || HighDensityAnimalsMod.Settings.scaleCombatStats)
                {
                    sharpArmorOffset.value = DenseAnimalUtility.ArmorOffset(density);
                    bluntArmorOffset.value = DenseAnimalUtility.ArmorOffset(density);
                    heatArmorOffset.value = DenseAnimalUtility.ArmorOffset(density, 0.5f);
                }
                else
                {
                    sharpArmorOffset.value = 0f;
                    bluntArmorOffset.value = 0f;
                    heatArmorOffset.value = 0f;
                }

                return denseStage;
            }
        }

        public override bool Visible => false;

        public override bool ShouldRemove => pawn == null || DenseAnimalUtility.GetDensity(pawn) <= 1;
    }
}
