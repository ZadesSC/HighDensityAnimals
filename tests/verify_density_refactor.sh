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

if ! rg -q "ScaleHealth\\(" Source/Patches/HighDensityAnimalsPatches.cs; then
  echo "density refactor: health patch should use sqrt health scaling helper"
  exit 1
fi
