using System;
using System.Collections.Generic;
using System.Linq;
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

    [HarmonyPatch(typeof(Pawn), "get_HealthScale")]
    public static class Patch_Pawn_HealthScale
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            if (HighDensityAnimalsMod.Settings == null || !HighDensityAnimalsMod.Settings.scaleHealth)
            {
                return;
            }

            CompDenseAnimal comp = __instance.GetComp<CompDenseAnimal>();
            if (comp == null || comp.Density <= 1)
            {
                return;
            }

            __result *= comp.CachedHealthScaleFactor;
        }
    }

    [HarmonyPatch(typeof(StatWorker_MarketValue), nameof(StatWorker_MarketValue.GetValueUnfinalized))]
    public static class Patch_DenseThing_MarketValue
    {
        public static void Postfix(StatRequest req, StatDef ___stat, ref float __result)
        {
            // Vanilla's pawn branch returns before consulting ThingComp stat factors.
            if (___stat != StatDefOf.MarketValue || HighDensityAnimalsMod.Settings == null ||
                !HighDensityAnimalsMod.Settings.scaleMarketValue) return;
            if (req.Thing is Pawn pawn) __result *= DenseAnimalUtility.GetDensity(pawn);
            else if (req.Thing != null) __result *= DenseEggUtility.GetEggDensity(req.Thing);
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
                __result.stackCount = 1;
                DenseEggUtility.GetComp(__result)?.Configure(embryoDensity, pawn.def);
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
            else if (pawn.RaceProps.Animal && motherOrEgg is Pawn mother &&
                Patch_HediffPregnant_DoBirthSpawn.CurrentBirth is { } birth && birth.Mother == mother)
            {
                DenseAnimalUtility.GetComp(pawn)?.SetDensity(birth.Density);
            }
        }
    }

    [HarmonyPatch(typeof(Hediff_Pregnant), "DoBirthSpawn")]
    public static class Patch_HediffPregnant_DoBirthSpawn
    {
        [ThreadStatic]
        private static BirthContext currentBirth;

        public sealed class BirthContext
        {
            public readonly Pawn Mother;
            public readonly int Density;

            public BirthContext(Pawn mother, Pawn father)
            {
                Mother = mother;
                Density = DenseAnimalUtility.GetOffspringDensity(mother, father);
            }
        }

        internal static BirthContext CurrentBirth => currentBirth;

        public static void Prefix(Pawn mother, Pawn father, out BirthContext __state)
        {
            __state = currentBirth;
            currentBirth = mother?.RaceProps.Animal == true ? new BirthContext(mother, father) : null;
        }

        public static void Finalizer(BirthContext __state)
        {
            // Restore the enclosing birth even when another patch or spawning throws.
            currentBirth = __state;
        }
    }

    [HarmonyPatch(typeof(RaceProperties), nameof(RaceProperties.SpecialDisplayStats))]
    public static class Patch_RaceProperties_SpecialDisplayStats
    {
        public static void Postfix(ThingDef parentDef, StatRequest req, ref IEnumerable<StatDrawEntry> __result)
        {
            int density = DenseAnimalUtility.GetDensity(req.Pawn ?? req.Thing as Pawn);
            if (density <= 1 || __result == null) return;
            ThingDef def = parentDef;
            // Match the game's translation keys, then calculate from numbers rather than parsing
            // localized display strings. Individual product values remain per unit.
            var replacements = new Dictionary<string, float>
            {
                { "Stat_Animal_AdultMeatAmount", AnimalProductionUtility.AdultMeatAmount(def) * density },
                { "Stat_Animal_MeatPerDayDuringGrowth", AnimalProductionUtility.MeatPerDayDuringGrowth(def) * density },
                { "Stat_Animal_GrassToMaintain", AnimalProductionUtility.GrassToMaintain(def) * density },
                { "Stat_Animal_EggsPerYear", AnimalProductionUtility.EggsPerYear(def) * density },
                { "Stat_Animal_EggNutritionYearly", AnimalProductionUtility.EggNutritionPerYear(def) * density },
                { "Stat_Animal_EggMarketValueYearly", AnimalProductionUtility.EggMarketValuePerYear(def) * density },
                { "Stat_Animal_MilkAmount", (def.GetCompProperties<CompProperties_Milkable>()?.milkAmount ?? 0) * density },
                { "Stat_Animal_MilkPerYear", AnimalProductionUtility.MilkPerYear(def) * density },
                { "Stat_Animal_MilkValuePerYear", AnimalProductionUtility.MilkMarketValuePerYear(def) * density },
                { "Stat_Animal_WoolAmount", (def.GetCompProperties<CompProperties_Shearable>()?.woolAmount ?? 0) * density },
                { "Stat_Animal_WoolPerYear", AnimalProductionUtility.WoolPerYear(def) * density },
                { "Stat_Animal_WoolValuePerYear", AnimalProductionUtility.WoolMarketValuePerYear(def) * density }
            };
            List<StatDrawEntry> entries = __result.ToList();
            for (int i = 0; i < entries.Count; i++)
                foreach (var item in replacements)
                    if (entries[i].LabelCap == item.Key.Translate().CapitalizeFirst())
                    {
                        string value = item.Key.Contains("Value") ? item.Value.ToStringMoney() : item.Value.ToString("0.##");
                        entries[i] = new StatDrawEntry(StatCategoryDefOf.AnimalProductivity, item.Key.Translate(), value,
                            (item.Key + "Desc").Translate() + "\n\n" + "HDA_DensityExplanation".Translate(density),
                            entries[i].DisplayPriorityWithinCategory);
                        break;
                    }
            __result = entries;
        }
    }
}
