using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using HighDensityAnimals;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

// Separate test mod: only loaded by tests/run.py into disposable profiles.
public sealed class RegressionMod : Mod
{
    public static readonly string Report = Environment.GetEnvironmentVariable("HDA_TEST_REPORT");
    public static int Errors;
    public static readonly Dictionary<Pawn, float> Eaten = new();
    public static bool ObserveIngestion;
    public RegressionMod(ModContentPack pack) : base(pack)
    {
        if (Report == null) throw new InvalidOperationException("Run this harness through tests/run.py.");
        new Harmony("local.hda.regression").Patch(AccessTools.Method(typeof(Log), "Error", new[] { typeof(string) }),
            prefix: new HarmonyMethod(typeof(RegressionMod), nameof(Error)));
        new Harmony("local.hda.regression.ingestion").Patch(AccessTools.Method(typeof(Thing), "Ingested"),
            postfix: new HarmonyMethod(typeof(RegressionMod), nameof(Ingested)));
    }
    public static void Ingested(Thing __instance, Pawn ingester, float __result)
    {
        if (ObserveIngestion && __instance.def == ThingDefOf.RawPotatoes)
            Eaten[ingester] = Eaten.GetValueOrDefault(ingester) + __result;
    }
    public static void Error(string text) { Errors++; Write(new { type = "game_error", text }); }
    public static void Write(object row) => File.AppendAllText(Report, JsonSerializer.Serialize(row) + "\n");
}

