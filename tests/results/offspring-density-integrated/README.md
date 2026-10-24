# Combined inheritance and sex-filter verification

Verified on 2026-10-23 after integrating `dev` into PR #2, with RimWorld 1.7.4985 rev1269, macOS, French language, Harmony, and disposable profiles. All **62 runtime cases passed**: 29 regression cases plus fresh-process reload and legacy-save migration in each of Core-only and full official DLC profiles. All 12 Python result-validator tests passed. Builds completed with zero warnings or errors.

Commands:

```sh
python3 -m unittest discover -s tests -p 'test_*.py'
DOTNET_ROOT=/Users/darren/.dotnet python3 tests/run.py --content both --language French --results-dir tmp_tools/test-runs/offspring-density-integrated-20261023
```

This combined suite retains all inheritance and sex-filter tests. It covers single-calf births, normal seeded pig litters, rounded parental averages, unknown fathers, nested birth contexts, spawning exception cleanup, naturally completed pregnancy, and saved pregnancy with paternal density changed before birth. The same runs cover all three dropdown choices, automatic male/female hauling, mandatory same-sex fusion, filter persistence, and refund/ejection of paused legacy mixed-sex cycles. The saved fixture includes both a near-term pregnancy and an invalid mixed-sex cycle.

The six JSONL files preserve per-case results. `manifest.json` records the tested DLL and Harmony hashes, game version, and exact source hashes. Its `sourceCommit` identifies the PR branch before the integration merge was committed; the working-tree source hashes identify the combined implementation. The rebuilt shipped DLL SHA-256 is `4173307ab0f746fe2d19caaae3ca6472662050a783bb18b0c287209fccdf3640`. Complete logs and disposable saves remain in the ignored run directory shown above.

Pregnancy completion starts near term. Dropdown tests invoke actual game callbacks rather than physical mouse clicks. Third-party reproduction and other mod combinations were not tested.
