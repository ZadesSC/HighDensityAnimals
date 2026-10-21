# Changelog

## 0.2.0

- Keep normal live-birth litter sizes and give newborn animals the average parental density, rounded down; an unknown father counts as density 1.
- Add a per-machine Prevent density waste gizmo, enabled by default; disabling it allows overflowing fusion pairs with capped output and full input nutrition cost.
- Target RimWorld 1.7 only, move definitions into `1.7/Defs`, and publish source-inclusive prerelease packages through GitHub Actions.
- Eject the surviving animal after fusion when no eligible partner remains for another cycle.
- Rebuild the 1.6 assembly and explicitly select version-specific load folders, fixing the missing densifier storage tab on 1.6.
- Build and package for RimWorld 1.7 with configurable references; remove official expansion requirements.
- Preserve contained animals on removal and retain contents when placement fails.
- Prevent dense egg metadata loss through stacking and splitting; consolidate fertilized egg batches without leftover duplicates.
- Preserve accepted density when limits change; snapshot cycle inputs and refund cancellation once.
- Pause fusion without power or automation and persist remaining processing time; migrate legacy deadline saves.
- Choose feasible animal pairs and check reachability/reservations/forbiddance; reject juveniles using animal life stages.
- Refresh contained/world animal caches and fix pawn market-value scaling.
- Translate player controls and calculate productivity displays from numbers.
- Show vanilla-style floating target-density feedback when adjusting densifiers.
- Replace text-based tests with a freshly built game regression suite and result-validator tests.
