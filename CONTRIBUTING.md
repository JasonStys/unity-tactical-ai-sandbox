# Contributing

1. Create a focused branch and keep generated output deterministic.
2. Add or update tests for behavior changes.
3. Run `node scripts/generate-code-index.mjs` after moving declarations.
4. Run `npm test`; every step must pass before review.
5. Explain gameplay trade-offs, compatibility impact, and evidence changes in the pull request.

Do not commit Unity-generated `Library`, `Temp`, `Logs`, or user-setting directories. Do not replace the checked-in original placeholder scenario with third-party assets unless their redistribution terms are documented.
