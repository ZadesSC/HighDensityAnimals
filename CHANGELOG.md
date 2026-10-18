# Changelog

## 0.2.0

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
