using HarmonyLib;
using UnityEngine;
using Verse;

namespace HighDensityAnimals
{
    [StaticConstructorOnStartup]
    public static class HighDensityAnimalsBootstrap
    {
        static HighDensityAnimalsBootstrap()
        {
            new Harmony("zades.highdensityanimals").PatchAll();
            LongEventHandler.ExecuteWhenFinished(HighDensityAnimalsStartup.Initialize);
        }
    }

    public sealed class HighDensityAnimalsMod : Mod
    {
        public static HighDensityAnimalsSettings Settings;

        public HighDensityAnimalsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<HighDensityAnimalsSettings>();
        }

        public override string SettingsCategory()
        {
            return "HDA_HighDensityAnimals".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            bool oldScaleHealth = Settings.scaleHealth;
            bool oldScaleMarketValue = Settings.scaleMarketValue;
            bool oldScaleCombatStats = Settings.scaleCombatStats;
            bool oldScaleVisualSize = Settings.scaleVisualSize;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("HDA_Thedensifierfusesanimalstwoatatimeuntiltheconfiguredtargetdensityisrea".Translate());
            listing.GapLine();
            listing.CheckboxLabeled("HDA_Requireadultsonly".Translate(), ref Settings.requireAdultsOnly);
            listing.CheckboxLabeled("HDA_Scalehealthbydensity".Translate(), ref Settings.scaleHealth);
            listing.CheckboxLabeled("HDA_Scalecombatstatsbydensity".Translate(), ref Settings.scaleCombatStats);
            listing.CheckboxLabeled("HDA_Scalevisualsizebydensity".Translate(), ref Settings.scaleVisualSize);
            listing.CheckboxLabeled("HDA_Scalemarketvaluebydensity".Translate(), ref Settings.scaleMarketValue);
            listing.Gap();
            listing.Label("HDA_Maxdensity".Translate() + Settings.maxDensity);
            Settings.maxDensity = (int)listing.Slider(Settings.maxDensity, 2f, 100f);
            listing.End();
            if (oldScaleHealth != Settings.scaleHealth || oldScaleMarketValue != Settings.scaleMarketValue || oldScaleCombatStats != Settings.scaleCombatStats ||
                oldScaleVisualSize != Settings.scaleVisualSize)
            {
                DenseAnimalUtility.RefreshAllDenseAnimalCaches();
            }

            Settings.Write();
        }
    }
}
