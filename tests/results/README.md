# Recorded verification

The uncapped visual scaling curve and rebuilt assembly are verified in [uncapped-visual-scaling/README.md](uncapped-visual-scaling/README.md).

The combined inheritance and sex-filter build is verified in [offspring-density-integrated/README.md](offspring-density-integrated/README.md).

The earlier standalone live-birth density inheritance change is verified separately in [offspring-density-inheritance/README.md](offspring-density-inheritance/README.md).

The earlier standalone per-machine sex filter and mandatory same-sex fusion are verified in [densifier-sex-filter/README.md](densifier-sex-filter/README.md).

## Earlier baseline

RimWorld 1.7.4977 rev1248, macOS, French game language. The newly built 0.2.0 DLL passed **46 runtime cases**: 21 regression cases, fresh-process reload, and legacy-save migration in each of Core-only and full official DLC profiles. All 12 Python result-validator tests also passed. Builds completed with no warnings or errors.

The JSONL files preserve per-case results. `summary.json` records the baseline DLL SHA-256, source hashes, and benchmark medians. Original complete logs and disposable saves remain in ignored `tmp_tools/test-runs/20261014-151818-5a759de5/`. These artifacts describe the earlier build, rather than the current DLL.

## Synthetic benchmark

One separate Core/French process, fenced animals, 2,000 warmup ticks, six samples of 1,000 ticks each. The two animal populations represent equal production density.

| Pawns | Density | Idle machines | Median milliseconds / 1,000 ticks | Median allocated bytes / 1,000 ticks |
| --- | --- | --- | --- | --- |
| 120 | 1 | 0 | 188.92 | 6,986,188 |
| 30 | 4 | 0 | 91.51 | 3,539,008 |
| 30 | 4 | 100 | 84.93 | 3,836,820 |

These are simulation-thread samples from a single serial launch, with AI state, JIT and GC variation. They exclude rendering, active work scanning, and a mod-free baseline. They support a scaling observation, not a whole-colony speedup claim. Raw samples are in `core-benchmark.jsonl`; scratch logs remain in `tmp_tools/test-runs/20261014-151414-96be94a0/`.

See the parent test README for coverage and limits. Re-run after changing source; these files are retained evidence, not a replacement for executing tests.
