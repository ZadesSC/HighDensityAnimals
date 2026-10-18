> Implementation update: the fixes and test replacement described below were applied for the 1.7 release. See [CHANGELOG.md](../CHANGELOG.md), [tests/README.md](../tests/README.md), and [passing results](../tests/results/README.md) for current behavior and coverage. The original findings and shipped-binary results below are preserved as historical evidence.

# High Density Animals audit

Reviewed 2026-10-14 at commit `830b8ca`. This is a functioning prototype with several animal-loss bugs to fix before release. The existing DLL runs in RimWorld **1.7.4977** when loaded from an isolated compatibility copy; the repository itself still declares and packages only 1.6 support.

## Confirmed in the 1.7 game process

| Priority | Finding | Reproduction and consequence | Fix direction |
| --- | --- | --- | --- |
| High | Deconstruction strands contained animals | Insert a cow, deconstruct the machine. The cow remains unspawned, alive, and held by the destroyed `Building_AnimalDensifier`. It is not returned to the colony. | Implement an explicit destruction/deconstruction policy that safely transfers contents before losing the map reference. Test destruction, removal, and failed placement. |
| High | Normal egg stacks erase dense embryos | Create a density-five fertilized egg and a normal fertilized egg. `normal.TryAbsorbStack(dense, true)` succeeds: the result is two normal eggs, and the dense egg is destroyed. | `CompDenseEgg.AllowStackWith` must inspect both eggs. Prevent merging if either egg is dense. |
| High | Lowering the cap destroys existing density | Start fusion with two density-three cows, then lower global max density to two. Completion destroys the secondary and reduces the survivor to density two. | Revalidate the pending operation; preserve all input density. Never clamp the result after irreversibly consuming an animal. |
| Medium | Fusion progresses during power loss | Start fusion, turn power off, advance beyond the deadline, restore power. Fusion completes on the next rare tick without the remaining powered processing time. | Track active processing time or extend the deadline while disabled/unpowered. Define whether automation toggles pause a running cycle. |

Relevant implementation locations:

- Containment and ejection: `Source/Buildings/Building_AnimalDensifier.cs:505`. There is no custom destruction handler. `EjectAll` also removes items before placement and ignores placement failure, unlike `EjectPrimary`.
- Egg stacking: `Source/Comps/CompDenseEgg.cs:38`.
- Density loss: `Source/Buildings/Building_AnimalDensifier.cs:465`.
- Power timing: `Source/Buildings/Building_AnimalDensifier.cs:133` and `:455`.

Passing runtime checks cover component injection and Harmony registration; two-animal fusion and nutrition consumption; inspection/gizmo enumeration; fertilized and unfertilized egg production; one dense chick hatching; three calves from a density-three cow; milk amount, meat/leather yield stats, health, melee and hunger-stage scaling; animal loading and nutrition delivery through real hauling jobs; and saving/loading contained animals plus an active fusion cycle. The final isolated run recorded **33 passing assertions and 6 failed assertions representing the four bugs above**, with no logged runtime exceptions/errors. See the raw results for individual assertions. A passing yield-stat check does not certify every butcher recipe or integration.

## Additional source findings

These need focused regression tests; they are not claimed as reproduced gameplay failures:

