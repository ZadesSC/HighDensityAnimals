# Live-birth density inheritance verification

Verified on 2026-10-21 with RimWorld 1.7.4984 rev1242, macOS, French language, Harmony, and disposable profiles. All **54 runtime cases passed** across Core-only and full official DLC content: 25 regression cases, fresh-process reload, and legacy-save migration per content set. Builds completed with zero warnings or errors. All 12 Python result-validator tests passed.

Commands:

```sh
python3 -m unittest discover -s tests -p 'test_*.py'
DOTNET_ROOT=/Users/darren/.dotnet python3 tests/run.py --content both --language French --results-dir tmp_tools/test-runs/offspring-density-inheritance-v2
```

The runtime suite verifies one calf per cow birth; equal and unequal parent densities with rounding down; dense fathers with normal mothers; unknown fathers as density 1; unchanged seeded pig litter sizes; density on every littermate; overflow-safe averaging; nested birth contexts and cleanup after a spawning exception. Naturally ticked near-term pregnancy and fresh-process pregnancy reload both produce a single calf. The reload case changes the father's density before birth and confirms inheritance uses that updated value. Existing egg laying/hatching tests also pass.

The six JSONL files preserve per-case results. `manifest.json` records game version, tested DLL and Harmony SHA-256, and exact working-tree source hashes. Its `sourceCommit` is the parent commit before this change was committed; the source hashes identify the tested implementation. The shipped DLL SHA-256 is `47ea6d3832018b9ea003cdb888eef2cb0a4e749b5f1597d9a32e3b023d758feb`. Complete logs and disposable saves remain in the ignored run directory shown above.

Pregnancy completion starts near term; this run does not simulate an entire gestation. Third-party reproduction patches were not tested.
