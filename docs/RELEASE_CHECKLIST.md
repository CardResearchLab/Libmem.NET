# Libmem.NET Release Checklist

Scope: Windows x64/x86 / .NET 8. Apply this checklist to the exact candidate commit for each new release. Repository checks and account-side publication setup are separate evidence.

Current source and published stable: `2.4.0` (assembly/file `2.4.0.0`). GitHub Release #45 and NuGet Trusted Publishing #46 completed; public 2.4.0 consumer smoke and stable-docs follow-up remain separate acceptance steps.

## 2.3.0 release gates

- [x] PR #128 accepted, x64/x86 Release and multi-arch NuGet checks succeeded, and feature merged into main.
- [x] Candidate metadata PR #130 passed Release x64/x86 and multi-arch NuGet tests (Build #412).
- [x] PR #130 merged to main; accepted commit `561bf1dfa82b7df2da4dc649a016c103f3af71ff` passed Build #413.
- [x] Branch `release/v2.3.0` Release #41 dry run passed without publishing.
- [x] Tag `v2.3.0` references the accepted commit; GitHub Release #42 published validated x64/x86 assets and checksums.
- [x] Tagged Release #43 completed NuGet Trusted Publishing (OIDC and push), and public x64/x86 Published NuGet Smoke #30 passed.
- [x] After public consumer validation, PR #131 advances the public NuGet CI baseline to 2.3.0 and updates stable documentation.
- [x] PR #129 (2.4.0) remained unmerged throughout 2.3.0 release and public verification.

## 2.4.0 release gates

- [x] Additive Assembly/Symbols feature PR #129 merged after x64/x86 Build #418; main Build #419 passed.
- [x] API boundary tests and consumer examples PR #132 merged after Build #421; main Build #422 and public 2.3.0 NuGet smoke #35 passed.
- [x] Candidate PR #133 passed Build #423 (Windows x64/x86 Release, runtime regression, Public API baseline, packaging and local NuGet consumers) and merged. 2.4.0 candidate CI passes exact-version Windows x64/x86 Release, runtime regression, Public API baseline, packaging and local NuGet consumer CI.
- [x] PR #133 merged; main Build #425 passed at `e73393d9805862db32c4e58dba1eadd91a191e56`.
- [x] `release/v2.4.0` built from accepted main; Release #44 dry run validated exact-version x64/x86 ZIPs, SHA-256, manifest provenance, multi-architecture NuGet and release notes **without publishing**.
- [x] `v2.4.0` annotated tag points at accepted commit; tagged GitHub Release #45 succeeded and published five validated assets.
- [x] Tagged manual Release #46 completed NuGet OIDC login and push successfully.
- [ ] Verify published 2.4.0 PackageReference restore/build/run/publish and architecture rejection from nuget.org on x64/x86.
- [ ] Complete post-release PR: advance the public NuGet smoke baseline to 2.4.0, exercise new managed APIs and update candidate docs to stable; merge only after public consumer checks pass.
- [ ] After post-release smoke/main checks pass, enter stability maintenance; do not begin ARM64, Linux, macOS or new feature work.

## Repository acceptance