1. **Animal selection can stall or choose a poor pair.** `FindNextAnimal` sorts by age and checks reservation, but not reachability, forbiddance, or availability of a compatible partner. An oldest animal in an unreachable enclosure can be selected repeatedly. With same-sex fusion enabled, loading the only female before considering several males can leave the machine waiting indefinitely. Choose a feasible pair before committing the first animal, and filter candidates using actual hauling constraints. (`Building_AnimalDensifier.cs:317`)
2. **Start/completion validation is incomplete.** Admission checks species, sex, eligibility and summed density, but `CanStartCycleNow` checks only two occupied slots and nutrition. Target/global settings can change after insertion. Snapshot or revalidate the intended inputs and cost at operation boundaries. (`Building_AnimalDensifier.cs:445`)
3. **Settings refresh skips nonspawned animals.** Cache refresh visits only spawned map pawns. Caravan and contained animals can retain old combat/visual settings until another recalculation. Add lifecycle-aware refresh or settings-version invalidation. (`DenseAnimalUtility.cs:133`)
4. **Localization is missing and affects correctness.** The stat-display patch identifies entries by English labels and parses English-formatted numeric strings. It can fail to scale displays in other languages or misread locale-specific numbers. All UI strings are hard-coded. Use translation keys and numeric values/stable identifiers instead of display strings. (`HighDensityAnimalsPatches.cs:171`, `:239`)
5. **Contained-pawn behavior needs an explicit design.** The custom holder does not explicitly tick its contents. Decide whether this is intended stasis, how nutrition/health/pregnancy/rot should behave, and what happens during long waits for another animal.
6. **Configuration UX needs feedback.** The species menu silently ignores changes while animals are loaded; target changes remain available during active cycles; missing partners have no specific explanation. Disable unavailable commands with a reason and show actionable waiting states.

## What is missing

- **Portable builds and a deliberate 1.7 package.** Replace developer-specific `G:`/WSL paths with configurable game/Harmony references. The production build currently fails locally on missing .NET Framework 4.8 reference assemblies. The project references Harmony workshop directory `1127530465`, while About.xml links `2009463077`; resolve this discrepancy. Running successfully in the smoke test does not replace a clean 1.7 build/package validation.
- **Behavioral regression tests.** The existing shell script checks source text rather than executing behavior. Some checks use regex alternation, so finding any one listed symbol satisfies a check intended to cover multiple requirements. Add runtime tests for the four confirmed bugs, actual automatic work selection, failed ejection, and save/load at each phase.
- **README and player documentation.** Explain installation, supported versions/DLCs, work assignment, nutrition costs, survivor selection, density scaling, breeding behavior, cancellation, and safe uninstall. The About description still describes an experimental workspace.
- **DLC compatibility declaration and tests.** The building uses the GrowthVat graphic and Biosculpter nutrition tab while About.xml declares only Harmony. Test Core-only and intended expansion combinations; provide fallback assets or declare required content. The runtime audit used the installed expansions.
- **Progression/balance decisions.** There is no research prerequisite. Decide whether immediately available fusion is intentional. Measure food/output balance, fusion costs, colony wealth, bonded-animal consequences, and whether mammal litter multiplication undermines the population-reduction goal.
- **Distribution polish.** Add an appropriate license, changelog, preview image, credits/provenance for copied assets, and release/build instructions. Most commands lack dedicated icons and the building reuses an existing game graphic.
- **Performance evidence.** Rare machine ticks and cached density factors are sensible, but every animal/egg gets a comp, work scanning sorts candidates, and mammal births multiply pawn count. Benchmark realistic colony sizes and multiple machines before claiming performance gains.

Recommended order: preserve animals/embryos and density first; fix cycle timing and selection second; establish portable builds and regression coverage; then finish documentation, compatibility, balance, and presentation.

## Evidence and limits

See [runtime results](../audit/runtime-results.txt), [test methods](../audit/METHODS.md), and [harness source](../audit/RuntimeAudit.cs). Tests ran the shipped DLL without production modifications. The mod XML syntax checks and existing shell test passed. The new audit files are the only repository changes.

The harness uses an actual quicktest map and game process. It accelerates some state transitions through reflection and runs hauling jobs through actual simulation ticks. It does not certify manual UI usability, rendering, all content combinations, multiplayer, or modpack compatibility. Source-review findings and runtime failures are separated above.

## Proposed fixes and test gaps (follow-up review)

This is a proposal; production fixes and test-runner changes have not been implemented.

### Recommended implementation order

