# Security design

## Trust boundaries

- Scenario, save, and replay JSON is untrusted input.
- CLI paths are supplied by the local operator or CI.
- Unity serialized fields are designer-authored input.
- GitHub Actions dependencies are external executable code.

## Controls

- JSON parsing disallows comments and trailing commas and caps nesting depth.
- Every document version and enum value is checked.
- Map dimensions cap allocation at 128 × 128 cells.
- Batch size, path expansion, utility evaluation, and turn count are bounded.
- Arithmetic that accumulates path costs uses checked operations.
- Replays validate each SHA-256 state fingerprint.
- Domain constructors reject invalid hit points, abilities, terrain, and spawns.
- The core performs no network or filesystem I/O.
- Workflows declare `contents: read` and pin third-party actions to full commit SHAs.
- Repository validation scans for common private-key and token signatures.
- The .NET graph contains no third-party packages; `NuGet.Config` clears remote sources.

## Non-goals

Replay hashes are integrity checks, not signatures. The sandbox does not implement authentication, authorization, anti-cheat, encrypted saves, multiplayer transport, or remote content delivery. Those require a separate threat model.

## Reporting

Use the private process in the root `SECURITY.md`. Include the affected version, a minimal reproduction, impact, and suggested mitigation when available.