public sealed class RegressionSuite : GameComponent
{
    int frames;
    bool done;
    readonly List<Thing> fixture = new();
    Map Map => Find.CurrentMap;
    static Thing blockedPlacement;
    static void Set(object obj, string name, object value) => AccessTools.Field(obj.GetType(), name).SetValue(obj, value);
    static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static void Near(float actual, float expected, string message, float tolerance = .001f) =>
        Assert(Math.Abs(actual - expected) <= tolerance, message + $": expected {expected}, got {actual}");
    public RegressionSuite(Game game) { }
    static readonly string[] Cases = {
        "package", "egg_stacks", "admission", "pair_selection", "blocked_ejection", "lifecycle",
        "fusion_and_pause", "post_cycle_partner", "cap_and_cancel", "nutrition", "production", "combat", "offspring", "natural_offspring", "rendering", "settings",
        "localization_and_ui", "reachability", "negative_controls", "autonomous_cycles", "interrupted_haul", "save_fixture"
    };
    void Case(string id, Action test, bool reset = true)
    {
        int errors = RegressionMod.Errors;
        try
        {
            if (reset) ResetFixture();
            test();
            Assert(RegressionMod.Errors == errors, "Game logged an error during the case");
            RegressionMod.Write(new { type = "case", id, passed = true });
        }
        catch (Exception e) { RegressionMod.Write(new { type = "case", id, passed = false, error = e.ToString() }); }
        finally { HighDensityAnimalsMod.Settings = new HighDensityAnimalsSettings(); blockedPlacement = null; RegressionMod.ObserveIngestion = false; DebugSettings.enableDamage = false; }
    }
    void ResetFixture()
    {
        HighDensityAnimalsMod.Settings = new HighDensityAnimalsSettings();
        foreach (var thing in fixture.ToList())
            if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
        fixture.Clear();
        foreach (Thing food in Map.listerThings.AllThings.Where(t => t.Spawned && t.def.IsNutritionGivingIngestible).ToList()) food.Destroy();
        foreach (Pawn pawn in Map.mapPawns.AllPawnsSpawned.ToList())
            if (pawn.IsColonyAnimal) pawn.DeSpawn();
    }
    Pawn Animal(string kind = "Cow", int density = 1, Gender sex = Gender.Female, float age = 5f)
    {
        Rand.PushState(173 + fixture.Count);
        Pawn pawn;
        try { pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDef.Named(kind), Faction.OfPlayer,
            fixedBiologicalAge: age, fixedChronologicalAge: age, fixedGender: sex)); }
        finally { Rand.PopState(); }
        GenSpawn.Spawn(pawn, Map.Center + new IntVec3(5, 0, 5), Map);
        fixture.Add(pawn);
        Assert(pawn.GetComp<CompDenseAnimal>() != null, "Animal comp missing");
        pawn.GetComp<CompDenseAnimal>().SetDensity(density);
        if (pawn.needs.food != null) pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
        return pawn;
    }
    Pawn Worker()
    {
        Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
            forceGenerateNewPawn: true, forceNoBackstory: true, forceNoIdeo: true, allowAddictions: false,
            fixedBiologicalAge: 25f, fixedChronologicalAge: 25f,
            validatorPreGear: p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && p.health.capacities.CapableOf(PawnCapacityDefOf.Moving)));
        GenSpawn.Spawn(pawn, Map.Center + new IntVec3(6, 0, 6), Map);
        fixture.Add(pawn);
        foreach (var type in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            pawn.workSettings.SetPriority(type, type == WorkTypeDefOf.Hauling ? 1 : 0);
        pawn.needs.food.CurLevel = pawn.needs.food.MaxLevel;
        pawn.needs.rest.CurLevel = 1f;
        if (pawn.needs.joy != null) pawn.needs.joy.CurLevel = 1f;
        if (pawn.needs.mood != null) pawn.needs.mood.CurLevel = 1f;
        for (int hour = 0; hour < 24; hour++) pawn.timetable.SetAssignment(hour, TimeAssignmentDefOf.Work);
        return pawn;
    }
    Building_AnimalDensifier Machine(int target = 2, Rot4? rotation = null, IntVec3? offset = null)
    {
        var machine = (Building_AnimalDensifier)ThingMaker.MakeThing(ThingDef.Named("HDA_AnimalDensifier"));
        machine.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(machine, Map.Center + (offset ?? IntVec3.Zero), Map, rotation ?? Rot4.North);
        machine.GetComp<CompPowerTrader>().PowerOn = true;
        fixture.Add(machine);
        Assert(machine.TrySetSpecies(ThingDef.Named("Cow")), "Set species through production controls");
        Assert(machine.TrySetTargetDensity(target), "Set target through production controls");
        return machine;
    }
    Thing Food(int count = 75, ThingDef def = null)
    {
        Thing food = null;
        while (count > 0) {
            food = ThingMaker.MakeThing(def ?? ThingDefOf.RawPotatoes); food.stackCount = Math.Min(count, food.def.stackLimit); count -= food.stackCount;
            GenPlace.TryPlaceThing(food, Map.Center + new IntVec3(4, 0, 4), Map, ThingPlaceMode.Near);
            fixture.Add(food);
        }
        return food;
    }
    void Load(Building_AnimalDensifier machine, Pawn a, Pawn b, int potatoes = 120)
    {
        Assert(machine.TryInsertAnimal(a), "Insert first animal");
        Assert(machine.TryInsertAnimal(b), "Insert second animal");
        if (potatoes > 0)
        {
            var food = ThingMaker.MakeThing(ThingDefOf.RawPotatoes); food.stackCount = potatoes;
            Assert(machine.GetDirectlyHeldThings().TryAdd(food), "Load real food into container");
        }
    }
    static Command_Toggle Automation(Building_AnimalDensifier machine) => machine.GetGizmos().OfType<Command_Toggle>()
        .Single(c => c.defaultLabel == "HDA_Automation".Translate().ToString());
    void Advance(int ticks) { for (int i = 0; i < ticks; i++) Find.TickManager.DoSingleTick(); }
    void FinishByNormalTicks(Building_AnimalDensifier machine)
    {
        for (int i = 0; i < 3500 && machine.ActiveCycle; i++) Find.TickManager.DoSingleTick();
        Assert(!machine.ActiveCycle, "Cycle did not complete within powered duration plus one rare interval");
    }
    public static bool PlacementPrefix(Thing __0, ref bool __result)
    {
        if (__0 != blockedPlacement) return true;
        __result = false; return false;
    }
    public override void GameComponentUpdate()
    {
        if (done || Current.ProgramState != ProgramState.Playing || Map == null || LongEventHandler.ShouldWaitForEvent || ++frames < 60) return;
        done = true;
        Find.TickManager.Pause();
        try
        {
            DebugSettings.enableDamage = false; DebugSettings.enableStoryteller = false;
            DebugSettings.enableRandomMentalStates = false; DebugSettings.enableRandomDiseases = false;
            DebugSettings.unlimitedPower = true; Prefs.AutosaveIntervalDays = 999;
            if (Environment.GetEnvironmentVariable("HDA_TEST_PHASE") is not ("prepare" or "benchmark")) { Resume(); return; }
            foreach (Pawn pawn in Map.mapPawns.FreeColonistsSpawned.ToList()) pawn.drafter.Drafted = true;
            foreach (IntVec3 cell in CellRect.CenteredOn(Map.Center, 25).Cells)
            {
                foreach (Thing thing in cell.GetThingList(Map).ToList())
                    if (thing is Building || thing is Plant) { if (thing.def.destroyable) thing.Destroy(); else thing.DeSpawn(); }
                Map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
            }
            RegressionMod.Write(new { type = "start", version = VersionControl.CurrentVersionString, artifactHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CompDenseAnimal).Assembly.Location))).ToLowerInvariant(),
                mods = ModsConfig.ActiveModsInLoadOrder.Select(m => m.PackageId).ToArray(),
                expected = Environment.GetEnvironmentVariable("HDA_TEST_PHASE") == "benchmark" ? new[] { "benchmark" } : Cases });
            if (Environment.GetEnvironmentVariable("HDA_TEST_PHASE") == "benchmark") Case("benchmark", Benchmark);
            else RunCases();
            RegressionMod.Write(new { type = "complete" });
        }
        catch (Exception e) { RegressionMod.Write(new { type = "fatal", error = e.ToString() }); }
    }
    void RunCases()
    {
        Case("package", () => {
            Assert(typeof(CompDenseAnimal).Assembly.Location.Contains("1.7"), "Wrong version-specific mod artifact loaded");
            Assert(ThingDef.Named("HDA_AnimalDensifier").inspectorTabs.Contains(typeof(ITab_DensifierStorage)), "Core storage tab missing");
            Assert(ThingDef.Named("HDA_AnimalDensifier").researchPrerequisites.Contains(ResearchProjectDef.Named("Electricity")), "Research prerequisite missing");
            if (Environment.GetEnvironmentVariable("HDA_TEST_CONTENT") == "core")
                Assert(!ModsConfig.BiotechActive && !ModsConfig.IdeologyActive && !ModsConfig.RoyaltyActive && !ModsConfig.AnomalyActive && !ModsConfig.OdysseyActive, "Core-only profile enabled DLC");
            var machine = Machine(); Assert(machine.Graphic != null && machine.Graphic != BaseContent.BadGraphic, "Missing building graphic");
        });
        Case("egg_stacks", () => {
            Thing Make(int density) { var egg = ThingMaker.MakeThing(ThingDef.Named("EggChickenFertilized")); fixture.Add(egg); egg.TryGetComp<CompDenseEgg>().Configure(density, ThingDef.Named("Chicken")); return egg; }
            var normal = Make(1); var dense = Make(5); var dense2 = Make(5);
            Assert(normal.CanStackWith(Make(1)), "Normal eggs should stack");
            Assert(!normal.CanStackWith(dense) && !dense.CanStackWith(normal) && !dense.CanStackWith(dense2), "Dense stacking is asymmetric");
            Assert(!normal.TryAbsorbStack(dense, true) && !dense.TryAbsorbStack(normal, true), "Dense embryo merged");
            Assert(!dense.Destroyed && DenseEggUtility.GetEggDensity(dense) == 5, "Embryo density lost");
            dense.stackCount = 2; var split = dense.SplitOff(1); fixture.Add(split);
            Assert(DenseEggUtility.GetEggDensity(split) == 5 && DenseEggUtility.GetEggSourceRace(split) == ThingDef.Named("Chicken"), "Split erased metadata");
        });
        Case("admission", () => {
            var machine = Machine(); var female = Animal();
            Assert(!machine.AcceptsAnimal(Animal("Chicken")), "Wrong species admitted");
            Assert(!machine.AcceptsAnimal(Animal(age: .01f)), "Juvenile admitted");
            Assert(!machine.AcceptsAnimal(Animal(density: 2)), "At-target animal admitted");
            var pregnant = Animal(); pregnant.health.AddHediff(HediffDef.Named("Pregnant"));
            Assert(!machine.AcceptsAnimal(pregnant), "Pregnant animal admitted");
            var mental = Animal(); mental.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);
            Assert(!machine.AcceptsAnimal(mental), "Animal in mental state admitted");
            Assert(machine.TryInsertAnimal(female), "Eligible adult rejected");
            Assert(!machine.AcceptsAnimal(female), "Same pawn admitted twice");
            Assert(!machine.AcceptsAnimal(Animal(sex: Gender.Male)), "Opposite-sex animal admitted");
            Assert(machine.TryInsertAnimal(Animal()), "Compatible partner rejected");
            Assert(!machine.AcceptsAnimal(Animal()), "Third animal admitted");
        });
        Case("pair_selection", () => {
            var machine = Machine(); var worker = Worker(); var lone = Animal(age: 10f);
            var maleA = Animal(sex: Gender.Male); var maleB = Animal(sex: Gender.Male);
            Assert(machine.FindNextAnimal(worker) != lone, "Selected lone oldest animal instead of compatible pair");
            maleA.SetFaction(null); Assert(machine.FindNextAnimal(worker) == null, "Non-colony partner selected");
            maleA.SetFaction(Faction.OfPlayer); var otherWorker = Worker();
            Assert(otherWorker.Reserve(maleA, JobMaker.MakeJob(JobDefOf.Wait)), "Reserve candidate");
            Assert(machine.FindNextAnimal(worker) == null, "Reserved animal selected");
            worker.Map.reservationManager.ReleaseAllForTarget(maleA);
            Assert(machine.FindNextAnimal(worker) != null, "Eligible pair not found after reservation release");
        });
        Case("blocked_ejection", () => {
            var machine = Machine(); var pawn = Animal(); Assert(machine.TryInsertAnimal(pawn), "Insert animal");
            blockedPlacement = pawn;
            var method = typeof(GenPlace).GetMethods().Single(m => m.Name == "TryPlaceThing" && m.GetParameters().Length == 9 && m.GetParameters()[0].ParameterType == typeof(Thing));
            var harmony = new Harmony("local.hda.block-placement");
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(RegressionSuite), nameof(PlacementPrefix)));
            try {
                machine.EjectAll(); Assert(machine.GetDirectlyHeldThings().Contains(pawn) && pawn.ParentHolder == machine, "Failed placement abandoned animal");
                machine.Destroy(DestroyMode.Deconstruct); Assert(pawn.Spawned && !pawn.Destroyed, "Removal fallback lost animal");
            } finally { harmony.UnpatchAll("local.hda.block-placement"); }
        });
        Case("lifecycle", () => {
            foreach (var mode in new[] { DestroyMode.Deconstruct, DestroyMode.KillFinalize, DestroyMode.Vanish }) {
                var machine = Machine(); var a = Animal(); var b = Animal(); Load(machine, a, b); machine.TickRare();
                machine.Destroy(mode); Assert(a.Spawned && b.Spawned && !a.Destroyed && !b.Destroyed, "Removal lost contained animals: " + mode);
            }
            var unspawned = (Building_AnimalDensifier)ThingMaker.MakeThing(ThingDef.Named("HDA_AnimalDensifier")); fixture.Add(unspawned);
            Assert(unspawned.TrySetSpecies(ThingDef.Named("Cow")), "Configure off-map holder"); var orphan = Animal();
            Assert(unspawned.TryInsertAnimal(orphan), "Load off-map holder"); unspawned.Destroy();
            Assert(!orphan.Destroyed && Find.WorldPawns.AllPawnsAlive.Contains(orphan), "Off-map destruction discarded live pawn");
        });
        Case("fusion_and_pause", () => {
            var machine = Machine(); var a = Animal(); var b = Animal(); Load(machine, a, b);
            machine.TickRare(); Assert(machine.CycleTicksRemaining == 3000, "Incorrect initial cycle duration");
            Advance(500); Assert(machine.ActiveCycle, "Early fusion completion"); int remaining = machine.CycleTicksRemaining;
            Automation(machine).toggleAction(); Advance(4000); Assert(machine.CycleTicksRemaining == remaining, "Disabled time counted toward fusion");
            Automation(machine).toggleAction();
            DebugSettings.unlimitedPower = false; machine.GetComp<CompPowerTrader>().PowerOn = false;
            Advance(1000); Assert(machine.CycleTicksRemaining == remaining, "Unpowered time counted toward fusion");
            DebugSettings.unlimitedPower = true; machine.GetComp<CompPowerTrader>().PowerOn = true;
            Advance(remaining - 251); Assert(machine.ActiveCycle && !b.Destroyed, "Cycle completed early after resume");
            FinishByNormalTicks(machine); Assert(a.Spawned && DenseAnimalUtility.GetDensity(a) == 2 && b.Destroyed, "Incorrect fusion result");
            Near(machine.StoredNutrition, 4f, "Nutrition was double-charged");
        });
        Case("post_cycle_partner", () => {
            foreach (string scenario in new[] { "none", "compatible", "wrong_sex", "juvenile", "too_dense", "non_colony" }) {
                ResetFixture();
                var machine = Machine(4); var a = Animal(); var b = Animal(); Load(machine, a, b);
                if (scenario != "none") {
                    var partner = Animal(density: scenario == "too_dense" ? 3 : 1,
                        sex: scenario == "wrong_sex" ? Gender.Male : Gender.Female,
                        age: scenario == "juvenile" ? .1f : 5f);
                    if (scenario == "non_colony") partner.SetFaction(null);
                }
                machine.TickRare(); Assert(machine.ActiveCycle, "Cycle did not start: " + scenario);
                for (int i = 0; i < 12; i++) machine.TickRare();
                Assert(!machine.ActiveCycle && b.Destroyed && DenseAnimalUtility.GetDensity(a) == 2,
                    "Incorrect completed fusion: " + scenario);
                Near(machine.StoredNutrition, 4f, "Incorrect post-cycle nutrition: " + scenario);
                if (scenario == "compatible")
                    Assert(!a.Spawned && a.ParentHolder == machine, "Ejected animal with viable partner");
                else {
                    Assert(a.Spawned && !a.Destroyed && !machine.GetDirectlyHeldThings().Contains(a),
                        "Stranded animal without viable partner: " + scenario);
                    Assert(machine.FindNextAnimal(Worker()) == null, "Ejected animal selected without compatible pair: " + scenario);
                }
            }
        });
        Case("cap_and_cancel", () => {
            var machine = Machine(10); var a = Animal(density: 3); var b = Animal(density: 3); Load(machine, a, b, 160);
            machine.TickRare(); HighDensityAnimalsMod.Settings.maxDensity = 2; FinishByNormalTicks(machine);
            Assert(DenseAnimalUtility.GetDensity(a) == 6 && b.Destroyed, "Changed cap truncated accepted density");
            HighDensityAnimalsMod.Settings.maxDensity = 25;
            var other = Machine(); var c = Animal(); var d = Animal(); Load(other, c, d);
            other.TickRare(); int beforeFood = Map.listerThings.ThingsOfDef(ThingDefOf.RawPotatoes).Sum(t => t.stackCount);
            other.EjectAll(); int returnedCount = Map.listerThings.ThingsOfDef(ThingDefOf.RawPotatoes).Sum(t => t.stackCount) - beforeFood;
            Near(other.StoredNutrition + returnedCount * ThingDefOf.RawPotatoes.GetStatValueAbstract(StatDefOf.Nutrition), 6f, "Cancelled cycle did not conserve nutrition");
            float refunded = other.StoredNutrition; other.EjectAll(); Near(other.StoredNutrition, refunded, "Cancellation refunded twice");
            Assert(c.Spawned && d.Spawned, "Cancellation consumed an animal");
            ResetFixture(); var changed = Machine(4); var first = Animal(); var second = Animal(); Load(changed, first, second);
            changed.TickRare(); first.GetComp<CompDenseAnimal>().SetDensity(2); FinishByNormalTicks(changed);
            Assert(DenseAnimalUtility.GetDensity(first) == 2 && !second.Destroyed, "Changed inputs were consumed by stale cycle snapshot");
            Near(changed.StoredNutrition, 6f, "Invalidated cycle did not refund its charge");
        });
        Case("nutrition", () => {
            var machine = Machine(); var a = Animal(); var b = Animal(); Load(machine, a, b, 39); machine.TickRare();
            Assert(!machine.ActiveCycle, "Cycle started with insufficient nutrition");
            var extra = ThingMaker.MakeThing(ThingDefOf.RawPotatoes); extra.stackCount = 2; machine.GetDirectlyHeldThings().TryAdd(extra);
            machine.TickRare(); Assert(machine.ActiveCycle, "Exact sufficient food did not start cycle");
            Near(machine.StoredNutrition, .05f, "Incorrect fractional food remainder");
            var egg = ThingMaker.MakeThing(ThingDef.Named("EggChickenFertilized")); fixture.Add(egg); egg.TryGetComp<CompDenseEgg>().Configure(3, ThingDef.Named("Chicken"));
            Assert(!machine.CanAcceptNutrition(egg), "Dense egg accepted as feedstock");
            var food = Food(); machine.GetStoreSettings().filter.SetDisallowAll();
            Assert(!machine.CanAcceptNutrition(food), "Storage filter ignored");
            machine.AbsorbNutrition(food); Assert(!food.Destroyed, "Rejected food destroyed");
        });
        Case("production", () => {
            var worker = Worker(); var normal = Animal(); var dense = Animal(density: 4);
            Near(dense.HealthScale / normal.HealthScale, 2f, "Health scaling");
            Near(dense.GetStatValue(StatDefOf.MeatAmount) / normal.GetStatValue(StatDefOf.MeatAmount), 4f, "Meat stat scaling");
            Near(dense.GetStatValue(StatDefOf.MaxNutrition) / normal.GetStatValue(StatDefOf.MaxNutrition), 4f, "Nutrition capacity scaling");
            Near(dense.GetStatValue(StatDefOf.MarketValue) / normal.GetStatValue(StatDefOf.MarketValue), 4f, "Market value scaling");
            var milk = dense.GetComp<CompMilkable>(); Set(milk, "fullness", 1f); worker.skills.GetSkill(SkillDefOf.Animals).Level = 20;
            int before = Map.listerThings.ThingsOfDef(ThingDef.Named("Milk")).Sum(t => t.stackCount);
            milk.Gathered(worker);
            Assert(Map.listerThings.ThingsOfDef(ThingDef.Named("Milk")).Sum(t => t.stackCount) - before == 4 * dense.def.GetCompProperties<CompProperties_Milkable>().milkAmount, "Actual milk collection incorrect");
            var sheep = Animal("Alpaca", 3); var shear = sheep.GetComp<CompShearable>(); Set(shear, "fullness", 1f);
            var woolDef = sheep.def.GetCompProperties<CompProperties_Shearable>().woolDef;
            before = Map.listerThings.ThingsOfDef(woolDef).Sum(t => t.stackCount); shear.Gathered(worker);
            Assert(Map.listerThings.ThingsOfDef(woolDef).Sum(t => t.stackCount) - before == 3 * sheep.def.GetCompProperties<CompProperties_Shearable>().woolAmount, "Actual wool collection incorrect");
            var products = dense.ButcherProducts(worker, 1f).ToList(); foreach (var product in products) fixture.Add(product);
            Assert(products.Where(t => t.def == dense.RaceProps.meatDef).Sum(t => t.stackCount) == (int)dense.GetStatValue(StatDefOf.MeatAmount), "Butchered meat incorrect");
            Assert(products.Where(t => t.def == dense.RaceProps.leatherDef).Sum(t => t.stackCount) == (int)dense.GetStatValue(StatDefOf.LeatherAmount), "Butchered leather incorrect");
            normal.needs.food.CurLevel = normal.needs.food.MaxLevel; dense.needs.food.CurLevel = dense.needs.food.MaxLevel;
            float n0 = normal.needs.food.CurLevel, d0 = dense.needs.food.CurLevel;
            // Tick needs directly to exclude eating jobs while exercising the real food-need calculation.
            for (int i = 0; i < 10; i++) { normal.needs.food.NeedInterval(); dense.needs.food.NeedInterval(); }
            Near((d0 - dense.needs.food.CurLevel) / (n0 - normal.needs.food.CurLevel), 4f, "Actual nutrition depletion scaling", .02f);
        });
        Case("combat", () => {
            DebugSettings.enableDamage = true;
            var attacker = Animal(); var target = Animal();
            var verb = attacker.verbTracker.AllVerbs.OfType<Verb_MeleeAttackDamage>().First();
            float AttackAmount() {
                Rand.PushState(604);
                try { return ((IEnumerable<DamageInfo>)AccessTools.Method(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply").Invoke(verb, new object[] { new LocalTargetInfo(target) })).First().Amount; }
                finally { Rand.PopState(); }
            }
            float normalAttack = AttackAmount(); Assert(normalAttack > 0, "Melee fixture has zero damage"); attacker.GetComp<CompDenseAnimal>().SetDensity(4);
            Near(AttackAmount() / normalAttack, 1.5f, "Actual melee damage calculation did not scale once", .01f);
            HighDensityAnimalsMod.Settings.scaleCombatStats = false; DenseAnimalUtility.RefreshAllDenseAnimalCaches();
            Near(AttackAmount(), normalAttack, "Combat disable still changes actual damage");
            HighDensityAnimalsMod.Settings.scaleCombatStats = true;
            DebugSettings.enableDamage = true;
            float normalTotal = 0f, denseTotal = 0f;
            for (int seed = 0; seed < 64; seed++) {
                var normal = Animal(); var dense = Animal(density: 4);
                float Hit(Pawn victim) {
                    Rand.PushState(seed);
                    try { return victim.TakeDamage(new DamageInfo(DamageDefOf.Cut, 10f, 0f, hitPart: victim.RaceProps.body.corePart)).totalDamageDealt; }
                    finally { Rand.PopState(); }
                }
                normalTotal += Hit(normal); denseTotal += Hit(dense);
                normal.Destroy(); dense.Destroy();
            }
            Assert(denseTotal < normalTotal, "Dense armor did not reduce actual incoming damage across matched seeded hits");
        });
        Case("offspring", () => {
            var chicken = Animal("Chicken", 3); var layer = chicken.GetComp<CompEggLayer>();
            var egg = layer.ProduceEgg(); fixture.Add(egg); Assert(egg.stackCount == 3, "Unfertilized egg over/underproduction");
            layer.Fertilize(chicken); var fertile = layer.ProduceEgg(); fixture.Add(fertile);
            Assert(fertile.stackCount == 1 && DenseEggUtility.GetEggDensity(fertile) == 3, "Incorrect dense embryo");
            GenSpawn.Spawn(fertile, Map.Center + new IntVec3(8, 0, 8), Map);
            var before = Map.mapPawns.AllPawnsSpawned.ToHashSet(); fertile.TryGetComp<CompHatcher>().Hatch();
            var chicks = Map.mapPawns.AllPawnsSpawned.Where(p => !before.Contains(p)).ToList(); fixture.AddRange(chicks);
            Assert(chicks.Count == 1 && DenseAnimalUtility.GetDensity(chicks[0]) == 3, "Dense chick hatch incorrect");
            var mother = Animal(density: 3); var father = Animal(sex: Gender.Male); before = Map.mapPawns.AllPawnsSpawned.ToHashSet();
            Hediff_Pregnant.DoBirthSpawn(mother, father); var calves = Map.mapPawns.AllPawnsSpawned.Where(p => !before.Contains(p)).ToList(); fixture.AddRange(calves);
            Assert(calves.Count == 3 && calves.All(p => DenseAnimalUtility.GetDensity(p) == 1), "Mammal birth scaling incorrect");
        });
        Case("natural_offspring", () => {
            var chicken = Animal("Chicken", 3); var layer = chicken.GetComp<CompEggLayer>(); layer.Fertilize(chicken);
            var egg = layer.ProduceEgg(); fixture.Add(egg); GenSpawn.Spawn(egg, Map.Center + new IntVec3(8, 0, 8), Map);
            Set(egg.TryGetComp<CompHatcher>(), "gestateProgress", .9999f);
            var mother = Animal(density: 3); var pregnancy = mother.health.AddHediff(HediffDef.Named("Pregnant")); pregnancy.Severity = .9999f;
            var before = Map.mapPawns.AllPawnsSpawned.ToHashSet(); Advance(1250);
            var babies = Map.mapPawns.AllPawnsSpawned.Where(p => !before.Contains(p)).ToList(); fixture.AddRange(babies);
            Assert(babies.Count(p => p.def == ThingDef.Named("Chicken") && DenseAnimalUtility.GetDensity(p) == 3) == 1, "Naturally ticked incubation lost dense embryo");
            Assert(babies.Count(p => p.def == ThingDef.Named("Cow")) == 3, "Naturally ticked pregnancy did not scale birth");
            Assert(!mother.health.hediffSet.HasHediff(HediffDef.Named("Pregnant")), "Pregnancy did not finish");
        });
        Case("rendering", () => {
            PawnRenderNodeProperties Body(PawnRenderNodeProperties node) {
                if (node == null || node.tagDef == PawnRenderNodeTagDefOf.Body) return node;
                foreach (var child in node.children ?? new List<PawnRenderNodeProperties>()) { var found = Body(child); if (found != null) return found; }
                return null;
            }
            var body = Body(DefDatabase<PawnRenderTreeDef>.GetNamed("Animal").root);
            Assert(body?.subworkerClasses.Contains(typeof(PawnRenderSubWorker_DenseAnimalScale)) == true, "Animal renderer scale hook not registered");
            var pawn = Animal(density: 4); HighDensityAnimalsMod.Settings.maxVisualScale = 1.1f; DenseAnimalUtility.RefreshAllDenseAnimalCaches();
            var scale = new Vector3(2f, 3f, 4f); var renderer = new PawnRenderSubWorker_DenseAnimalScale();
            renderer.TransformScale(null, new PawnDrawParms { pawn = pawn }, ref scale);
            Near(scale.x, 2.2f, "Rendered width exceeds cap"); Near(scale.z, 4.4f, "Rendered length exceeds cap"); Near(scale.y, 3f, "Renderer changed depth axis");
            HighDensityAnimalsMod.Settings.scaleVisualSize = false; scale = new Vector3(2f, 3f, 4f);
            renderer.TransformScale(null, new PawnDrawParms { pawn = pawn }, ref scale);
            Near(scale.x, 2f, "Disabled visual scaling still transforms mesh"); Near(scale.z, 4f, "Disabled visual scaling still transforms mesh");
        });
        Case("settings", () => {
            var machine = Machine(10); var pawn = Animal(density: 4); machine.TryInsertAnimal(pawn);
            Assert(pawn.Suspended, "Contained pawn is not explicitly in stasis");
            HighDensityAnimalsMod.Settings.scaleCombatStats = false; HighDensityAnimalsMod.Settings.maxVisualScale = 1.1f;
            DenseAnimalUtility.RefreshAllDenseAnimalCaches();
            Near(pawn.GetComp<CompDenseAnimal>().CachedMeleeDamageFactor, 1f, "Contained combat settings stale");
            Near(pawn.GetComp<CompDenseAnimal>().CachedVisualScaleFactor, 1.1f, "Contained size settings stale");
            long age = pawn.ageTracker.AgeBiologicalTicks; float hunger = pawn.needs.food.CurLevel;
            Advance(1000); Assert(pawn.ageTracker.AgeBiologicalTicks == age, "Contained pawn aged while in stasis");
            Near(pawn.needs.food.CurLevel, hunger, "Contained pawn ate while in stasis");
            var worldPawn = Animal(density: 4); worldPawn.DeSpawn(); Find.WorldPawns.PassToWorld(worldPawn, PawnDiscardDecideMode.KeepForever);
            worldPawn.GetComp<CompDenseAnimal>().RecalculateCachedValues();
            HighDensityAnimalsMod.Settings.scaleCombatStats = true; DenseAnimalUtility.RefreshAllDenseAnimalCaches();
            Assert(worldPawn.GetComp<CompDenseAnimal>().CachedMeleeDamageFactor > 1f, "World pawn settings stale");
        });
        Case("localization_and_ui", () => {
            var machine = Machine(); var pawn = Animal(); var partner = Animal(); Load(machine, pawn, partner); machine.TickRare();
            var commands = machine.GetGizmos().OfType<Command_Action>().ToList();
            Assert((bool)AccessTools.Field(typeof(Gizmo), "disabled").GetValue(commands.Single(c => c.defaultLabel == "HDA_SetSpecies".Translate())), "Species command enabled while loaded");
            Assert((bool)AccessTools.Field(typeof(Gizmo), "disabled").GetValue(commands.Single(c => c.defaultLabel == "HDA_TargetDecrease".Translate())), "Target command enabled during cycle");
            Assert(machine.GetInspectString().Contains("HDA_Cycleprogress".Translate()), "Missing cycle inspection");
            var chicken = Animal("Chicken", 3);
            var entry = chicken.RaceProps.SpecialDisplayStats(chicken.def, StatRequest.For(chicken)).Single(e => e.LabelCap == "Stat_Animal_EggsPerYear".Translate().CapitalizeFirst());
            Assert(entry.ValueString == (AnimalProductionUtility.EggsPerYear(chicken.def) * 3).ToString("0.##"), "Productivity display incorrect");
        });
        Case("reachability", () => {
            var machine = Machine(); var worker = Worker(); var blocked = Animal(age: 10f); Animal(); Animal();
            blocked.Position = Map.Center + new IntVec3(12, 0, 12);
            foreach (IntVec3 cell in CellRect.CenteredOn(blocked.Position, 1).EdgeCells) {
                var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel); fixture.Add(wall); GenSpawn.Spawn(wall, cell, Map);
            }
            Map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
            Assert(!worker.CanReach(blocked, PathEndMode.ClosestTouch, Danger.Some), "Enclosure fixture is reachable");
            Assert(machine.FindNextAnimal(worker) is Pawn chosen && chosen != blocked, "Unreachable oldest animal selected");
            machine.SetForbidden(true); Assert(!new WorkGiver_LoadAnimalToDensifier().HasJobOnThing(worker, machine), "Forbidden machine accepted work");
            machine.SetForbidden(false); var food = Food(); food.SetForbidden(true);
            Assert(!new WorkGiver_HaulNutritionToAnimalDensifier().HasJobOnThing(worker, machine), "Forbidden food selected");
        });
        Case("negative_controls", () => {
            var pawn = Animal(density: 4); float scaledValue = pawn.GetStatValue(StatDefOf.MarketValue);
            HighDensityAnimalsMod.Settings.scaleMarketValue = false; HighDensityAnimalsMod.Settings.scaleHealth = false;
            HighDensityAnimalsMod.Settings.scaleCombatStats = false; HighDensityAnimalsMod.Settings.scaleVisualSize = false;
            DenseAnimalUtility.RefreshAllDenseAnimalCaches();
            Near(pawn.GetStatValue(StatDefOf.MarketValue) * 4f, scaledValue, "Market-value toggle/cache incorrect");
            Near(pawn.GetComp<CompDenseAnimal>().CachedMeleeDamageFactor, 1f, "Combat disable incorrect");
            Near(pawn.GetComp<CompDenseAnimal>().CachedArmorSharpOffset, 0f, "Armor disable incorrect");
            var normal = Animal(); Near(pawn.HealthScale, normal.HealthScale, "Health disable incorrect");
            var egg = ThingMaker.MakeThing(ThingDef.Named("EggChickenFertilized")); var baseEgg = ThingMaker.MakeThing(egg.def); fixture.Add(egg); fixture.Add(baseEgg);
            egg.TryGetComp<CompDenseEgg>().Configure(5, ThingDef.Named("Chicken"));
            HighDensityAnimalsMod.Settings.scaleMarketValue = true;
            Near(egg.GetStatValue(StatDefOf.MarketValue) / baseEgg.GetStatValue(StatDefOf.MarketValue), 5f, "Dense egg market value incorrectly scaled");
            var machine = Machine(); var a = Animal(); var b = Animal(); Load(machine, a, b);
            Assert(!machine.TrySetTargetDensity(101) && !machine.TrySetSpecies(ThingDef.Named("Chicken")), "Invalid configuration change accepted");
            machine.TickRare(); Assert(!machine.TrySetTargetDensity(3), "Active target change accepted");
        });
        Case("autonomous_cycles", () => {
            var machine = Machine(3); var a = Animal(); var b = Animal(); var c = Animal(); Food(240); var worker = Worker();
            RegressionMod.Eaten.Clear(); RegressionMod.ObserveIngestion = true;
            worker.jobs.EndCurrentJob(JobCondition.InterruptForced);
            for (int i = 0; i < 18000 && !new[] { a, b, c }.Any(p => p.Spawned && DenseAnimalUtility.GetDensity(p) == 3); i++) Find.TickManager.DoSingleTick();
            var survivor = new[] { a, b, c }.SingleOrDefault(p => p.Spawned && DenseAnimalUtility.GetDensity(p) == 3);
            Assert(survivor != null && new[] { a, b, c }.Count(p => p.Destroyed) == 2, "Normal Hauling work did not complete two fusion cycles: job=" + worker.CurJobDef?.defName +
                "; downed=" + worker.Downed + "; mental=" + worker.InMentalState + "; machine=" + machine.GetInspectString());
            Near(machine.StoredNutrition, 0f, "Unexpected nutrition remaining in machine");
            var remainingFood = new List<Thing>();
            ThingOwnerUtility.GetAllThingsRecursively(Map, ThingRequest.ForDef(ThingDefOf.RawPotatoes), remainingFood);
            float leftNutrition = remainingFood.Distinct().Sum(t => t.stackCount * t.GetStatValue(StatDefOf.Nutrition));
            float eatenNutrition = RegressionMod.Eaten.Values.Sum();
            Near(leftNutrition + eatenNutrition + 5f + machine.StoredNutrition, 12f,
                "Autonomous cycles did not conserve delivered, consumed, carried and eaten food");
        });
        Case("interrupted_haul", () => {
            var machine = Machine(); var a = Animal(); Animal(); var worker = Worker();
            var giver = new WorkGiver_LoadAnimalToDensifier(); var job = giver.JobOnThing(worker, machine);
            Assert(job != null, "Workgiver missing loading job"); worker.jobs.StartJob(job, JobCondition.InterruptForced);
            for (int i = 0; i < 2000 && worker.carryTracker.CarriedThing == null; i++) Find.TickManager.DoSingleTick();
            Assert(worker.carryTracker.CarriedThing is Pawn, "Never picked up animal");
            machine.Destroy(DestroyMode.Deconstruct); Advance(20);
            Assert(new[] { a }.All(p => !p.Destroyed && (p.Spawned || p.ParentHolder is Pawn_CarryTracker)), "Interruption lost animal");
        });
        Case("save_fixture", () => {
            var machine = Machine(17); var a = Animal(density: 3); var b = Animal(); Load(machine, a, b);
            machine.GetStoreSettings().filter.SetAllow(ThingDefOf.RawPotatoes, false);
            machine.TickRare(); Advance(500); Automation(machine).toggleAction();
            Assert(machine.ActiveCycle && machine.CycleTicksRemaining > 0, "Save fixture has no active cycle");
            var egg = ThingMaker.MakeThing(ThingDef.Named("EggChickenFertilized")); egg.TryGetComp<CompDenseEgg>().Configure(5, ThingDef.Named("Chicken"));
            GenSpawn.Spawn(egg, Map.Center + new IntVec3(8, 0, 8), Map);
            var variants = new List<object>();
            object Snapshot(string state, Building_AnimalDensifier holder) => new {
                state, machine = holder.thingIDNumber, density = holder.CurrentDensity, nutrition = holder.StoredNutrition,
                remaining = holder.CycleTicksRemaining, automation = holder.AutomationEnabled,
                pawns = holder.GetDirectlyHeldThings().OfType<Pawn>().Select(p => p.thingIDNumber).ToArray()
            };
            var empty = Machine(offset: new IntVec3(-16, 0, 0)); variants.Add(Snapshot("empty", empty));
            var single = Machine(4, offset: new IntVec3(-8, 0, 0)); single.TryInsertAnimal(Animal()); variants.Add(Snapshot("one_animal", single));
            var waiting = Machine(4, offset: new IntVec3(8, 0, 0)); Load(waiting, Animal(), Animal(), 0); variants.Add(Snapshot("waiting_for_food", waiting));
            var foodOnly = Machine(offset: new IntVec3(16, 0, 0)); var raw = ThingMaker.MakeThing(ThingDefOf.RawPotatoes); raw.stackCount = 7;
            foodOnly.GetDirectlyHeldThings().TryAdd(raw); variants.Add(Snapshot("food_only", foodOnly));
            var powered = Machine(5, offset: new IntVec3(0, 0, 8)); Load(powered, Animal(), Animal()); powered.TickRare(); variants.Add(Snapshot("powered_cycle", powered));
            RegressionMod.Write(new { type = "snapshot", machine = machine.thingIDNumber, primary = a.thingIDNumber, secondary = b.thingIDNumber,
                remaining = machine.CycleTicksRemaining, nutrition = machine.StoredNutrition, egg = egg.thingIDNumber, variants });
            GameDataSaveLoader.SaveGame("HdaRegression");
        });
    }
    void Benchmark()
    {
        foreach (var scenario in new[] { (count: 120, density: 1, machines: 0), (count: 30, density: 4, machines: 0), (count: 30, density: 4, machines: 100) })
        {
            ResetFixture();
            foreach (IntVec3 cell in CellRect.CenteredOn(Map.Center, 25).EdgeCells) {
                var fence = ThingMaker.MakeThing(ThingDef.Named("Fence"), ThingDefOf.WoodLog);
                fixture.Add(fence); GenSpawn.Spawn(fence, cell, Map);
            }
            var marker = ThingMaker.MakeThing(ThingDef.Named("PenMarker"), ThingDefOf.WoodLog); marker.SetFaction(Faction.OfPlayer);
            fixture.Add(marker); GenSpawn.Spawn(marker, Map.Center + new IntVec3(22, 0, 22), Map);
            Map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
            for (int i = 0; i < scenario.count; i++) Animal(density: scenario.density);
            for (int i = 0; i < scenario.machines; i++) {
                var machine = (Building_AnimalDensifier)ThingMaker.MakeThing(ThingDef.Named("HDA_AnimalDensifier"));
                machine.SetFaction(Faction.OfPlayer); fixture.Add(machine);
                GenSpawn.Spawn(machine, Map.Center + new IntVec3((i % 10 - 5) * 4, 0, (i / 10 - 5) * 4), Map);
            }
            Advance(2000);
            var milliseconds = new List<double>(); var allocations = new List<long>();
            for (int sample = 0; sample < 6; sample++) {
                long before = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
                Advance(1000); watch.Stop(); milliseconds.Add(watch.Elapsed.TotalMilliseconds);
                allocations.Add(GC.GetAllocatedBytesForCurrentThread() - before);
            }
            int actual = Map.mapPawns.AllPawnsSpawned.Count(p => p.IsColonyAnimal);
            Assert(actual == scenario.count, "Benchmark population changed: expected " + scenario.count + ", got " + actual);
            RegressionMod.Write(new { type = "benchmark", pawns = scenario.count, density = scenario.density,
                machines = scenario.machines, ticksPerSample = 1000, milliseconds, allocations });
        }
    }

    void Resume()
    {
        RegressionMod.Write(new { type = "start", expected = new[] { Environment.GetEnvironmentVariable("HDA_TEST_PHASE") == "legacy" ? "legacy_save_migration" : "fresh_process_reload" }, version = VersionControl.CurrentVersionString, artifactHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CompDenseAnimal).Assembly.Location))).ToLowerInvariant() });
        Case(Environment.GetEnvironmentVariable("HDA_TEST_PHASE") == "legacy" ? "legacy_save_migration" : "fresh_process_reload", () => {
            var snapshot = JsonDocument.Parse(File.ReadAllLines(Environment.GetEnvironmentVariable("HDA_TEST_PREPARE_REPORT"))
                .Single(s => JsonDocument.Parse(s).RootElement.GetProperty("type").GetString() == "snapshot")).RootElement;
            var machine = Map.listerThings.AllThings.OfType<Building_AnimalDensifier>().Single(b => b.thingIDNumber == snapshot.GetProperty("machine").GetInt32());
            foreach (var variant in snapshot.GetProperty("variants").EnumerateArray()) {
                var holder = Map.listerThings.AllThings.OfType<Building_AnimalDensifier>().Single(b => b.thingIDNumber == variant.GetProperty("machine").GetInt32());
                Assert(holder.CurrentDensity == variant.GetProperty("density").GetInt32() && holder.CycleTicksRemaining == variant.GetProperty("remaining").GetInt32(), "Variant density/timer not restored: " + variant.GetProperty("state"));
                Assert(holder.AutomationEnabled == variant.GetProperty("automation").GetBoolean(), "Variant automation not restored");
                Near(holder.StoredNutrition, variant.GetProperty("nutrition").GetSingle(), "Variant food/liquid nutrition not restored");
                Assert(holder.GetDirectlyHeldThings().OfType<Pawn>().Select(p => p.thingIDNumber).SequenceEqual(variant.GetProperty("pawns").EnumerateArray().Select(e => e.GetInt32())), "Variant pawn identities not restored");
            }
            Assert(machine.SelectedSpecies == ThingDef.Named("Cow") && machine.TargetDensity == 17 && !machine.AutomationEnabled, "Configuration not restored");
            Assert(!machine.GetStoreSettings().filter.Allows(ThingDefOf.RawPotatoes), "Storage filter not restored");
            var animals = machine.GetDirectlyHeldThings().OfType<Pawn>().ToList();
            Assert(animals.Count == 2 && animals[0].thingIDNumber == snapshot.GetProperty("primary").GetInt32() && animals[1].thingIDNumber == snapshot.GetProperty("secondary").GetInt32(), "Pawn identities not restored");
            Assert(machine.CurrentDensity == 3 && machine.CycleTicksRemaining == snapshot.GetProperty("remaining").GetInt32(), "Density/timer not restored");
            Near(machine.StoredNutrition, snapshot.GetProperty("nutrition").GetSingle(), "Nutrition not restored");
            var egg = Map.listerThings.AllThings.Single(t => t.thingIDNumber == snapshot.GetProperty("egg").GetInt32());
            Assert(DenseEggUtility.GetEggDensity(egg) == 5 && DenseEggUtility.GetEggSourceRace(egg) == ThingDef.Named("Chicken"), "Egg metadata not restored");
            int remaining = machine.CycleTicksRemaining; Advance(1000); Assert(machine.CycleTicksRemaining == remaining, "Paused save advanced after reload");
            Automation(machine).toggleAction(); machine.GetComp<CompPowerTrader>().PowerOn = true;
            Advance(remaining - 251); Assert(machine.ActiveCycle, "Restored cycle completed early");
            FinishByNormalTicks(machine);
            Assert(DenseAnimalUtility.GetDensity(animals[0]) == 4 && !animals[0].Destroyed && animals[1].Destroyed &&
                (animals[0].Spawned || animals[0].ParentHolder == machine), "Restored cycle result incorrect");
            Near(machine.StoredNutrition, snapshot.GetProperty("nutrition").GetSingle(), "Restored cycle charged twice");
        }, false);
        RegressionMod.Write(new { type = "complete" });
    }
}
