using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HighDensityAnimals
{
    public sealed class Building_AnimalDensifier : Building, IStoreSettingsParent, IThingHolder, ISuspendableThingHolder
    {
        private const int FusionTicks = 3000;

        private ThingOwner innerContainer;
        private StorageSettings allowedNutritionSettings;
        private CompPowerTrader powerComp;
        private ThingDef selectedSpecies;
        private int targetDensity = 2;
        private bool automationEnabled = true;
        private bool preventDensityWaste = true;
        private DensifierSexFilter sexFilter = DensifierSexFilter.Both;
        private float storedNutrition;
        private int cycleEndTick = -1;
        private int cycleTicksRemaining = -1;
        private Pawn cyclePrimary;
        private Pawn cycleSecondary;
        private int cyclePrimaryDensity;
        private int cycleSecondaryDensity;
        private float cycleNutrition;
        private int cycleResultDensity;

        public Building_AnimalDensifier()
        {
            innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, innerContainer);
        }

        public bool StorageTabVisible => true;
        public bool IsContentsSuspended => true;

        private Pawn SlotA => GetAnimalSlot(0);
        private Pawn SlotB => GetAnimalSlot(1);
        private int AnimalCount => CountLoadedAnimals();
        private bool PoweredOn => powerComp == null || powerComp.PowerOn;
        public bool ActiveCycle => cycleTicksRemaining >= 0;
        public int CycleTicksRemaining => cycleTicksRemaining;
        public ThingDef SelectedSpecies => selectedSpecies;
        public int TargetDensity => targetDensity;
        public bool AutomationEnabled => automationEnabled;
        public bool PreventDensityWaste => preventDensityWaste;
        public DensifierSexFilter SexFilter => sexFilter;
        private int DensityLimit => Mathf.Min(targetDensity, GetMaxDensity());
        public float StoredNutrition => NutritionStored;
        public bool NeedsAnimal => automationEnabled && selectedSpecies != null && !ActiveCycle && CurrentDensity < targetDensity && AnimalCount < 2;
        public bool NeedsNutrition => automationEnabled && !ActiveCycle && selectedSpecies != null && CurrentDensity < targetDensity && NutritionNeededForNextCycle > 0.001f;
        public int CurrentDensity => SlotA != null ? DenseAnimalUtility.GetDensity(SlotA) : 0;
        public float NutritionNeededForNextCycle => Mathf.Max(0f, NutritionRequiredForNextCycle - NutritionStored);

        public float NutritionRequiredForNextCycle
        {
            get
            {
                if (selectedSpecies == null || CurrentDensity >= targetDensity)
                {
                    return 0f;
                }

                int left = SlotA != null ? DenseAnimalUtility.GetDensity(SlotA) : 1;
                int right = SlotB != null ? DenseAnimalUtility.GetDensity(SlotB) : 1;
                return left + right;
            }
        }

        private float NutritionStored
        {
            get
            {
                float total = storedNutrition;
                for (int i = 0; i < innerContainer.Count; i++)
                {
                    Thing thing = innerContainer[i];
                    if (thing is Pawn)
                    {
                        continue;
                    }

                    if (IsUsableNutrition(thing))
                    {
                        total += NutritionPerUnit(thing) * thing.stackCount;
                    }
                }

                return total;
            }
        }

        public override void PostMake()
        {
            base.PostMake();
            EnsureAllowedNutritionSettings();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
            EnsureAllowedNutritionSettings();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref selectedSpecies, "selectedSpecies");
            Scribe_Values.Look(ref targetDensity, "targetDensity", 2);
            Scribe_Values.Look(ref automationEnabled, "automationEnabled", true);
            Scribe_Values.Look(ref preventDensityWaste, "preventDensityWaste", true);
            Scribe_Values.Look(ref sexFilter, "sexFilter", DensifierSexFilter.Both);
            Scribe_Values.Look(ref storedNutrition, "storedNutrition", 0f);
            Scribe_Values.Look(ref cycleTicksRemaining, "cycleTicksRemaining", -1);
            Scribe_Values.Look(ref cycleEndTick, "cycleEndTick", -1); // Legacy save migration only.
            Scribe_References.Look(ref cyclePrimary, "cyclePrimary");
            Scribe_References.Look(ref cycleSecondary, "cycleSecondary");
            Scribe_Values.Look(ref cyclePrimaryDensity, "cyclePrimaryDensity");
            Scribe_Values.Look(ref cycleSecondaryDensity, "cycleSecondaryDensity");
            Scribe_Values.Look(ref cycleNutrition, "cycleNutrition");
            Scribe_Values.Look(ref cycleResultDensity, "cycleResultDensity");
            Scribe_Deep.Look(ref allowedNutritionSettings, "allowedNutritionSettings", this);
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer == null)
            {
                innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!IsValidSexFilter(sexFilter)) sexFilter = DensifierSexFilter.Both;
                EnsureAllowedNutritionSettings();
                if (cycleTicksRemaining < 0 && cycleEndTick >= 0)
                {
                    cycleTicksRemaining = Mathf.Clamp(cycleEndTick - Find.TickManager.TicksGame, 1, FusionTicks);
                }
                if (ActiveCycle && cyclePrimary == null && cycleSecondary == null)
                {
                    CaptureCycleInputs();
                }
                // Older accepted cycles always preserved their full combined density.
                if (ActiveCycle && cycleResultDensity <= 0)
                    cycleResultDensity = cyclePrimaryDensity + cycleSecondaryDensity;
                // Old saves could disable same-sex pairing. Refund without ejecting or
                // consuming either animal, even if the saved cycle was paused.
                if (ActiveCycle && cyclePrimary != null && cycleSecondary != null &&
                    (cyclePrimary.gender != cycleSecondary.gender || !AllowsSex(cyclePrimary) || !AllowsSex(cycleSecondary)))
                    CancelCycle();
                cycleEndTick = -1;
            }
        }

        public override void TickRare()
        {
            base.TickRare();

            if (!PoweredOn || !automationEnabled)
            {
                return;
            }

            if (ActiveCycle)
            {
                // Contents are deliberately in stasis. Only powered rare intervals count.
                cycleTicksRemaining = Mathf.Max(0, cycleTicksRemaining - GenTicks.TickRareInterval);
                if (cycleTicksRemaining == 0) CompleteCycle();

                return;
            }

            targetDensity = Mathf.Clamp(targetDensity, 2, GetMaxDensity());
            if (SlotA != null && DenseAnimalUtility.GetDensity(SlotA) >= targetDensity)
            {
                EjectPrimary();
                return;
            }

            if (CanStartCycleNow())
            {
                StartCycle();
            }
        }

        public override string GetInspectString()
        {
            string baseString = base.GetInspectString();
            List<string> lines = new List<string>();
            if (!baseString.NullOrEmpty())
            {
                lines.Add(baseString);
            }

            lines.Add("HDA_Configuredspecies".Translate() + (selectedSpecies?.label?.CapitalizeFirst() ?? "None".Translate()));
            lines.Add("HDA_Targetdensity".Translate() + targetDensity);
            lines.Add("HDA_DensifySex".Translate(SexFilterLabel(sexFilter)));
            lines.Add("HDA_Storednutrition".Translate() + NutritionStored.ToString("0.0"));
            lines.Add("HDA_State".Translate() + GetStateLabel());

            if (SlotA != null)
            {
                lines.Add("HDA_PrimaryAnimal".Translate(SlotA.LabelShortCap, DenseAnimalUtility.GetDensity(SlotA)));
            }

            if (SlotB != null)
            {
                lines.Add("HDA_SecondaryAnimal".Translate(SlotB.LabelShortCap, DenseAnimalUtility.GetDensity(SlotB)));
            }

            if (ActiveCycle)
            {
                float ticksRemaining = cycleTicksRemaining;
                lines.Add("HDA_Cycleprogress".Translate() + (1f - ticksRemaining / FusionTicks).ToStringPercent());
            }
            else if (selectedSpecies != null && CurrentDensity < targetDensity)
            {
                lines.Add("HDA_Nutritionneededfornextcycle".Translate() + NutritionNeededForNextCycle.ToString("0.0"));
            }

            return lines.Where(line => !line.NullOrEmpty()).ToLineList("  - ");
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            EnsureAllowedNutritionSettings();

            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            foreach (Gizmo gizmo in StorageSettingsClipboard.CopyPasteGizmosFor(allowedNutritionSettings))
            {
                yield return gizmo;
            }

            yield return new Command_Toggle
            {
                defaultLabel = "HDA_Automation".Translate(),
                defaultDesc = "HDA_Automaticallypullinanimalsandnutritionuntilthetargetdensityisreached".Translate(),
                isActive = () => automationEnabled,
                toggleAction = () => automationEnabled = !automationEnabled
            };

            Command_Toggle wasteProtection = new Command_Toggle
            {
                defaultLabel = "HDA_PreventDensityWaste".Translate(),
                defaultDesc = "HDA_PreventDensityWasteDescription".Translate(),
                isActive = () => preventDensityWaste,
                toggleAction = () => preventDensityWaste = !preventDensityWaste
            };
            if (ActiveCycle) wasteProtection.Disable("HDA_Finishorcancelthecurrentcyclefirst".Translate());
            yield return wasteProtection;

            Command_Action speciesCommand = new Command_Action
            {
                defaultLabel = "HDA_SetSpecies".Translate(),
                defaultDesc = "HDA_Choosewhichanimalspeciesthismachineshoulddensify".Translate(),
                icon = GetSpeciesCommandIcon(),
                iconAngle = GetSpeciesCommandIconAngle(),
                iconOffset = GetSpeciesCommandIconOffset(),
                defaultIconColor = GetSpeciesCommandIconColor(),
                overrideMaterial = GetSpeciesCommandIconMaterial(),
                action = OpenSpeciesMenu
            };
            if (AnimalCount > 0 || ActiveCycle) speciesCommand.Disable("HDA_Ejecttheloadedanimalsbeforechangingspecies".Translate());
            yield return speciesCommand;

            Command_Action sexCommand = new Command_Action
            {
                defaultLabel = "HDA_DensifySex".Translate(SexFilterLabel(sexFilter)),
                defaultDesc = "HDA_DensifySexDescription".Translate(),
                icon = (sexFilter == DensifierSexFilter.FemalesOnly ? Gender.Female :
                    sexFilter == DensifierSexFilter.MalesOnly ? Gender.Male : Gender.None).GetIcon(),
                action = OpenSexFilterMenu
            };
            if (AnimalCount > 0 || ActiveCycle) sexCommand.Disable("HDA_EjectBeforeChangingSexFilter".Translate());
            yield return sexCommand;

            Command_Action targetDown = new Command_Action
            {
                defaultLabel = "HDA_TargetDecrease".Translate(),
                defaultDesc = "HDA_Decreasethetargetdensity".Translate(),
                action = () => InterfaceChangeTargetDensity(-1)
            };
            if (ActiveCycle) targetDown.Disable("HDA_Finishorcancelthecurrentcyclefirst".Translate());
            else if (targetDensity <= 2)
            {
                targetDown.Disable("HDA_Alreadyatminimumtargetdensity".Translate());
            }
            yield return targetDown;

            Command_Action targetUp = new Command_Action
            {
                defaultLabel = "HDA_TargetIncrease".Translate(),
                defaultDesc = "HDA_Increasethetargetdensity".Translate(),
                action = () => InterfaceChangeTargetDensity(1)
            };
            if (ActiveCycle) targetUp.Disable("HDA_Finishorcancelthecurrentcyclefirst".Translate());
            else if (targetDensity >= GetMaxDensity())
            {
                targetUp.Disable("HDA_Alreadyatmaxtargetdensity".Translate());
            }
            yield return targetUp;

            if (innerContainer.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "HDA_EjectContents".Translate(),
                    defaultDesc = "HDA_Ejectloadedanimalsfromthedensifier".Translate(),
                    action = EjectAll
                };
            }
        }

        private void InterfaceChangeTargetDensity(int offset)
        {
            if (!TrySetTargetDensity(targetDensity + offset)) return;
            if (Spawned)
            {
                MoteMaker.ThrowText(this.TrueCenter() + new Vector3(0.5f, 0f, 0.5f), Map, "×" + targetDensity, Color.white);
            }
        }

        public bool TrySetSpecies(ThingDef species)
        {
            if (AnimalCount > 0 || ActiveCycle || species?.race?.Animal != true) return false;
            selectedSpecies = species;
            return true;
        }

        public bool TrySetTargetDensity(int value)
        {
            if (ActiveCycle || value < 2 || value > GetMaxDensity()) return false;
            targetDensity = value;
            return true;
        }

        private static bool IsValidSexFilter(DensifierSexFilter value)
        {
            return value == DensifierSexFilter.Both || value == DensifierSexFilter.FemalesOnly || value == DensifierSexFilter.MalesOnly;
        }

        public bool TrySetSexFilter(DensifierSexFilter value)
        {
            if (AnimalCount > 0 || ActiveCycle || !IsValidSexFilter(value)) return false;
            sexFilter = value;
            return true;
        }

        private bool AllowsSex(Pawn pawn)
        {
            return sexFilter == DensifierSexFilter.Both ||
                sexFilter == DensifierSexFilter.FemalesOnly && pawn.gender == Gender.Female ||
                sexFilter == DensifierSexFilter.MalesOnly && pawn.gender == Gender.Male;
        }

        private static string SexFilterLabel(DensifierSexFilter value)
        {
            return (value == DensifierSexFilter.FemalesOnly ? "HDA_DensifyFemalesOnly" :
                value == DensifierSexFilter.MalesOnly ? "HDA_DensifyMalesOnly" : "HDA_DensifyBoth").Translate();
        }

        private void OpenSexFilterMenu()
        {
            if (AnimalCount > 0 || ActiveCycle) return;
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (DensifierSexFilter value in new[] { DensifierSexFilter.Both, DensifierSexFilter.FemalesOnly, DensifierSexFilter.MalesOnly })
                options.Add(new FloatMenuOption(SexFilterLabel(value), () => TrySetSexFilter(value)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public bool AcceptsAnimal(Pawn pawn)
        {
            if (ActiveCycle || pawn == SlotA || pawn == SlotB || pawn == null || selectedSpecies == null || DenseAnimalUtility.GetComp(pawn) == null || !DenseAnimalUtility.IsEligibleFusionAnimal(pawn, false))
            {
                return false;
            }

            if (AnimalCount >= 2 || pawn.def != selectedSpecies || !AllowsSex(pawn))
            {
                return false;
            }

            int pawnDensity = DenseAnimalUtility.GetDensity(pawn);
            if (pawnDensity >= targetDensity)
            {
                return false;
            }

            if (SlotA != null && !DenseAnimalUtility.CanFuseTogether(SlotA, pawn))
            {
                return false;
            }

            if (SlotA == null)
            {
                return pawnDensity < GetMaxDensity();
            }

            int totalDensity = DenseAnimalUtility.GetDensity(SlotA) + pawnDensity;
            return AllowsCombinedDensity(totalDensity);
        }

        public bool TryInsertAnimal(Pawn pawn)
        {
            if (!AcceptsAnimal(pawn))
            {
                return false;
            }

            if (pawn.ParentHolder is Pawn_CarryTracker tracker)
                return tracker.innerContainer.TryTransferToContainer(pawn, innerContainer, false);
            Map originalMap = pawn.Map;
            IntVec3 originalPosition = pawn.Position;
            if (pawn.Spawned) pawn.DeSpawn();
            if (innerContainer.TryAdd(pawn, false)) return true;
            if (originalMap != null) GenSpawn.Spawn(pawn, originalPosition, originalMap);
            return false;
        }

        private bool CanHaulAnimal(Pawn animal, Pawn worker)
        {
            if (!animal.Spawned || !AcceptsAnimal(animal)) return false;
            if (worker == null) return !animal.IsForbidden(Faction.OfPlayer);
            return !animal.IsForbidden(worker) && worker.CanReserveAndReach(animal, PathEndMode.ClosestTouch, Danger.Some);
        }

        public Pawn FindNextAnimal(Pawn worker)
        {
            if (!NeedsAnimal || Map == null) return null;
            List<Pawn> candidates = Map.mapPawns.AllPawnsSpawned
                .Where(p => CanHaulAnimal(p, worker))
                .OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat).ToList();
            if (SlotA != null) return candidates.FirstOrDefault();
            // Do not commit a lone animal when a different compatible pair is available.
            foreach (Pawn first in candidates)
                if (candidates.Any(second => second != first && DenseAnimalUtility.CanFuseTogether(first, second) &&
                    AllowsCombinedDensity(DenseAnimalUtility.GetDensity(first) + DenseAnimalUtility.GetDensity(second))))
                    return first;
            return null;
        }

        public bool CanAcceptNutrition(Thing thing)
        {
            if (!IsUsableNutrition(thing))
            {
                return false;
            }

            EnsureAllowedNutritionSettings();
            return allowedNutritionSettings.AllowedToAccept(thing);
        }

        public void AbsorbNutrition(Thing thing)
        {
            if (thing == null)
            {
                return;
            }

            if (!CanAcceptNutrition(thing)) return;
            storedNutrition += NutritionPerUnit(thing) * thing.stackCount;
            thing.Destroy(DestroyMode.Vanish);
        }

        public StorageSettings GetStoreSettings()
        {
            EnsureAllowedNutritionSettings();
            return allowedNutritionSettings;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return def.building.fixedStorageSettings;
        }

        public void Notify_SettingsChanged()
        {
        }

        private void OpenSpeciesMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            IEnumerable<ThingDef> speciesDefs = Map.mapPawns.AllPawnsSpawned
                .Where(DenseAnimalUtility.CanBeFused)
                .Select(p => p.def)
                .Distinct()
                .OrderBy(def => def.label);

            foreach (ThingDef def in speciesDefs)
            {
                options.Add(new FloatMenuOption(def.label.CapitalizeFirst(), () =>
                {
                    TrySetSpecies(def);
                }));
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("HDA_Noeligiblecolonyanimals".Translate(), null));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private string GetStateLabel()
        {
            if (!automationEnabled)
            {
                return "HDA_Disabled".Translate();
            }

            if (!PoweredOn)
            {
                return "HDA_Nopower".Translate();
            }

            if (selectedSpecies == null)
            {
                return "HDA_Awaitingspecies".Translate();
            }

            if (ActiveCycle)
            {
                return "HDA_Fusing".Translate();
            }

            if (SlotA == null)
            {
                return "HDA_Awaitingfirstanimal".Translate();
            }

            if (DenseAnimalUtility.GetDensity(SlotA) >= targetDensity)
            {
                return "HDA_Readytoeject".Translate();
            }

            if (SlotB == null)
            {
                return FindNextAnimal(null) == null ? "HDA_Nocompatibleavailablepartner".Translate() :
                    (NeedsNutrition ? "HDA_Awaitingnutritionandsecondanimal".Translate() : "HDA_Awaitingsecondanimal".Translate());
            }

            if (!AllowsSex(SlotA) || !AllowsSex(SlotB) || !DenseAnimalUtility.CanFuseTogether(SlotA, SlotB) ||
                !AllowsCombinedDensity(CurrentDensity + DenseAnimalUtility.GetDensity(SlotB)))
                return "HDA_IncompatibleContents".Translate();
            if (NeedsNutrition)
            {
                return "HDA_Awaitingnutrition".Translate();
            }

            return "HDA_Ready".Translate();
        }

        private bool AllowsCombinedDensity(int density)
        {
            return !preventDensityWaste || density <= DensityLimit;
        }

        private bool CanStartCycleNow()
        {
            if (SlotA == null || SlotB == null || DenseAnimalUtility.GetComp(SlotA) == null ||
                !AllowsSex(SlotA) || !AllowsSex(SlotB) || !DenseAnimalUtility.CanFuseTogether(SlotA, SlotB)) return false;
            int result = DenseAnimalUtility.GetDensity(SlotA) + DenseAnimalUtility.GetDensity(SlotB);
            return CurrentDensity < DensityLimit && AllowsCombinedDensity(result) && NutritionStored + 0.001f >= result;
        }

        private void CaptureCycleInputs()
        {
            cyclePrimary = SlotA;
            cycleSecondary = SlotB;
            cyclePrimaryDensity = DenseAnimalUtility.GetDensity(cyclePrimary);
            cycleSecondaryDensity = DenseAnimalUtility.GetDensity(cycleSecondary);
            cycleNutrition = cyclePrimaryDensity + cycleSecondaryDensity;
            cycleResultDensity = preventDensityWaste ? cyclePrimaryDensity + cycleSecondaryDensity :
                Mathf.Min(cyclePrimaryDensity + cycleSecondaryDensity, DensityLimit);
        }

        private void StartCycle()
        {
            if (!CanStartCycleNow()) return;
            CaptureCycleInputs();
            if (!TryConsumeNutrition(cycleNutrition)) { ClearCycle(); return; }
            cycleTicksRemaining = FusionTicks;
        }

        private void CompleteCycle()
        {
            // Preserve the output accepted at cycle start, including any intentional density loss.
            CompDenseAnimal comp = DenseAnimalUtility.GetComp(cyclePrimary);
            if (comp == null || cycleSecondary == null || cyclePrimary != SlotA || cycleSecondary != SlotB ||
                cyclePrimary.Dead || cycleSecondary.Dead ||
                cyclePrimary.gender != cycleSecondary.gender || !AllowsSex(cyclePrimary) || !AllowsSex(cycleSecondary) ||
                DenseAnimalUtility.GetDensity(cyclePrimary) != cyclePrimaryDensity ||
                DenseAnimalUtility.GetDensity(cycleSecondary) != cycleSecondaryDensity)
            { CancelCycle(); return; }
            Pawn consumed = cycleSecondary;
            comp.SetDensity(cycleResultDensity);
            innerContainer.Remove(consumed);
            consumed.Destroy(DestroyMode.Vanish);
            ClearCycle();
            if (CurrentDensity >= targetDensity || CurrentDensity >= GetMaxDensity() || FindNextAnimal(null) == null)
                EjectPrimary();
        }

        private void ClearCycle()
        {
            cycleTicksRemaining = cycleEndTick = -1;
            cycleNutrition = 0f;
            cyclePrimary = cycleSecondary = null;
            cyclePrimaryDensity = cycleSecondaryDensity = 0;
            cycleResultDensity = 0;
        }

        private void CancelCycle()
        {
            if (ActiveCycle) storedNutrition += cycleNutrition;
            ClearCycle();
        }

        private void EjectPrimary()
        {
            if (SlotA != null && Map != null)
                innerContainer.TryDrop(SlotA, InteractionCell, Map, ThingPlaceMode.Near, out _);
        }

        public void EjectAll()
        {
            CancelCycle();
            if (Map != null) innerContainer.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (!Spawned && innerContainer.Count > 0)
            {
                CancelCycle();
                if (MapHeld != null) innerContainer.TryDropAll(PositionHeld, MapHeld, ThingPlaceMode.Near);
                ThingOwner surroundingContainer = ParentHolder?.GetDirectlyHeldThings();
                if (surroundingContainer != null && surroundingContainer != innerContainer)
                    innerContainer.TryTransferAllToContainer(surroundingContainer, false);
                // Handles legacy/off-map holders without silently discarding living pawns.
                foreach (Pawn pawn in innerContainer.OfType<Pawn>().ToList())
                {
                    innerContainer.Remove(pawn);
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                }
            }
            base.Destroy(mode);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            CancelCycle();
            Map map = Map;
            if (map != null)
            {
                innerContainer.TryDropAll(InteractionCell, map, ThingPlaceMode.Near);
                // A removal cannot leave a holder behind. Direct spawning is the emergency fallback
                // when the normal near-placement search fails (e.g. a completely blocked exit).
                foreach (Thing thing in innerContainer.ToList())
                {
                    IntVec3 cell = map.AllCells.Where(c => c.Standable(map)).OrderBy(c => c.DistanceToSquared(Position)).FirstOrDefault();
                    if (!cell.InBounds(map)) cell = Position;
                    innerContainer.Remove(thing);
                    GenSpawn.Spawn(thing, cell, map);
                }
            }
            base.DeSpawn(mode);
        }

        private int GetMaxDensity()
        {
            return HighDensityAnimalsMod.Settings?.maxDensity ?? 25;
        }

        private Texture GetSpeciesCommandIcon()
        {
            return HasSelectedSpeciesIcon() ? selectedSpecies.uiIcon : TexCommand.Install;
        }

        private Color GetSpeciesCommandIconColor()
        {
            return HasSelectedSpeciesIcon() ? selectedSpecies.uiIconColor : Color.white;
        }

        private float GetSpeciesCommandIconAngle()
        {
            return HasSelectedSpeciesIcon() ? selectedSpecies.uiIconAngle : 0f;
        }

        private Vector2 GetSpeciesCommandIconOffset()
        {
            return HasSelectedSpeciesIcon() ? selectedSpecies.uiIconOffset : Vector2.zero;
        }

        private Material GetSpeciesCommandIconMaterial()
        {
            return HasSelectedSpeciesIcon() ? selectedSpecies.uiIconMaterial : null;
        }

        private bool HasSelectedSpeciesIcon()
        {
            return selectedSpecies != null && selectedSpecies.uiIcon != null && selectedSpecies.uiIcon != BaseContent.BadTex;
        }

        private Pawn GetAnimalSlot(int slot)
        {
            int seen = 0;
            for (int i = 0; i < innerContainer.Count; i++)
            {
                Pawn pawn = innerContainer[i] as Pawn;
                if (pawn == null)
                {
                    continue;
                }

                if (seen == slot)
                {
                    return pawn;
                }

                seen++;
            }

            return null;
        }

        private int CountLoadedAnimals()
        {
            int count = 0;
            for (int i = 0; i < innerContainer.Count; i++)
            {
                if (innerContainer[i] is Pawn)
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryConsumeNutrition(float nutrition)
        {
            if (NutritionStored + 0.001f < nutrition)
            {
                return false;
            }

            if (storedNutrition + 0.001f < nutrition)
            {
                AbsorbNutritionFromContainer(nutrition - storedNutrition);
            }

            storedNutrition -= nutrition;
            if (storedNutrition < 0f)
            {
                storedNutrition = 0f;
            }

            return true;
        }

        private void AbsorbNutritionFromContainer(float nutritionNeeded)
        {
            for (int i = innerContainer.Count - 1; i >= 0 && nutritionNeeded > 0f; i--)
            {
                Thing thing = innerContainer[i];
                if (thing is Pawn)
                {
                    continue;
                }

                if (!IsUsableNutrition(thing))
                {
                    continue;
                }

                float nutritionPerUnit = NutritionPerUnit(thing);
                int count = Mathf.Min(thing.stackCount, Mathf.CeilToInt(nutritionNeeded / nutritionPerUnit));
                Thing consumed = thing.SplitOff(count);
                storedNutrition += nutritionPerUnit * count;
                nutritionNeeded -= nutritionPerUnit * count;
                consumed.Destroy(DestroyMode.Vanish);
            }
        }

        private float NutritionPerUnit(Thing thing)
        {
            return thing?.GetStatValue(StatDefOf.Nutrition) ?? 0f;
        }

        private bool IsUsableNutrition(Thing thing)
        {
            if (thing == null || !thing.def.IsNutritionGivingIngestible || NutritionPerUnit(thing) <= 0f)
            {
                return false;
            }

            return !thing.def.IsEgg || !DenseEggUtility.IsDenseEgg(thing);
        }

        private void EnsureAllowedNutritionSettings()
        {
            if (allowedNutritionSettings != null)
            {
                return;
            }

            allowedNutritionSettings = new StorageSettings(this);
            if (def?.building?.defaultStorageSettings != null)
            {
                allowedNutritionSettings.CopyFrom(def.building.defaultStorageSettings);
            }
        }
    }
}