1. **Make test outcomes reliable.** Add a runner that builds the chosen mod target, stages exactly that DLL, records its hash, creates a unique profile/report directory, and launches the game. Require every expected case to finish; fail on any assertion, harness exception, missing result, timeout, or relevant game error. Separate setup requirements (abort the case) from assertions (record failures). Restore settings in `finally`, reset fixtures between cases, and use reproducible game generation where possible. Keep shipped-binary compatibility tests separate from source-build regression tests.
2. **Preserve contents and density.** Make egg stacking symmetric. Centralize safe content transfer/ejection, using holder-aware transfers and handling placement failures without abandoning a pawn. Handle deconstruction/destruction before map context disappears; normal ejection must retain contents on failure, while destruction needs a tested game-supported fallback. Specify removal/minification separately from destruction. Snapshot the two input pawn identities, their densities, result density, and charged nutrition when a cycle starts. The cap governs admission, not truncation: recommend completing an already accepted cycle at its full original density even if the cap is subsequently lowered. New cycles use the new cap. If inputs no longer match, cancel safely and refund unspent/reserved nutrition exactly once. Destroy the secondary only after the survivor's comp and planned result have been validated and applied.
3. **Define cycle semantics and selection.** Persist remaining powered processing ticks; pause on power loss and automation disable. Continue rare ticking with a documented timing tolerance of at most one rare interval, or account for power transitions explicitly if exact timing is required. Migrate existing deadline saves once. Validate compatibility before starting; prevent silent target/species changes from changing a running operation. Select a reachable, reservable, nonforbidden compatible pair before loading the first animal, rechecking at pickup/insertion. Make the lack of a compatible partner visible. Avoid holding a reservation indefinitely while waiting for feedstock.
4. **Make settings/lifecycle behavior explicit.** Refresh relevant cached values for contained and world/caravan animals as well as spawned animals, or use a settings revision checked at relevant lifecycle/stat boundaries without per-tick scans. Decide and document stasis behavior. Recommend stasis while held for this prototype, with explicit safe ejection/cancellation, rather than accidentally mixing partially ticking needs and health. Replace English-label stat matching and formatted-number parsing with stable identifiers/numeric calculations and translation keys.
5. **Finish the release path.** Add configurable game/Harmony paths and explicit version-specific build/output targets. Preserve 1.6 only if it is independently built and tested. Validate the actual 1.7 package on a clean profile. Test Core-only and required DLC combinations, then either provide fallback assets/UI or declare requirements. Add README, balance/progression decisions, asset credits, license chosen by the owner, changelog, and release instructions. Benchmark scaling after correctness is established.

### Weaknesses in the current tests

| Gap | Why current passes are insufficient | Required change |
| --- | --- | --- |
| Shell checks use text and OR patterns | Finding one of several identifiers passes checks labelled as requiring all of them. They do not compile or execute code. | Keep only useful structural checks; use separate checks for genuinely required symbols, build the source, and rely on behavior for correctness. |
| Tests enforce implementation details | The shell script requires an absolute deadline and forbids a particular countdown expression. That can reject a valid pause-aware fix. | Replace those constraints with powered-duration, pause/resume, and persistence assertions. |
| Runtime failure is only text | `Check(false)` logs and continues; `Case` catches exceptions; the reload path can emit `COMPLETE` after a failed case. There is no committed runner enforcing a failing exit status. | Emit structured counts/status and have an external runner reject failures and incomplete suites. Treat COMPLETE as termination, not success. |
| Stale-binary risk | The harness references `../1.6/Assemblies/HighDensityAnimals.dll`; it does not build production source. | For regression runs, build and hash the exact tested artifact. Preserve a separate test mode for the shipped DLL. |
| Manual intervention bypasses logic | Direct insertion, injected nutrition, direct job creation, forced deadlines, and direct birth/hatch calls skip work selection, timing, storage conversion, gestation, and incubation. | Retain focused component cases but add full autonomous cycles and natural progression cases. |
| Save/load is incomplete | The test reloads in the same process and overwrites the restored deadline before completion. | Restart the game, verify every saved field and remaining duration, then complete through normal ticks without changing state. |
| Assertions can be too weak | `GetGizmos().Any()` can pass on a base gizmo; egg counts `>=3` accept overproduction; stock nutrition `>=2` does not prove exact hauling/consumption; increased melee stat is not actual damage. | Assert specific commands, exact/bounded expected quantities, conservation of resources, and actual gameplay outcomes where relevant. |
| Fixtures influence later cases | Cases share a world, settings, game tick counter, and generated entities. Exploratory runs exposed worker and automation interference. | Reset or independently load fixtures; use fresh workers and explicit initial conditions; restore globals even after exceptions. |
| Repeatability/portability missing | Random quicktest generation, hard-coded report path, manual staging, and no expected-case manifest. | Parameterize paths, use unique run IDs, record seeds/version/mod list, package the harness automatically, and verify all required cases ran. |

