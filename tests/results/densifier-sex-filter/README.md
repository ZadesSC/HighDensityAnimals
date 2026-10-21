# Densifier sex filter verification

Verified on 2026-10-21 with RimWorld 1.7.4984 rev1242, macOS, French language, Harmony, and disposable profiles. All **58 runtime cases passed** across Core-only and full official DLC content: 27 regression cases, fresh-process reload, and legacy-save migration per content set. Builds completed with zero warnings or errors. All 12 Python result-validator tests passed.

Commands:

```sh
python3 -m unittest discover -s tests -p 'test_*.py'
DOTNET_ROOT=/Users/darren/.dotnet python3 tests/run.py --content both --language French --results-dir tmp_tools/test-runs/densifier-sex-filter-v2
```

The suite invokes the actual gizmo action to open a game FloatMenu and selects each of its three options through its real callback. It checks labels, icon availability, the Both default, loaded/active locks, stale menu callbacks, food-only selection changes, direct insertion filters, and genderless animal handling. Normal colonist Hauling work completes both a female-only cycle and a male-only cycle while excluding the other sex. Pair selection skips an older lone animal to use an available same-sex pair, including when mod settings are unavailable.

Fresh-process reload checks Both, Males only, and Females only selections, including loaded and active machines. A paused mixed-sex cycle is cancelled during load, refunds its charge exactly once, retains both animal identities, and allows their ejection. A running cycle whose pair becomes mixed also cancels without consuming animals. The existing legacy deadline migration and full regression suite pass.

The six JSONL files preserve per-case results. `manifest.json` records game version, tested DLL and Harmony SHA-256, and exact working-tree source hashes. Its `sourceCommit` is the base commit before this change was committed; the source hashes identify the tested implementation. The shipped DLL SHA-256 is `096fddcc12dbf3d360df65fe44ae112eb12b08fd81dd9ce8303aab1a04b9ee8d`. Complete logs and disposable saves remain in the ignored run directory shown above.

Menu verification runs callbacks inside the game; it does not simulate physical mouse clicks or inspect rendered screenshots. Third-party mod combinations are outside this matrix.
