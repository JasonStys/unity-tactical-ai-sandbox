# Balance simulation report

Date: 2026-09-18

## Fixed experiment

- Seeds: 1000 through 1499
- Matches: 500
- Maximum commands per match: 200
- Same utility weights and abilities for both teams
- Output: `docs/reports/generated/balance-report.json` and `.csv`

## Result

| Outcome | Count | Rate |
| --- | ---: | ---: |
| Blue wins | 253 | 50.6% |
| Red wins | 238 | 47.6% |
| Draws | 9 | 1.8% |

Average reported turn number was 30.21. Because turn numbering begins at one, resolved command counts are one lower for bounded matches. The checked-in JSON records exact action totals, damage, per-seed outcomes, and final hashes.

## Tuning finding

An earlier configuration penalized threat more heavily than it rewarded closing distance. In the same 500-seed window, 431 matches drew and many dealt zero damage. Increasing the distance weight and reducing threat/cover weights produced decisive engagement while preserving a small exposure preference. A 40-match draw threshold is now part of the test suite.

## Interpretation

The side difference is 15 wins across 500 seeds and neither team exceeds 51% of all outcomes. This suggests no obvious first-turn advantage in this sample, but it is not statistical proof of balance. The generated maps, tie-breaking order, abilities, and seeds define the experiment; new roles or objectives require new analyses.