A concrete mutation check was run in a temporary copy: renaming `DenseAnimalUtility.ArmorOffset` to `MissingArmorHelper` while leaving callers unchanged still produced exit code **0** from `tests/verify_density_refactor.sh`. This demonstrates that its passing result does not even imply compilable source. The repository source was not mutated.

### Regression coverage to add

| Area | Required cases and acceptance criteria |
| --- | --- |
| Contents/lifecycle | Empty, one-animal, two-animal, food-loaded and active machines; manual eject, deconstruct, combat destruction, failed placement, interrupted carrying, and applicable uninstall/reinstall. No lost/duplicated pawn IDs or ownerless transfers. |
| Eggs | Normal/normal positive control; normal/dense and dense/normal rejection; dense/dense rejection; hauling into existing stacks; split/transfer/save/reload; exact embryo density after hatching. |
| Fusion limits | Densities 1, 2, configured maximum and above-maximum existing animals; target/cap changes before insertion, after loading, during processing and after reload. Sum of input density must be preserved on success, or both original animals preserved on cancellation. |
| Time and power | Full 3,000 powered ticks, pause/resume at several points, power flicker, automation off/on, save while paused, fresh-process reload, and legacy-save migration. No early completion or double charging; allow only documented rare-tick quantization. |
| Automatic work | Configure through commands, then let enabled Hauling work select jobs. Test unreachable/forbidden animals and machines, lone-sex candidates, eligible alternatives, pregnant/juvenile/downed animals, reservations, two workers/two machines, and cancellation while carrying. |
| Nutrition | Exact requirement, shortage, fractional-unit rounding, multiple food stacks/types, filter changes, forbidden food, dense egg rejection, excess feedstock, cancellation/refund and restart. Account for delivered, consumed, stored and returned nutrition. |
| Outputs and stats | Actual milk/shear collection, butchered meat/leather stacks, food depletion and eating over time, actual combat damage/armor, market value, settings toggles and negative controls at density one. Compare matched baselines; avoid inequalities that permit double scaling. |
| Persistence | Save in every machine phase; verify animal identities, density, nutrition, species, target, filters, automation, timer and egg metadata after a new-process load. Include caravan/contained-pawn cache behavior. |
| UI/compatibility | Invoke specific mod commands, test disabled reasons and species/target changes, inspect storage tab, rendering and size caps, non-English locale, Core-only and declared DLC combinations, and clean-package startup. |
| Performance | Compare a fixed colony with/without the mod and equivalent production at lower pawn count. Measure tick cost, allocations, repeated work scans and several active machines; keep these separate from correctness tests. |

Start with a small release gate: clean build/package load, the four confirmed bug regressions, one autonomous multi-cycle densification, interrupted carrying/ejection, and a fresh-process save/reload. Broaden to production, settings, UI and compatibility once this gate is reliable.
