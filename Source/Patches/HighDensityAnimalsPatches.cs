using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HighDensityAnimals
{
    [HarmonyPatch(typeof(CompMilkable), "get_ResourceAmount")]
    public static class Patch_Milkable_ResourceAmount
    {
        public static void Postfix(CompMilkable __instance, ref int __result)
        {
            __result = DenseAnimalUtility.ScaleCount(__instance.parent as Pawn, __result);
        }
    }

    [HarmonyPatch(typeof(CompShearable), "get_ResourceAmount")]
    public static class Patch_Shearable_ResourceAmount
    {
        public static void Postfix(CompShearable __instance, ref int __result)
        {
            __result = DenseAnimalUtility.ScaleCount(__instance.parent as Pawn, __result);
        }
    }

    [HarmonyPatch(typeof(Need_Food), "FoodFallPerTickAssumingCategory")]
    public static class Patch_NeedFood_FoodFallPerTick
    {
        private static readonly System.Reflection.FieldInfo PawnField = AccessTools.Field(typeof(Need), "pawn");

        public static void Postfix(Need_Food __instance, ref float __result)
        {
            Pawn pawn = PawnField?.GetValue(__instance) as Pawn;
            __result = DenseAnimalUtility.ScaleFloat(pawn, __result);
        }
    }

    [HarmonyPatch(typeof(Pawn), "ButcherProducts")]
    public static class Patch_Pawn_ButcherProducts
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Thing> __result)
        {
            int density = DenseAnimalUtility.GetDensity(__instance);
            if (density <= 1 || __result == null)
            {
                return;
            }

            List<Thing> scaled = new List<Thing>();
            foreach (Thing thing in __result.ToList())
            {
                int totalCount = thing.stackCount * density;
                while (totalCount > 0)
                {
                    Thing split = ThingMaker.MakeThing(thing.def, thing.Stuff);
                    split.stackCount = Math.Min(totalCount, split.def.stackLimit);
                    scaled.Add(split);
                    totalCount -= split.stackCount;
                }
            }

            __result = scaled;
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_HealthScale")]
    public static class Patch_Pawn_HealthScale
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            if (HighDensityAnimalsMod.Settings == null || !HighDensityAnimalsMod.Settings.scaleHealth)
            {
                return;
            }

            __result = DenseAnimalUtility.ScaleFloat(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(CompEggLayer), "ProduceEgg")]
    public static class Patch_CompEggLayer_ProduceEgg
    {
        public static void Postfix(CompEggLayer __instance, ref Thing __result)
        {
            Pawn pawn = __instance.parent as Pawn;
            int density = DenseAnimalUtility.GetDensity(pawn);
            if (__result == null || density <= 1)
            {
                return;
            }

            if (__result.def == __instance.Props.eggFertilizedDef)
            {
                int embryoDensity = density * Math.Max(1, __result.stackCount);
                Thing denseEggThing = __result;

                if (__result.Spawned && __result.stackCount > 1)
                {
                    denseEggThing = __result.SplitOff(1);
                    if (denseEggThing != null)
                    {
                        GenPlace.TryPlaceThing(denseEggThing, __result.PositionHeld, __result.MapHeld, ThingPlaceMode.Near);
                    }
                }
                else if (__result.stackCount > 1)
                {
                    __result.stackCount = 1;
                }

                if (denseEggThing != null)
                {
                    denseEggThing.stackCount = 1;
                    CompDenseEgg denseEgg = DenseEggUtility.GetComp(denseEggThing);
                    if (denseEgg != null)
                    {
                        denseEgg.Configure(embryoDensity, pawn.def);
                    }

                    __result = denseEggThing;
                }
            }
            else
            {
                __result.stackCount *= density;
            }
        }
    }

    [HarmonyPatch(typeof(RimWorld.PawnUtility), "TrySpawnHatchedOrBornPawn")]
    public static class Patch_PawnUtility_TrySpawnHatchedOrBornPawn
    {
        public static void Prefix(Pawn pawn, Thing motherOrEgg)
        {
            int eggDensity = DenseEggUtility.GetEggDensity(motherOrEgg);
            if (eggDensity > 1)
            {
                CompDenseAnimal denseComp = DenseAnimalUtility.GetComp(pawn);
                denseComp?.SetDensity(eggDensity);
            }
        }
    }

    [HarmonyPatch(typeof(Hediff_Pregnant), "DoBirthSpawn")]
    public static class Patch_HediffPregnant_DoBirthSpawn
    {
        [ThreadStatic]
        private static bool reentrant;

        public static void Postfix(Hediff_Pregnant __instance, Pawn mother, Pawn father)
        {
            if (reentrant)
            {
                return;
            }

            int density = DenseAnimalUtility.GetDensity(mother);
            if (density <= 1)
            {
                return;
            }

            try
            {
                reentrant = true;
                for (int i = 1; i < density; i++)
                {
                    Hediff_Pregnant.DoBirthSpawn(mother, father);
                }
            }
            finally
            {
                reentrant = false;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpecialDisplayStats))]
    public static class Patch_Pawn_SpecialDisplayStats
    {
        private static readonly FieldInfo CategoryField = AccessTools.Field(typeof(StatDrawEntry), "category");
        private static readonly FieldInfo ExplanationField = AccessTools.Field(typeof(StatDrawEntry), "explanationText");
        private static readonly FieldInfo OverrideReportTextField = AccessTools.Field(typeof(StatDrawEntry), "overrideReportText");
        private static readonly FieldInfo OverrideReportTitleField = AccessTools.Field(typeof(StatDrawEntry), "overrideReportTitle");
        private static readonly FieldInfo HyperlinksField = AccessTools.Field(typeof(StatDrawEntry), "hyperlinks");
        private static readonly FieldInfo ForceUnfinalizedField = AccessTools.Field(typeof(StatDrawEntry), "forceUnfinalizedMode");
        private static readonly FieldInfo OverridesHideStatsField = AccessTools.Field(typeof(StatDrawEntry), "overridesHideStats");

        public static void Postfix(Pawn __instance, ref IEnumerable<StatDrawEntry> __result)
        {
            int density = DenseAnimalUtility.GetDensity(__instance);
            if (density <= 1 || __result == null)
            {
                return;
            }

            List<StatDrawEntry> entries = __result.ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                StatDrawEntry entry = entries[i];
                string label = entry.LabelCap;
                if (label == "Eggs per year" ||
                    label == "Egg nutrition per year" ||
                    label == "Egg market value per year" ||
                    label == "Grass to maintain" ||
                    label == "Meat per day during growth")
                {
                    entries[i] = ReplaceEntry(entry, density);
                }
            }

            __result = entries;
        }

        private static StatDrawEntry ReplaceEntry(StatDrawEntry original, int density)
        {
            string scaledValue = ScaleValueString(original.ValueString, density);
            string explanation = GetExplanationText(original);
            if (!explanation.NullOrEmpty())
            {
                explanation += "\n\nHigh density: x" + density;
            }
            else
            {
                explanation = "High density: x" + density;
            }

            return new StatDrawEntry(
                GetCategory(original),
                original.LabelCap,
                scaledValue,
                explanation,
                original.DisplayPriorityWithinCategory,
                GetOverrideReportTitle(original),
                GetHyperlinks(original),
                GetForceUnfinalizedMode(original),
                GetOverridesHideStats(original));
        }

        private static StatCategoryDef GetCategory(StatDrawEntry entry)
        {
            return CategoryField?.GetValue(entry) as StatCategoryDef ?? StatCategoryDefOf.AnimalProductivity;
        }

        private static string GetExplanationText(StatDrawEntry entry)
        {
            return OverrideReportTextField?.GetValue(entry) as string ?? ExplanationField?.GetValue(entry) as string;
        }

        private static string GetOverrideReportTitle(StatDrawEntry entry)
        {
            return OverrideReportTitleField?.GetValue(entry) as string;
        }

        private static IEnumerable<Dialog_InfoCard.Hyperlink> GetHyperlinks(StatDrawEntry entry)
        {
            return HyperlinksField?.GetValue(entry) as IEnumerable<Dialog_InfoCard.Hyperlink>;
        }

        private static bool GetForceUnfinalizedMode(StatDrawEntry entry)
        {
            return ForceUnfinalizedField != null && ForceUnfinalizedField.GetValue(entry) is bool value && value;
        }

        private static bool GetOverridesHideStats(StatDrawEntry entry)
        {
            return OverridesHideStatsField != null && OverridesHideStatsField.GetValue(entry) is bool value && value;
        }

        private static string ScaleValueString(string valueString, int density)
        {
            if (valueString.NullOrEmpty())
            {
                return valueString;
            }

            string trimmed = valueString.Trim();
            bool isCurrency = trimmed.Contains("$");
            Match match = Regex.Match(trimmed, @"-?\d+(?:,\d{3})*(?:\.\d+)?");
            if (!match.Success)
            {
                return valueString;
            }

            string numericPart = match.Value.Replace(",", string.Empty);
            if (!float.TryParse(numericPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
            {
                return valueString;
            }

            float scaled = parsed * density;
            int decimals = 0;
            int decimalIndex = match.Value.IndexOf('.');
            if (decimalIndex >= 0)
            {
                decimals = match.Value.Length - decimalIndex - 1;
            }

            string format = decimals > 0 ? "F" + decimals : "0";
            string replacement = scaled.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
            if (isCurrency)
            {
                replacement = "$" + replacement;
            }

            if (trimmed.StartsWith("$") && match.Index == 1)
            {
                return replacement + trimmed.Substring(match.Index + match.Length);
            }

            return valueString.Substring(0, match.Index) + replacement + valueString.Substring(match.Index + match.Length);
        }
    }
}
