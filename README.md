# High Density Animals

A RimWorld 1.7 mod that fuses compatible colony animals into denser livestock. Requires Harmony. No official expansion is required.

## Play

1. Install Harmony and this mod; enable Harmony before High Density Animals.
2. Research Electricity and build an **animal densifier** under Production.
3. Connect power, choose a species present in your colony, set a target density, and enable Hauling work for a colonist.
4. Use the Storage tab to choose feedstock. The machine loads a compatible pair and nutrition automatically, then retains the survivor for further cycles until the target is reached or no eligible partner is available. It then ejects the survivor.

Fusion always requires animals of the same species and sex. Animals must also be healthy enough to walk, not pregnant or in a mental state, and adults by default. The oldest animal in a feasible pair becomes the primary survivor: its identity, injuries, bonds, training, and age are retained. The secondary animal is permanently consumed. Avoid fusing valued bonded animals unless you intend to lose the secondary's identity. Animals and feedstock inside the machine are in stasis.

Each machine defaults to **Densify: Both**. Click this gizmo to open a dropdown with **Both**, **Females only**, and **Males only**. Both allows either sex and animals without a sex; each pair must still have matching sex. The selection is saved per machine and can change while empty or holding only food. Eject loaded animals before changing it. Existing machines default to Both, and the old same-sex mod option is removed. Mixed-sex cycles from old saves cancel and refund their nutrition while retaining both animals for ejection.

Each machine has a **Prevent density waste** gizmo, enabled by default. With it enabled, pairs that exceed the machine's target or global maximum are blocked. Disable it to allow overflowing pairs: the survivor is capped at the lower limit and excess density is lost. For example, density 6 + 6 at a limit of 10 produces density 10. Nutrition still costs the full input sum (12 in this example). The result is fixed when the cycle starts; the gizmo is disabled during processing, and later limit changes apply to future cycles. Animals already at the target remain ineligible for loading.

Each cycle takes **3,000 powered ticks** (about 50 seconds at normal speed), with up to one rare-tick interval (250 ticks) of timing precision. Power loss and disabling automation pause processing. Feedstock costs one nutrition per unit of the resulting density: fusing density 2 with density 1 costs 3 nutrition. Whole food items are converted as needed; fractional surplus remains stored. Cancelling with Eject Contents returns both animals and refunds the cycle's charge into stored nutrition. Unconverted food is ejected too; converted nutrition stays in the machine and is lost if the machine is destroyed.

Changing the global cap never reduces an existing animal's density. A cycle already accepted finishes at its original combined density. Species changes require an empty machine; target changes require an inactive cycle. Removal, including deconstruction, ejects contained animals.

## Density effects

- Milk, wool, unfertilized egg counts, meat and leather scale linearly with density.
- Food consumption and stomach capacity scale linearly too; density reduces pawn count, not feed costs.
- Health scales by the square root of density. Melee damage and armor increase more slowly. Settings can disable health/combat scaling.
- Visual size increases slowly, subject to the configured maximum. Market value scales linearly when enabled.
- A fertilized egg stores a dense embryo and hatches into one dense offspring. Dense eggs never stack or serve as densifier feedstock.
- Mammals produce density times as many normal offspring, retaining the original breeding behavior. This can increase pawn count; it is not a population cap.

Current defaults allow density up to 25 and visual scale up to 1.75. These are initial balance choices, not a measured recommendation for every colony.

## Build

The default target is RimWorld 1.7's .NET 10 runtime. Install the .NET 10 SDK, then provide the game installation and matching Harmony assembly:

```sh
dotnet build Source/HighDensityAnimals.csproj -c Release \
  -p:RimWorldDir="/path/to/RimWorld" \
  -p:HarmonyAssembly="/path/to/Harmony/1.7/Assemblies/0Harmony.dll"
```

`GameManagedPath` can be supplied instead of `RimWorldDir`. The project detects macOS, Windows, and Linux managed-folder layouts; the build and game tests in this workspace use macOS. Build output goes to `1.7/Assemblies`. Definitions live in `1.7/Defs`; `LoadFolders.xml` loads that version folder alongside shared content. Game references are not redistributed.

## Test

Run the validator tests without a game:

```sh
python3 -m unittest discover -s tests -p 'test_*.py'
```

Run the actual game suite against a local RimWorld 1.7 macOS installation:

```sh
python3 tests/run.py --game-dir "/path/to/RimWorld" \
  --harmony "/path/to/Harmony/1.7/Assemblies/0Harmony.dll" \
  --dotnet /path/to/dotnet
```

The runner builds source, stages only the matching 1.7 mod DLL, hashes it, creates disposable Core-only and full-DLC profiles, runs the regression harness, and restarts the game to verify persistence. It rejects failed assertions, errors, incomplete/missing/duplicate cases, stale DLLs, and timeouts. Profiles, saves, and logs go to ignored `tmp_tools/test-runs/` folders. Temporary Mods symlinks are removed afterward. Your normal save folder and ModsConfig are not changed. The test harness alters its test map and must never be enabled in a real colony. The old shell test now delegates to this runner.

See [tests/README.md](tests/README.md) for coverage and limitations, and [recorded results](tests/results/README.md) for the completed verification. [AUDIT.md](docs/AUDIT.md) records the original findings; [audit/](audit/) preserves the original shipped-binary smoke evidence.

## Release and removal

Distribute `1.7` (containing `Assemblies` and `Defs`), `About`, `Languages`, `Source`, `LoadFolders.xml`, README, LICENSE and CHANGELOG. Keep build intermediates and test/audit/scratch files out of the mod package. Test the packaged copy through the same clean profiles before publishing.

Push a `v*` tag to run the GitHub Actions prerelease workflow. It builds the 1.7 DLL against pinned NuGet reference assemblies, validates and archives the package, and publishes the ZIP and SHA-256 checksum as a GitHub prerelease. The same build/package step can be run locally with `python3 scripts/build_release.py --version v0.2.0-pre.1`.

Before disabling the mod in an existing save, eject every machine and remove/downgrade density-specific animals and eggs using an appropriate save migration. There is currently no automatic uninstall migration: removing the mod loses density metadata and removes its buildings. Keep a backup when changing mods.

## License and assets

Code is licensed under MIT; see [LICENSE](LICENSE). The building graphic and command graphics are referenced from RimWorld Core and remain Ludeon assets. The original vector preview and its rendered PNG are covered by the MIT license. Game files and Harmony are not included in the distribution. English translation keys are provided; contributions can add other languages without changing numeric productivity calculations.
