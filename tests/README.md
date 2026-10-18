# Test suite

`run.py` builds the 1.7 source and separate regression mod, stages a package, records the loaded DLL hash, and runs disposable profiles. Default coverage includes Core-only and all installed official DLCs. Each content set runs in three processes: regression fixtures, reload of the saved active/paused cycle, and migration of a copy converted to the old deadline save schema. `--content core` or `--content full` limits the matrix. `--language French` exercises translated vanilla productivity labels and fallback mod strings. See the root README for commands and [results/README.md](results/README.md) for retained verification.

`test_runner.py` verifies the validator rejects assertion failures, missing/duplicate cases, a self-reduced manifest, stale DLLs, game errors, fatal harness exceptions, and incomplete runs. The independent Python manifest prevents the C# harness from silently dropping tests. Result files are structured JSONL, and the runner exits nonzero on failure. `COMPLETE` means execution ended, not that tests passed.

## Runtime coverage

- Correct version-specific DLL, loaded graphic, research prerequisite, and Core storage tab.
- Normal/normal egg stacking; both normal/dense directions; dense/dense rejection; merge attempts; metadata on split.
- Species, juvenile, target, sex, pregnancy, mental-state, capacity and duplicate-pawn admission.
- Compatible-pair selection, reservations, unreachable enclosure, forbidden machine and forbidden feedstock.
- Placement failure injected only in the separate harness; manual ejection retains ownership and removal uses its emergency fallback.
- Deconstruction, destruction, despawn, off-map destruction, and interrupted carrying.
- Processing under normal ticks, power loss, automation pause/resume, accepted-cap changes, cancellation/resource conservation and single refund.
- Exact food shortage, sufficient supply, fractional remainder, rejected dense eggs, and storage filters.
- Actual milk/shearing collection and butcher product stacks; health, nutrition capacity and market value; food-need depletion without eating jobs.
- Dense egg laying/hatching and mammal offspring; exact quantities, with both direct helpers and naturally ticked near-term completion.
- Registered render scale hook, transformed dimensions/size cap, visual disable control; contained-pawn stasis, contained/world cache refresh, settings disable controls, single egg market scaling, specific command availability, numeric productivity displays.
- Two successive fusion cycles selected through normal colonist Hauling work, with feedstock accounting.
- Fresh-process persistence of empty, single-animal, food-waiting, food-only, powered and paused machines; animal identities/density, timer, liquid nutrition, filters, species, target, automation and egg metadata, followed by normally ticked completion without another charge.
- Fresh-process migration from deadline saves with absent new snapshot fields.

Fixtures use fresh capable workers, drafted original colonists, full animal food meters, controlled animal-generation seeds, reset settings, and cleanup between cases. Setup/assertion exceptions fail the case. Power is supplied through the game's unlimited-power developer setting; the outage case disables it and switches the real power trader off. Timing remains quantized to the game's 250-tick rare interval. The production assembly contains no test hooks. The separate harness injects the placement failure and observes real ingestion to account for eaten feedstock.

## Optional performance workload

`--benchmark` starts another fresh quicktest profile/process per content set. `--benchmark-only` runs just these workloads. It records six 1,000-tick samples after 2,000 warmup ticks in a fixed fenced pen for 120 density-one cows, 30 density-four cows (equal represented production), and 30 dense cows with 100 idle machines. Tick-time samples and simulation-thread allocations are recorded outside fixture generation. It is a scaling observation, not a performance threshold or comparison to a mod-free game. Rendering, realistic colony behavior, active multi-machine work scanning, and large modpacks are outside this workload; do not interpret it as FPS or whole-colony speedup.

## Remaining limits

This suite is stronger than the original source-text checks, but it is not exhaustive. It currently requires the macOS RimWorld 1.7 runtime and .NET 10 SDK. Offspring tests include direct helpers and naturally ticked completion from near-term progress; they do not simulate entire gestation/incubation periods. Combat coverage checks the real melee-damage calculation, its disable control, and damage taken across 64 matched seeded hits; sustained AI combat, wider injury/armor distributions, UI mouse interaction, caravan travel, minification, multiplayer and third-party mod combinations need dedicated fixtures. The saved migration fixture represents the old deadline schema, not every historical save variant. Other DLC combinations can be added to the runner matrix.

The original `audit/RuntimeAudit.cs` and its results are preserved as historical evidence for the shipped 1.6 DLL tested on 1.7. They are not the current regression suite and should not be used to certify changes. The legacy shell entry point delegates to `run.py`.
