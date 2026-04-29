using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HighDensityAnimals
{
    public sealed class Building_AnimalDensifier : Building, IThingHolder
    {
        private const int FusionTicks = 3000;

        private ThingOwner innerContainer;
        private CompPowerTrader powerComp;
        private ThingDef selectedSpecies;
        private int targetDensity = 2;
        private bool automationEnabled = true;
        private float storedNutrition;
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

        private Pawn SlotA => innerContainer.Count > 0 ? innerContainer[0] as Pawn : null;
        private Pawn SlotB => innerContainer.Count > 1 ? innerContainer[1] as Pawn : null;
        private bool PoweredOn => powerComp == null || powerComp.PowerOn;
        private bool ActiveCycle => cycleTicksRemaining > 0;
        public ThingDef SelectedSpecies => selectedSpecies;
        public int TargetDensity => targetDensity;
        public bool AutomationEnabled => automationEnabled;
        public float StoredNutrition => storedNutrition;
        public bool NeedsAnimal => automationEnabled && selectedSpecies != null && !ActiveCycle && CurrentDensity < targetDensity && innerContainer.Count < 2;
        public bool NeedsNutrition => automationEnabled && !ActiveCycle && selectedSpecies != null && CurrentDensity < targetDensity && storedNutrition + 0.001f < NutritionRequiredForNextCycle;
        public int CurrentDensity => SlotA != null ? DenseAnimalUtility.GetDensity(SlotA) : 0;

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

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref selectedSpecies, "selectedSpecies");
            Scribe_Values.Look(ref targetDensity, "targetDensity", 2);
            Scribe_Values.Look(ref automationEnabled, "automationEnabled", true);
            Scribe_Values.Look(ref storedNutrition, "storedNutrition", 0f);
            Scribe_Values.Look(ref cycleTicksRemaining, "cycleTicksRemaining", -1);
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer == null)
            {
                innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
            }

        }

        protected override void Tick()
        {
            base.Tick();

            if ((Find.TickManager.TicksGame + thingIDNumber) % 30 == 0)
            {
                TryAbsorbNutritionAtInteractionCell();
            }

            if (!PoweredOn || !automationEnabled)
            {
                return;
            }

            if (ActiveCycle)
            {
                cycleTicksRemaining--;
                if (cycleTicksRemaining <= 0)
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
            lines.Add("Stored nutrition: " + storedNutrition.ToString("0.0"));
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
                lines.Add("Cycle progress: " + (1f - (float)cycleTicksRemaining / FusionTicks).ToStringPercent());
            }
            else if (selectedSpecies != null && CurrentDensity < targetDensity)
            {
                lines.Add("Nutrition needed for next cycle: " + NutritionRequiredForNextCycle.ToString("0.0"));
            }

            return lines.Where(line => !line.NullOrEmpty()).ToLineList("  - ");
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
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

            if (innerContainer.Count >= 2 || pawn.def != selectedSpecies)
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

        public bool AcceptsNutrition(Thing thing)
        {
            if (thing == null || !thing.Spawned || !thing.IngestibleNow || !thing.def.IsNutritionGivingIngestible)
            {
                return false;
            }

            if (thing.def.IsEgg && DenseEggUtility.IsDenseEgg(thing))
            {
                return false;
            }

            return FoodUtility.GetNutrition(null, thing, thing.def) > 0f;
        }

        public Thing FindBestNutrition(Pawn worker)
        {
            if (!NeedsNutrition || Map == null)
            {
                return null;
            }

            return GenClosest.ClosestThingReachable(
                InteractionCell,
                Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.Touch,
                TraverseParms.For(worker),
                9999f,
                thing => AcceptsNutrition(thing) && worker.CanReserve(thing, 1, -1, null, false));
        }

        public void AbsorbNutrition(Thing thing)
        {
            if (thing == null)
            {
                return;
            }

            storedNutrition += FoodUtility.GetNutrition(null, thing, thing.def) * thing.stackCount;
            thing.Destroy(DestroyMode.Vanish);
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
                    if (innerContainer.Count == 0)
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

        private void TryAbsorbNutritionAtInteractionCell()
        {
            if (!NeedsNutrition || Map == null)
            {
                return;
            }

            List<Thing> things = InteractionCell.GetThingList(Map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing thing = things[i];
                if (AcceptsNutrition(thing))
                {
                    AbsorbNutrition(thing);
                    if (!NeedsNutrition)
                    {
                        break;
                    }
                }
            }
        }

        private bool CanStartCycleNow()
        {
            if (SlotA == null || SlotB == null)
            {
                return false;
            }

            return storedNutrition + 0.001f >= NutritionRequiredForNextCycle;
        }

        private void StartCycle()
        {
            storedNutrition -= NutritionRequiredForNextCycle;
            if (storedNutrition < 0f)
            {
                storedNutrition = 0f;
            }

            cycleTicksRemaining = FusionTicks;
        }

        private void CompleteCycle()
        {
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

            cycleTicksRemaining = -1;
        }

        private int GetMaxDensity()
        {
            return HighDensityAnimalsMod.Settings?.maxDensity ?? 25;
        }
    }
}
