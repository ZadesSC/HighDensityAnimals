# Runtime audit, 2026-10-14

Tested the repository's **unchanged** `1.6/Assemblies/HighDensityAnimals.dll` inside the installed RimWorld **1.7.4977** process, with Harmony 3.0.0.0-rc.2 and the installed official expansions. No production source changes or 1.7 rebuild were needed for these tests.

DLL SHA-256: `e422b454368de10a98d6905d129eff9f7f6eda0a73b1a8c4fee16bac287fa692`.

An isolated copy under `/private/tmp/hda-audit/runtime/mod` contains About, Defs, Textures, and the DLL in root Assemblies. Only this copy's supported-version declaration was changed from 1.6 to 1.7. The user's saves and normal mod configuration were not changed. Temporary symlinks registered the copy and the harness under the game's Mods directory.

The harness is a separate .NET 10 mod loaded after High Density Animals. `RuntimeAudit.cs` preserves the executed test code. The checked-in project uses configurable reference paths rather than the test machine's paths:

```sh
dotnet build audit/RuntimeAudit.csproj -c Release \
  -p:GameManagedPath="/path/to/RimWorld.app/Contents/MacOS" \
  -p:HarmonyAssembly="/path/to/Harmony/1.7/Assemblies/0Harmony.dll"
```

For a rerun, put the resulting harness DLL in a separate mod with package ID `local.hdaaudit` and supported version 1.7. Prepare the isolated mod copy described above and create `/private/tmp/hda-audit/runtime` (the harness's report location). Launch from the game installation directory:

```sh
./RimWorld.app/Contents/MacOS/RimWorld \
  -savedatafolder=/private/tmp/hda-audit/runtime/profile \
  -mod=brrainz.harmony,ludeon.rimworld,zades.highdensityanimals,local.hdaaudit \
  -quicktest
```

Use a dedicated profile with `runInBackground=True`, `pauseOnError=False`, and developer mode enabled. The harness creates a quicktest colony, clears a small arena, generates adult female animals, and pauses normal time. It reports to `results.txt` in the isolated runtime directory. On completion it leaves the game paused. Never load the harness into a real colony: it deliberately alters the test map and exercises destructive lifecycle cases.

Most checks invoke actual game/mod methods directly. Fusion edge cases set private configuration fields and move the completion deadline or game tick counter to avoid waiting 3,000 ticks for each case. These validate state transitions, not processing duration or frame pacing. Animal loading and nutrition delivery run actual jobs for 2,000 game ticks each. Save/load writes and reloads an isolated `HdaAudit` save. Power tests directly change the power trader's state. Random incidents, diseases, mental states, and damage are disabled; unlimited power supports the other fixtures. The hauling fixture drafts the original colonists, disables other machines, despawns animals from earlier cases, and uses a fresh worker to avoid interference. It records whether the animal ever enters the target container, rather than assuming it remains there after subsequent autonomous work. Earlier exploratory runs included a downed worker and are not counted as evidence of a hauling bug.

`FAIL` denotes an unmet expected invariant, not a harness crash. Read labels separately: several failed assertions can represent one underlying bug. This is a focused smoke/regression audit, not a comprehensive compatibility certification. It does not verify all DLC combinations, other mods, performance at large population sizes, manual UI interaction, or visual rendering correctness. Nutrition checks cover capacity and the hunger-stage factor, not measured food consumption over a long simulation.

The pre-existing shell test passed; all five mod XML files parsed. A direct build of the production .NET Framework 4.8 project failed with MSB3644 because its reference assemblies are unavailable locally. Its hard-coded Windows/WSL game references also need configuration. The independently compiled runtime harness had zero compiler warnings/errors.

Final controlled run: 33 passing assertions, 6 failed assertions covering four distinct bugs, no harness failures, and no engine ERROR/Exception log lines. Temporary game Mods symlinks were removed after testing. Full scratch logs and the isolated save remain under `/private/tmp/hda-audit/runtime`.
