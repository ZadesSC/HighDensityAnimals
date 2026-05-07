using UnityEngine;
using Verse;

namespace HighDensityAnimals
{
    public sealed class PawnRenderSubWorker_DenseAnimalScale : PawnRenderSubWorker
    {
        public override void TransformScale(PawnRenderNode node, PawnDrawParms parms, ref Vector3 scale)
        {
            if (parms.pawn == null || HighDensityAnimalsMod.Settings != null && !HighDensityAnimalsMod.Settings.scaleVisualSize)
            {
                return;
            }

            int density = DenseAnimalUtility.GetDensity(parms.pawn);
            if (density <= 1)
            {
                return;
            }

            float factor = DenseAnimalUtility.VisualScaleFactor(density);
            scale.x *= factor;
            scale.z *= factor;
        }
    }
}
