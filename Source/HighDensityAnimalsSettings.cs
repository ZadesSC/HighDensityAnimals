using Verse;

namespace HighDensityAnimals
{
    public sealed class HighDensityAnimalsSettings : ModSettings
    {
        public int maxDensity = 25;
        public bool requireSameSex = true;
        public bool requireAdultsOnly = true;
        public bool scaleHealth = true;
        public bool scaleMarketValue = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref maxDensity, "maxDensity", 25);
            Scribe_Values.Look(ref requireSameSex, "requireSameSex", true);
            Scribe_Values.Look(ref requireAdultsOnly, "requireAdultsOnly", true);
            Scribe_Values.Look(ref scaleHealth, "scaleHealth", true);
            Scribe_Values.Look(ref scaleMarketValue, "scaleMarketValue", true);
        }
    }
}
