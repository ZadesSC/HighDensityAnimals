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
            return "High Density Animals";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            bool oldScaleCombatStats = Settings.scaleCombatStats;
            bool oldScaleVisualSize = Settings.scaleVisualSize;
            float oldMaxVisualScale = Settings.maxVisualScale;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("The densifier fuses animals two at a time until the configured target density is reached.");
            listing.GapLine();
            listing.CheckboxLabeled("Require same sex for fusion", ref Settings.requireSameSex);
            listing.CheckboxLabeled("Require adults only", ref Settings.requireAdultsOnly);
            listing.CheckboxLabeled("Scale health by density", ref Settings.scaleHealth);
            listing.CheckboxLabeled("Scale combat stats by density", ref Settings.scaleCombatStats);
            listing.CheckboxLabeled("Scale visual size by density", ref Settings.scaleVisualSize);
            listing.CheckboxLabeled("Scale market value by density", ref Settings.scaleMarketValue);
            listing.Gap();
            listing.Label("Max density: " + Settings.maxDensity);
            Settings.maxDensity = (int)listing.Slider(Settings.maxDensity, 2f, 100f);
            listing.Label("Max visual scale: " + Settings.maxVisualScale.ToString("0.00") + "x");
            Settings.maxVisualScale = listing.Slider(Settings.maxVisualScale, 1f, 3f);
            listing.End();
            if (oldScaleCombatStats != Settings.scaleCombatStats ||
                oldScaleVisualSize != Settings.scaleVisualSize ||
                oldMaxVisualScale != Settings.maxVisualScale)
            {
                DenseAnimalUtility.RefreshAllSpawnedDenseAnimalCaches();
            }

            Settings.Write();
        }
    }
}
