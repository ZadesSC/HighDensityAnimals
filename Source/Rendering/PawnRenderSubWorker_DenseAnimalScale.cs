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

            CompDenseAnimal comp = parms.pawn.GetComp<CompDenseAnimal>();
            if (comp == null || comp.Density <= 1)
            {
                return;
            }

            float factor = comp.CachedVisualScaleFactor;
            scale.x *= factor;
            scale.z *= factor;
        }
    }
}
