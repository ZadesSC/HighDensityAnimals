#!/usr/bin/env bash
set -euo pipefail

patch_file="Source/Patches/HighDensityAnimalsPatches.cs"
hediff_file="Defs/HediffDefs/HediffDefs_HighDensityAnimals.xml"

if rg -q "Patch_NeedFood_FoodFallPerTick|FoodFallPerTickAssumingCategory|Patch_Pawn_ButcherProducts|ButcherProducts" "$patch_file"; then
  echo "density refactor: removed food/butcher Harmony patches check failed"
  exit 1
fi

if ! rg -q "HDA_DenseAnimalMetabolism" "$hediff_file"; then
  echo "density refactor: missing dense metabolism hediff def"
  exit 1
fi

if ! rg -q "Hediff_DenseAnimalMetabolism|EnsureDensityHediff" Source; then
  echo "density refactor: missing dense metabolism hediff implementation or sync"
  exit 1
fi

if ! rg -q "HealthScaleFactor|MeleeDamageFactor|VisualScaleFactor|ArmorOffset" Source/Utils/DenseAnimalUtility.cs; then
  echo "density refactor: missing density scaling helper methods"
  exit 1
fi

if ! rg -q "CachedMeleeDamageFactor" Source/Comps/CompDenseAnimal.cs; then
  echo "density refactor: missing cached melee damage factor"
  exit 1
fi

if ! rg -q "CachedMaxNutritionFactor" Source/Comps/CompDenseAnimal.cs; then
  echo "density refactor: missing cached max nutrition factor"
  exit 1
fi

if ! rg -q "StatDefOf\\.MaxNutrition\\.Worker\\.ClearCacheForThing" Source/Comps/CompDenseAnimal.cs; then
  echo "density refactor: max nutrition cache should be cleared when cached density values change"
  exit 1
fi

if rg -q "StatDefOf\\.MeleeDamageFactor" Source/Comps/CompDenseAnimal.cs; then
  echo "density refactor: dense animal comp should not apply melee damage stat factor directly"
  exit 1
fi

if ! rg -q "MeleeDamageFactor|statFactors|CachedMeleeDamageFactor" Source/Hediffs/Hediff_DenseAnimalMetabolism.cs; then
  echo "density refactor: dense animal melee damage should be applied by the cached hediff stage"
  exit 1
fi

if ! rg -q "MaxNutrition|CachedMaxNutritionFactor" Source/Hediffs/Hediff_DenseAnimalMetabolism.cs; then
  echo "density refactor: dense animal max nutrition should be applied by the cached hediff stage"
  exit 1
fi

if rg -q "scaleHealth && stat == StatDefOf\\.MaxHitPoints" Source/Comps/CompDenseAnimal.cs; then
  echo "density refactor: pawn health should not rely on MaxHitPoints stat factor"
  exit 1
fi

if ! rg -q "ArmorRating_Sharp|ArmorRating_Blunt|ArmorRating_Heat" Source/Hediffs/Hediff_DenseAnimalMetabolism.cs; then
  echo "density refactor: missing dense animal armor offsets"
  exit 1
fi

if ! rg -q "PawnRenderSubWorker_DenseAnimalScale|DenseAnimalRenderStartup|TransformScale" Source; then
  echo "density refactor: missing dense animal visual scale render hook"
  exit 1
fi

if ! rg -q "CachedHealthScaleFactor|CachedMeleeDamageFactor|CachedMaxNutritionFactor|CachedVisualScaleFactor|CachedArmorSharpOffset|RecalculateCachedValues" Source/Comps/CompDenseAnimal.cs; then
  echo "density refactor: missing cached density-derived values on dense animal comp"
  exit 1
fi

if rg -q "override void CompTick\\(" Source/Comps/CompDenseAnimal.cs; then
  echo "density refactor: dense animal comp should not tick every game tick"
  exit 1
fi

if rg -q "return DenseAnimalUtility\\.MeleeDamageFactor|float factor = DenseAnimalUtility\\.VisualScaleFactor" Source/Comps/CompDenseAnimal.cs Source/Rendering/PawnRenderSubWorker_DenseAnimalScale.cs; then
  echo "density refactor: hot stat/render paths should read cached values instead of recalculating"
  exit 1
fi

if rg -q "DenseAnimalUtility\\.GetDensity\\(pawn\\)|DenseAnimalUtility\\.ArmorOffset" Source/Hediffs/Hediff_DenseAnimalMetabolism.cs; then
  echo "density refactor: hediff stage should use cached values pushed by comp"
  exit 1
fi

if rg -q "ScaleHealth\\(" Source/Patches/HighDensityAnimalsPatches.cs; then
  echo "density refactor: health patch should read cached health factor directly"
  exit 1
fi

if ! rg -q "<tickerType>Rare</tickerType>" Defs/ThingDefs_Buildings/ThingDef_AnimalDensifier.xml; then
  echo "density refactor: densifier should use rare ticking"
  exit 1
fi

if ! rg -q "ITab_BiosculpterNutritionStorage|fixedStorageSettings|defaultStorageSettings|haulToContainerDuration" Defs/ThingDefs_Buildings/ThingDef_AnimalDensifier.xml; then
  echo "density refactor: densifier should expose vanilla-style nutrition storage settings"
  exit 1
fi

if ! rg -q "IStoreSettingsParent|StorageTabVisible|allowedNutritionSettings|GetStoreSettings|GetParentStoreSettings|Notify_SettingsChanged" Source/Buildings/Building_AnimalDensifier.cs; then
  echo "density refactor: densifier should implement vanilla-style storage settings parent"
  exit 1
fi

if ! rg -q "override void TickRare\\(" Source/Buildings/Building_AnimalDensifier.cs; then
  echo "density refactor: densifier processing should run in TickRare"
  exit 1
fi

if rg -q "protected override void Tick\\(|TryAbsorbNutritionAtInteractionCell|InteractionCell\\.GetThingList|cycleTicksRemaining--" Source/Buildings/Building_AnimalDensifier.cs; then
  echo "density refactor: densifier should not normal-tick or scan the interaction cell for nutrition"
  exit 1
fi

if ! rg -q "cycleEndTick|Find\\.TickManager\\.TicksGame >= cycleEndTick" Source/Buildings/Building_AnimalDensifier.cs; then
  echo "density refactor: densifier cycles should use an absolute end tick"
  exit 1
fi

if ! rg -q "HaulAIUtility\\.HaulToContainerJob|ThingCount|CanAcceptNutrition" Source/WorkGivers/WorkGiver_HaulNutritionToAnimalDensifier.cs; then
  echo "density refactor: nutrition hauling should use vanilla container hauling with stack counts"
  exit 1
fi

if rg -q "HDA_HaulNutritionToDensifier|JobDriver_HaulNutritionToDensifier|PlaceHauledThingInCell" Defs Source; then
  echo "density refactor: custom nutrition drop-at-cell job should be removed"
  exit 1
fi

if [ ! -f "Textures/UI/Icons/HDA_InsertAnimalDensifier.png" ]; then
  echo "density refactor: missing copied densifier insert-animal gizmo icon"
  exit 1
fi

if ! rg -q "HDA_InsertAnimalDensifier|GetSpeciesCommandIcon|selectedSpecies\\.uiIcon" Source/Buildings/Building_AnimalDensifier.cs; then
  echo "density refactor: densifier species gizmo should use copied insert icon until a species is selected"
  exit 1
fi
