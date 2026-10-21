using Verse;

namespace HighDensityAnimals
{
    public sealed class HighDensityAnimalsSettings : ModSettings
    {
        public int maxDensity = 25;
        public bool requireSameSex = true;
        public bool requireAdultsOnly = true;
        public bool scaleHealth = true;
        public bool scaleCombatStats = true;
        public bool scaleVisualSize = true;
        public bool linearVisualScaling = false;
        public bool scaleMarketValue = true;
        public float maxVisualScale = 1.75f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref maxDensity, "maxDensity", 25);
            Scribe_Values.Look(ref requireSameSex, "requireSameSex", true);
            Scribe_Values.Look(ref requireAdultsOnly, "requireAdultsOnly", true);
            Scribe_Values.Look(ref scaleHealth, "scaleHealth", true);
            Scribe_Values.Look(ref scaleCombatStats, "scaleCombatStats", true);
            Scribe_Values.Look(ref scaleVisualSize, "scaleVisualSize", true);
            Scribe_Values.Look(ref linearVisualScaling, "linearVisualScaling", false);
            Scribe_Values.Look(ref scaleMarketValue, "scaleMarketValue", true);
            Scribe_Values.Look(ref maxVisualScale, "maxVisualScale", 1.75f);
        }
    }
}
