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

if ! rg -q "StatDefOf\\.MeleeDamageFactor" Source/Comps/CompDenseAnimal.cs; then
  echo "density refactor: missing melee damage stat factor"
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

if ! rg -q "CachedHealthScaleFactor|CachedMeleeDamageFactor|CachedVisualScaleFactor|CachedArmorSharpOffset|RecalculateCachedValues" Source/Comps/CompDenseAnimal.cs; then
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
