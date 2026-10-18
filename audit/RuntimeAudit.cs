using System.Reflection;
using HarmonyLib;
using HighDensityAnimals;
using RimWorld;
using Verse;
using Verse.AI;

public class HdaAudit : GameComponent
{
    const string Report = "/private/tmp/hda-audit/runtime/results.txt";
    int frames, phase, ticks;
    bool done, everLoaded;
    static bool reloading;
    Building_AnimalDensifier haulBuilding, saveBuilding;
    Pawn worker, hauled;
    public HdaAudit(Game game) { }
    static void Say(string s) { File.AppendAllText(Report,s+"\n"); Log.Message("[HDA-AUDIT] "+s); }
    static void Check(bool ok,string label) => Say((ok?"PASS ":"FAIL ")+label);
    static void Set(object o,string name,object value) => AccessTools.Field(o.GetType(),name).SetValue(o,value);
    static object Call(object o,string name,params object[] args) => AccessTools.Method(o.GetType(),name).Invoke(o,args);
    static void Case(string name,Action action) { try { Say("CASE "+name); action(); } catch(Exception e) { Say("ERROR "+name+": "+e); } }
    Pawn Animal(string kind="Cow", int density=1)
    {
        var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDef.Named(kind),Faction.OfPlayer, fixedBiologicalAge:5f, fixedChronologicalAge:5f, fixedGender:Gender.Female));
        GenSpawn.Spawn(p,Find.CurrentMap.Center+new IntVec3(3,0,3),Find.CurrentMap);
        p.GetComp<CompDenseAnimal>()?.SetDensity(density);
        return p;
    }
    Building_AnimalDensifier Building(int x,int target=2)
    {
        var b=(Building_AnimalDensifier)ThingMaker.MakeThing(ThingDef.Named("HDA_AnimalDensifier"));
        b.SetFaction(Faction.OfPlayer);
        GenSpawn.Spawn(b,Find.CurrentMap.Center+new IntVec3(x,0,0),Find.CurrentMap);
        b.GetComp<CompPowerTrader>().PowerOn=true;
        Set(b,"selectedSpecies",ThingDef.Named("Cow")); Set(b,"targetDensity",target);
        return b;
    }
    void Load(Building_AnimalDensifier b,Pawn a,Pawn c)
    {
        Check(b.TryInsertAnimal(a)&&b.TryInsertAnimal(c),"insert two eligible animals");
        Set(b,"storedNutrition",30f);
    }
    public override void GameComponentUpdate()
    {
        if(done || Current.ProgramState!=ProgramState.Playing || Find.CurrentMap==null || LongEventHandler.ShouldWaitForEvent || ++frames<60)return;
        Find.TickManager.Pause();
        try
        {
            var map=Find.CurrentMap;
            if(reloading)
            {
                reloading=false;
                Case("save/load",()=>{
                    var b=map.listerThings.AllThings.OfType<Building_AnimalDensifier>().Single(x=>x.TargetDensity==17);
                    Check(b.GetDirectlyHeldThings().OfType<Pawn>().Count()==2,"two contained animals restored");
                    Check(b.CurrentDensity==3,"contained animal density restored");
                    Check(b.GetDirectlyHeldThings().OfType<Pawn>().First().GetStatValue(StatDefOf.MaxNutrition)>1.5f,"contained animal nutrition scaling restored");
                    Check(b.GetInspectString().Contains("Cycle progress"),"active cycle restored");
                    Set(b,"cycleEndTick",Find.TickManager.TicksGame); b.GetComp<CompPowerTrader>().PowerOn=true; b.TickRare();
                    Check(b.CurrentDensity==4,"restored cycle completes");
                });
                Say("COMPLETE");done=true;return;
            }
            if(phase==0)
            {
                File.WriteAllText(Report,"RimWorld "+VersionControl.CurrentVersionString+"\nAssembly "+typeof(CompDenseAnimal).Assembly.Location+"\n");
                DebugSettings.enableDamage=false;DebugSettings.enableStoryteller=false; DebugSettings.enableRandomMentalStates=false; DebugSettings.enableRandomDiseases=false;
                DebugSettings.unlimitedPower=true;
                foreach(var c in CellRect.CenteredOn(map.Center,25).Cells)
                {
                    foreach(var t in c.GetThingList(map).ToList())if(t is Building || t is Plant){ if(t.def.destroyable)t.Destroy();else t.DeSpawn(); }
                    map.terrainGrid.SetTerrain(c,TerrainDefOf.Concrete);
                }
                Case("startup",()=>{
                    Check(ThingDef.Named("Cow").comps.Any(c=>c is CompProperties_DenseAnimal),"animal comps injected");
                    Check(Harmony.GetPatchInfo(AccessTools.PropertyGetter(typeof(Pawn),"HealthScale"))?.Owners.Contains("zades.highdensityanimals")==true,"health Harmony patch registered");
                    Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(Hediff_Pregnant),"DoBirthSpawn"))?.Owners.Contains("zades.highdensityanimals")==true,"birth Harmony patch registered");
                });
                Case("basic fusion",()=>{
                    var b=Building(-20);var a=Animal();var c=Animal();Load(b,a,c);
                    b.TickRare();Check(b.StoredNutrition==28f,"fusion consumes two nutrition");
                    Set(b,"cycleEndTick",Find.TickManager.TicksGame);b.TickRare();
                    Check(a.Spawned&&a.GetComp<CompDenseAnimal>().Density==2&&c.Destroyed,"fusion ejects density-two survivor and consumes secondary");
                    Check(a.GetStatValue(StatDefOf.MaxNutrition)>1f,"density scales nutrition capacity");
                    Check(b.GetGizmos().Any()&&b.GetInspectString().Length>0,"gizmos and inspection work");
                });
                Case("power interruption",()=>{
                    var b=Building(-16);var a=Animal();var c=Animal();Load(b,a,c);b.TickRare();
                    var end=(int)AccessTools.Field(b.GetType(),"cycleEndTick").GetValue(b);
                    b.GetComp<CompPowerTrader>().PowerOn=false; Find.TickManager.DebugSetTicksGame(end+1);b.TickRare();
                    Check(!a.Spawned,"unpowered machine holds animals");
                    b.GetComp<CompPowerTrader>().PowerOn=true;b.TickRare();
                    Check(!a.Spawned,"power interruption preserves unfinished processing time");
                });
                Case("density cap lowered",()=>{
                    var b=Building(-12,10);var a=Animal(density:3);var c=Animal(density:3);Load(b,a,c);b.TickRare();
                    HighDensityAnimalsMod.Settings.maxDensity=2;Set(b,"cycleEndTick",Find.TickManager.TicksGame);b.TickRare();
                    Check(DenseAnimalUtility.GetDensity(a)>=3,"lower cap never reduces existing survivor density");
                    Check(!c.Destroyed||DenseAnimalUtility.GetDensity(a)==6,"fusion conserves density when cap changes");
                    HighDensityAnimalsMod.Settings.maxDensity=25;
                });
                Case("deconstruction",()=>{
                    var b=Building(-8);var a=Animal();Check(b.TryInsertAnimal(a),"load animal before deconstruction");
                    b.Destroy(DestroyMode.Deconstruct);Check(a.Spawned&&!a.Destroyed,"deconstruction safely ejects live animal");
                    Say("DETAIL animal spawned="+a.Spawned+" destroyed="+a.Destroyed+" holder="+a.ParentHolder?.GetType().Name);
                });
                Case("egg stacking",()=>{
                    var dense=ThingMaker.MakeThing(ThingDef.Named("EggChickenFertilized"));var normal=ThingMaker.MakeThing(dense.def);
                    dense.TryGetComp<CompDenseEgg>().Configure(5,ThingDef.Named("Chicken"));
                    Check(!dense.CanStackWith(normal),"dense egg refuses normal stack");
                    Check(!normal.CanStackWith(dense),"normal egg refuses dense stack");
                    bool merged=normal.TryAbsorbStack(dense,true);
                    Check(!merged,"normal egg stack cannot absorb dense embryo");
                    Say("DETAIL merge="+merged+" normalStack="+normal.stackCount+" normalDensity="+DenseEggUtility.GetEggDensity(normal)+" denseDestroyed="+dense.Destroyed);
                });
                Case("egg laying",()=>{
                    var p=Animal("Chicken",3);var layer=p.GetComp<CompEggLayer>();
                    var egg=layer.ProduceEgg();Check(egg!=null&&egg.stackCount>=3,"unfertilized egg output scales");
                    layer.Fertilize(p);var fertilized=layer.ProduceEgg();
                    Check(fertilized!=null&&fertilized.stackCount==1&&DenseEggUtility.GetEggDensity(fertilized)>=3,"fertilized egg stores dense embryo");
                });
                Case("dense hatching",()=>{
                    var egg=ThingMaker.MakeThing(ThingDef.Named("EggChickenFertilized"));
                    egg.TryGetComp<CompDenseEgg>().Configure(5,ThingDef.Named("Chicken"));
                    GenSpawn.Spawn(egg,map.Center+new IntVec3(8,0,8),map);
                    var before=map.mapPawns.AllPawnsSpawned.ToHashSet();
                    egg.TryGetComp<CompHatcher>().Hatch();
                    var babies=map.mapPawns.AllPawnsSpawned.Where(p=>!before.Contains(p)&&p.def==ThingDef.Named("Chicken")).ToList();
                    Check(babies.Count==1&&DenseAnimalUtility.GetDensity(babies[0])==5,"one density-five chick hatches");
                });
                Case("dense mammal birth",()=>{
                    var mother=Animal(density:3);var father=Animal();father.gender=Gender.Male;
                    var before=map.mapPawns.AllPawnsSpawned.ToHashSet();
                    Hediff_Pregnant.DoBirthSpawn(mother,father);
                    var babies=map.mapPawns.AllPawnsSpawned.Where(p=>!before.Contains(p)&&p.def==ThingDef.Named("Cow")).ToList();
                    Check(babies.Count==3,"density-three cow gives birth to three calves");
                });
                Case("production and combat",()=>{
                    var baseCow=Animal();var denseCow=Animal(density:4);
                    Check((int)AccessTools.Property(typeof(CompMilkable),"ResourceAmount").GetValue(denseCow.GetComp<CompMilkable>())==4*(int)AccessTools.Property(typeof(CompMilkable),"ResourceAmount").GetValue(baseCow.GetComp<CompMilkable>()),"milk amount scales fourfold");
                    Check(Math.Abs(denseCow.GetStatValue(StatDefOf.MeatAmount)/baseCow.GetStatValue(StatDefOf.MeatAmount)-4)<0.01f,"meat yield stat scales fourfold");
                    Check(Math.Abs(denseCow.GetStatValue(StatDefOf.LeatherAmount)/baseCow.GetStatValue(StatDefOf.LeatherAmount)-4)<0.01f,"leather yield stat scales fourfold");
                    Check(Math.Abs(denseCow.HealthScale/baseCow.HealthScale-2)<0.01f,"health scales by square root of density");
                    Check(denseCow.GetStatValue(StatDefOf.MeleeDamageFactor)>baseCow.GetStatValue(StatDefOf.MeleeDamageFactor),"melee damage increases");
                    Check(denseCow.health.hediffSet.GetFirstHediffOfDef(HighDensityAnimalsDefOf.HDA_DenseAnimalMetabolism).CurStage.hungerRateFactor==4f,"hunger stage scales fourfold");
                });
                Case("real animal hauling job",()=>{
                    foreach(var machine in map.listerThings.AllThings.OfType<Building_AnimalDensifier>())Set(machine,"automationEnabled",false);
                    foreach(var colonist in map.mapPawns.FreeColonistsSpawned.ToList()) { colonist.jobs.EndCurrentJob(JobCondition.InterruptForced); colonist.drafter.Drafted=true; }
                    foreach(var animal in map.mapPawns.AllPawnsSpawned.Where(p=>p.RaceProps.Animal).ToList())animal.DeSpawn();
                    haulBuilding=Building(0);hauled=Animal();worker=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);GenSpawn.Spawn(worker,map.Center+new IntVec3(5,0,5),map);
                    worker.drafter.Drafted=false;worker.needs.food.CurLevel=worker.needs.food.MaxLevel;worker.needs.rest.CurLevel=1f;
                    worker.Position=map.Center+new IntVec3(5,0,5);
                    var job=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("HDA_LoadAnimalToDensifier"),hauled,haulBuilding);job.targetC=haulBuilding.InteractionCell;job.count=1;
                    worker.jobs.StartJob(job,JobCondition.InterruptForced);
                });
                phase=1;ticks=0;return;
            }
            if(phase==1)
            {
                for(int i=0;i<100;i++) { Find.TickManager.DoSingleTick(); if(haulBuilding.GetDirectlyHeldThings().Contains(hauled))everLoaded=true; } ticks+=100;if(ticks<2000)return;
                Check(everLoaded,"real job carries animal into densifier");
                Say("DETAIL hauling job="+worker.CurJobDef?.defName+" animalSpawned="+hauled.Spawned+" holder="+hauled.ParentHolder?.GetType().Name);
                Case("nutrition hauling job",()=>{
                    var food=ThingMaker.MakeThing(ThingDefOf.RawPotatoes);food.stackCount=75;GenSpawn.Spawn(food,map.Center+new IntVec3(4,0,4),map);
                    var giver=new WorkGiver_HaulNutritionToAnimalDensifier();
                    Check(giver.HasJobOnThing(worker,haulBuilding),"nutrition workgiver finds job");
                    var job=giver.JobOnThing(worker,haulBuilding);Check(job!=null&&job.count>0,"nutrition job has nonzero count");
                    if(job!=null)worker.jobs.StartJob(job,JobCondition.InterruptForced);
                });
                phase=2;ticks=0;return;
            }
            if(phase==2)
            {
                for(int i=0;i<100;i++)Find.TickManager.DoSingleTick();ticks+=100;if(ticks<2000)return;
                Check(haulBuilding.StoredNutrition>=2f,"real job delivers nutrition to container");
                Case("save active cycle",()=>{
                    saveBuilding=Building(12,17);Load(saveBuilding,Animal(density:3),Animal());saveBuilding.TickRare();
                    GameDataSaveLoader.SaveGame("HdaAudit");reloading=true;GameDataSaveLoader.LoadGame("HdaAudit");
                });
                phase=3;
            }
        }
        catch(Exception e){Say("FATAL "+e);done=true;}
    }
}
