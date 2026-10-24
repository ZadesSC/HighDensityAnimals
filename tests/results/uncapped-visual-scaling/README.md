# Uncapped visual scaling verification

Verified on 2026-10-24 with RimWorld 1.7.4985 rev1269 on macOS, English language, Harmony, and disposable Core-only and full official DLC profiles. All **62 runtime cases passed**: 29 regression cases plus fresh-process reload and legacy-save migration per content set. All 12 Python result-validator tests passed. The build completed with zero warnings or errors.

Commands:

```sh
python3 -m unittest discover -s tests -p 'test_*.py'
python3 tests/run.py --content both --results-dir tmp_tools/test-runs/uncapped-visual-scaling-20261024-v3
```

Rendering coverage checks vanilla size at density 1, intermediate growth at 5, continuation at 30/40/50/100, and 2.5× cached scale and actual mesh transforms for chickens, cows, and thrumbos at density 25. It also checks transformed dimensions at density 50, unchanged depth axis, and disabling visual scaling. The settings fixture verifies density changes refresh the visual cache while an animal remains in stasis inside a densifier.

The six JSONL files retain per-case results. The manifest records exact source hashes and the loaded DLL SHA-256, `29dfd96e4a888cefb243da7da3e0d6a19370163e22e82a27dda62b92ef8c33fb`. Its source commit is the branch base before these changes were committed; the source hashes identify the tested implementation. Complete logs and disposable profiles remain in the ignored run directory above. Earlier local runs exposed incorrect expected constants and invalid settings-fixture targets; this directory contains the corrected final run.

These checks exercise the game's rendering transform hook, not screenshot appearance or occlusion in a busy colony. Third-party mod combinations were not tested.
