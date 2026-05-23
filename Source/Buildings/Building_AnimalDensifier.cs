using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HighDensityAnimals
{
    public sealed class Building_AnimalDensifier : Building, IStoreSettingsParent, IThingHolder
    {
        private const int FusionTicks = 3000;
        private static readonly CachedTexture InsertAnimalIcon = new CachedTexture("UI/Icons/HDA_InsertAnimalDensifier");

        private ThingOwner innerContainer;
        private StorageSettings allowedNutritionSettings;
        private CompPowerTrader powerComp;
        private ThingDef selectedSpecies;
        private int targetDensity = 2;
        private bool automationEnabled = true;
        private float storedNutrition;
        private int cycleEndTick = -1;
        private int cycleTicksRemaining = -1;

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

        private Pawn SlotA => GetAnimalSlot(0);
        private Pawn SlotB => GetAnimalSlot(1);
        private int AnimalCount => CountLoadedAnimals();
        private bool PoweredOn => powerComp == null || powerComp.PowerOn;
        private bool ActiveCycle => cycleEndTick >= 0;
        public ThingDef SelectedSpecies => selectedSpecies;
        public int TargetDensity => targetDensity;
        public bool AutomationEnabled => automationEnabled;
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
            Scribe_Values.Look(ref storedNutrition, "storedNutrition", 0f);
            Scribe_Values.Look(ref cycleTicksRemaining, "cycleTicksRemaining", -1);
            Scribe_Values.Look(ref cycleEndTick, "cycleEndTick", -1);
            Scribe_Deep.Look(ref allowedNutritionSettings, "allowedNutritionSettings", this);
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer == null)
            {
                innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureAllowedNutritionSettings();
                if (cycleEndTick < 0 && cycleTicksRemaining > 0)
                {
                    cycleEndTick = Find.TickManager.TicksGame + cycleTicksRemaining;
                }
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
                if (Find.TickManager.TicksGame >= cycleEndTick)
                {
                    CompleteCycle();
                }

                return;
            }

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

            lines.Add("Configured species: " + (selectedSpecies?.label?.CapitalizeFirst() ?? "None"));
            lines.Add("Target density: " + targetDensity);
            lines.Add("Stored nutrition: " + NutritionStored.ToString("0.0"));
            lines.Add("State: " + GetStateLabel());

            if (SlotA != null)
            {
                lines.Add("Primary: " + SlotA.LabelShortCap + " (Density " + DenseAnimalUtility.GetDensity(SlotA) + ")");
            }

            if (SlotB != null)
            {
                lines.Add("Secondary: " + SlotB.LabelShortCap + " (Density " + DenseAnimalUtility.GetDensity(SlotB) + ")");
            }

            if (ActiveCycle)
            {
                float ticksRemaining = Mathf.Max(0, cycleEndTick - Find.TickManager.TicksGame);
                lines.Add("Cycle progress: " + (1f - ticksRemaining / FusionTicks).ToStringPercent());
            }
            else if (selectedSpecies != null && CurrentDensity < targetDensity)
            {
                lines.Add("Nutrition needed for next cycle: " + NutritionNeededForNextCycle.ToString("0.0"));
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
                defaultLabel = "Automation",
                defaultDesc = "Automatically pull in animals and nutrition until the target density is reached.",
                isActive = () => automationEnabled,
                toggleAction = () => automationEnabled = !automationEnabled
            };

            yield return new Command_Action
            {
                defaultLabel = "Set Species",
                defaultDesc = "Choose which animal species this machine should densify.",
                icon = GetSpeciesCommandIcon(),
                iconAngle = GetSpeciesCommandIconAngle(),
                iconOffset = GetSpeciesCommandIconOffset(),
                defaultIconColor = GetSpeciesCommandIconColor(),
                overrideMaterial = GetSpeciesCommandIconMaterial(),
                action = OpenSpeciesMenu
            };

            Command_Action targetDown = new Command_Action
            {
                defaultLabel = "Target -",
                defaultDesc = "Decrease the target density.",
                action = () => targetDensity = Mathf.Max(2, targetDensity - 1)
            };
            if (targetDensity <= 2)
            {
                targetDown.Disable("Already at minimum target density.");
            }
            yield return targetDown;

            Command_Action targetUp = new Command_Action
            {
                defaultLabel = "Target +",
                defaultDesc = "Increase the target density.",
                action = () => targetDensity = Mathf.Min(GetMaxDensity(), targetDensity + 1)
            };
            if (targetDensity >= GetMaxDensity())
            {
                targetUp.Disable("Already at max target density.");
            }
            yield return targetUp;

            if (innerContainer.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Eject Contents",
                    defaultDesc = "Eject loaded animals from the densifier.",
                    action = EjectAll
                };
            }
        }

        public bool AcceptsAnimal(Pawn pawn)
        {
            if (pawn == null || selectedSpecies == null || !DenseAnimalUtility.IsEligibleFusionAnimal(pawn, false))
            {
                return false;
            }

            if (AnimalCount >= 2 || pawn.def != selectedSpecies)
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
                return true;
            }

            int totalDensity = DenseAnimalUtility.GetDensity(SlotA) + pawnDensity;
            return totalDensity <= targetDensity && totalDensity <= GetMaxDensity();
        }

        public bool TryInsertAnimal(Pawn pawn)
        {
            if (!AcceptsAnimal(pawn))
            {
                return false;
            }

            if (pawn.Spawned)
            {
                pawn.DeSpawn();
            }

            return innerContainer.TryAdd(pawn);
        }

        public Pawn FindNextAnimal(Pawn worker)
        {
            if (!NeedsAnimal || Map == null)
            {
                return null;
            }

            return Map.mapPawns.AllPawnsSpawned
                .Where(AcceptsAnimal)
                .Where(p => worker == null || worker.CanReserve(p, 1, -1, null, false))
                .OrderByDescending(p => p.ageTracker.AgeBiologicalYearsFloat)
                .FirstOrDefault();
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

            if (IsUsableNutrition(thing))
            {
                storedNutrition += NutritionPerUnit(thing) * thing.stackCount;
            }

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
                    if (AnimalCount == 0 && !ActiveCycle)
                    {
                        selectedSpecies = def;
                    }
                }));
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("No eligible colony animals", null));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private string GetStateLabel()
        {
            if (!automationEnabled)
            {
                return "Disabled";
            }

            if (!PoweredOn)
            {
                return "No power";
            }

            if (selectedSpecies == null)
            {
                return "Awaiting species";
            }

            if (ActiveCycle)
            {
                return "Fusing";
            }

            if (SlotA == null)
            {
                return "Awaiting first animal";
            }

            if (DenseAnimalUtility.GetDensity(SlotA) >= targetDensity)
            {
                return "Ready to eject";
            }

            if (SlotB == null)
            {
                return NeedsNutrition ? "Awaiting nutrition and second animal" : "Awaiting second animal";
            }

            if (NeedsNutrition)
            {
                return "Awaiting nutrition";
            }

            return "Ready";
        }

        private bool CanStartCycleNow()
        {
            if (SlotA == null || SlotB == null)
            {
                return false;
            }

            return NutritionStored + 0.001f >= NutritionRequiredForNextCycle;
        }

        private void StartCycle()
        {
            if (!TryConsumeNutrition(NutritionRequiredForNextCycle))
            {
                return;
            }

            cycleEndTick = Find.TickManager.TicksGame + FusionTicks;
        }

        private void CompleteCycle()
        {
            cycleEndTick = -1;
            cycleTicksRemaining = -1;

            Pawn survivor = SlotA;
            Pawn consumed = SlotB;
            if (survivor == null || consumed == null)
            {
                return;
            }

            int newDensity = DenseAnimalUtility.GetDensity(survivor) + DenseAnimalUtility.GetDensity(consumed);
            newDensity = Mathf.Min(newDensity, GetMaxDensity());
            DenseAnimalUtility.GetComp(survivor)?.SetDensity(newDensity);

            innerContainer.Remove(consumed);
            consumed.Destroy(DestroyMode.Vanish);

            if (newDensity >= targetDensity)
            {
                EjectPrimary();
            }
        }

        private void EjectPrimary()
        {
            Pawn survivor = SlotA;
            if (survivor == null)
            {
                return;
            }

            innerContainer.Remove(survivor);
            if (!GenPlace.TryPlaceThing(survivor, InteractionCell, Map, ThingPlaceMode.Near))
            {
                innerContainer.TryAdd(survivor);
            }
        }

        private void EjectAll()
        {
            for (int i = innerContainer.Count - 1; i >= 0; i--)
            {
                Thing thing = innerContainer[i];
                innerContainer.Remove(thing);
                GenPlace.TryPlaceThing(thing, InteractionCell, Map, ThingPlaceMode.Near);
            }

            cycleEndTick = -1;
            cycleTicksRemaining = -1;
        }

        private int GetMaxDensity()
        {
            return HighDensityAnimalsMod.Settings?.maxDensity ?? 25;
        }

        private Texture GetSpeciesCommandIcon()
        {
            return HasSelectedSpeciesIcon() ? selectedSpecies.uiIcon : InsertAnimalIcon.Texture;
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