| Area | Required evidence |
| --- | --- |
| Public API freeze | `api/Libmem.NET.PublicApi.txt` matches `src/Libmem.NET.h`; intentional breaking changes are documented and versioned. |
| Version metadata | `VERSION`, assembly attributes, tag and a non-empty versioned CHANGELOG section agree. |
| Windows builds | The exact candidate passes automatic Release x64 and Release x86 builds. Debug remains optional/manual. |
| Runtime smoke tests | Process/thread/module/symbol/memory/scan/assembly/disassembly/code-length cases pass. |
| External-process tests | Architecture-aware TestTarget coverage passes on x64 and x86, including remote memory, Hook boundaries, identity, process exit and dead-target behavior. |
| Hook / VMT lifecycle | Install/remove, trampoline execution, repeated Dispose, Reset and post-dispose rejection pass. |
| Injector lifecycle | Inject, discovery, explicit unload, repeated Dispose, idempotent unload and missing-file failure pass. |
| Runtime ZIP integrity | x64 and x86 manifest v2 metadata, file inventory/hashes, archive checksum, exact commit, version, platform and configuration are verified. |
| NuGet package layout | One package contains matching `win-x64` and `win-x86` managed/native assets, XML documentation, provenance, README, and buildTransitive selection logic. |
| Independent NuGet consumers | PackageReference restore/build/run/publish passes for x64 and x86; native dependencies reach output; AnyCPU fails with a clear diagnostic. |
| Release notes | Notes are rendered from the matching CHANGELOG section plus both architecture manifests/checksums. |
| GitHub Release safety | `release/v*` is dry-run only; tag pushes create GitHub Releases; manual runs on an existing tag are NuGet-only and require `publish-nuget`; no automatic branch deletion. |
| NuGet publish safety | OIDC login and push require a manual run on a published tag with `publish-nuget` enabled; same-ref Release runs are serialized without cancelling an in-flight publish; duplicate package pushes remain guarded by `--skip-duplicate`; branch validation requires no NuGet account credentials. |


## NuGet account-side setup

- [x] Verify the nuget.org Trusted Publishing policy matches `CardResearchLab/Libmem.NET` and the `.github/workflows/release.yml` workflow.
- [x] Verify the GitHub Actions repository secret `NUGET_USER` contains the NuGet account username consumed by `NuGet/login@v1`.
- OIDC supplies the short-lived publish credential at workflow runtime; do not store a long-lived NuGet API key in the repository.
- Release-branch dry runs and ordinary PR validation must not require NuGet account credentials.

## 2.2.0 publication sequence

- [x] Select `2.2.0` for the first stable dual-architecture release line.
- [x] Update version metadata and consumer-facing release documentation for x64/x86 support.
- [x] Confirm the release-preparation PR passes Release x64, Release x86 and the multi-architecture NuGet gate.
- [x] Create `release/v2.2.0` from the accepted candidate and verify the complete dry run.
- [x] Review both Runtime ZIPs, both SHA-256 files, the exact-version NuGet package, manifests and generated release notes; confirm all artifacts bind to the same source commit.
- [x] Create the matching `v2.2.0` tag only after the dry run is green.
- [x] Verify the GitHub Release publishes x64/x86 downloads and the exact-version NuGet asset.
- [x] Manually run Release on the published tag with `publish-nuget` enabled.
- [x] Restore independent x64 and x86 consumers from nuget.org using exact version `2.2.0`, then build, run and publish both.
- [x] Advance the automatic public-NuGet smoke baseline from 2.1.1 to 2.2.0 only after public x64/x86 validation succeeds.

## 2.2.1 publication record

- [x] Release-preparation PR #126 passed required x64, x86, and multi-architecture NuGet gates using exact 2.2.1 metadata (Build #402).
- [x] Merged PR #126 into `main`; exact commit `5e2b051a61943d0ac0825b2ac0125ffb29cf1721` passed Build #403.
- [x] Created `release/v2.2.1` from the accepted commit; Release #38 dry run passed ZIP/checksum, exact-version NuGet, and generated release-note verification.
- [x] Validated release provenance and checksums; created `v2.2.1` at the accepted commit and published GitHub Release with x64/x86 assets (Release #39).
- [x] Published nuget.org package through tagged Release #40 (`NuGet login (OIDC)` and `Publish Libmem.NET to nuget.org` both passed).
- [x] Verified published nuget.org-only 2.2.1 PackageReference restore/build/run/publish on x64 and x86, both architecture-matched runtime assets, and rejection of AnyCPU/arm64 explicit targets (Published NuGet Smoke #26).

## Compatibility boundary

- x64 and x86 are supported release architectures in 2.3.0.
- Consumers must explicitly select x64 or x86; AnyCPU is unsupported.
- Cross-bitness operation is not promised.
- ARM64 remains a future architecture and is not a 2.3.0 production target.
- Game state, snapshots, IPC, Unity/Mono/Hearthstone policy and other application logic remain consumer responsibilities.

See [RELEASES.md](RELEASES.md) and [CONSUMPTION.md](CONSUMPTION.md).
