# Libmem.NET Release Checklist

Scope: Windows x64/x86 / .NET 8. Apply this checklist to the exact candidate commit for each new release. Repository checks and account-side publication setup are separate evidence.

**2.5.0 is now published stable** (assembly/file `2.5.0.0`); exact-commit main Build #461, Release #50 dry run, tagged GitHub Release #51 and NuGet OIDC push #52 succeeded. Independent nuget.org-only x64/x86 consumer acceptance passed PR #148 Published NuGet Smoke #48; main-branch CI must still be checked after merge.

## 2.4.1 patch release gates

- [x] PRs #137–#141 merged; main Build #443 succeeded for Release x64, Release x86 and local multi-arch NuGet.
- [x] Candidate PR #142 passed Build #444 including Release x64/x86 and multi-arch local consumer validation.
- [x] Candidate PR merged; main Build #445 succeeded for accepted commit `036556f6a508edee9401fe02fe0a8f7cdca39083`.
- [x] Release #47 on `release/v2.4.1` passed the exact-commit dry run without publication.
- [x] Release validation steps succeeded for x64/x86 ZIPs, SHA-256, package provenance, NuGet and unchanged pinned native libmem/Public API.
- [x] Tagged GitHub Release #48 published assets; separate explicit NuGet Trusted Publishing Release #49 succeeded (OIDC login and push).
- [x] Post-release PR #143 passed public 2.4.1 x64/x86 PackageReference restore/build/run/publish, XML/native assets and unsupported AnyCPU/ARM64 checks (Published NuGet Smoke #40), then merged; main public smoke #41 also passed.
- [x] Retain documented TOCTOU limitation: process-identity prechecks do not make native PID-only operations atomic.

## 2.5.0 release gates (publication and PR public-feed acceptance passed)

- [x] Additive ScanManager Try API PR #144 merged; feature Build #451 and main Build #452 passed, including x64/x86 and local multiarch NuGet.
- [x] Additive MemoryManager TryRead/TryWrite PR #145 merged; feature Build #453 and main Build #454 passed.
- [x] Hook/VMT lifecycle PR #146 merged after the fixed fixture passed feature Build #456, including both Hook/VMT suites and local NuGet.
- [x] Post-Stage-3 main Build #457 passed Windows Release x64/x86, Hook/VMT and multiarch gates.
- [x] Public API diff relative to the published v2.4.1 baseline: exactly five added declarations, zero removed declarations.
- [x] Candidate metadata PR #147 passed Build #460: x64, x86, runtime suites, public API contract and local multiarch consumers; `VERSION=2.5.0` and bilingual metadata align.
- [x] Candidate PR #147 merged as `190ed68e3d420932e19a724edfdc0e9482518e14`; post-merge main Build #461 succeeded.
- [x] `release/v2.5.0` at accepted main commit passed Release #50 dry run; x64/x86 ZIP, SHA-256, manifest provenance, NuGet exact-version package and rendered notes validated without publication.
- [x] Formal release notes rendered from 2.5.0 CHANGELOG and validated architecture manifest/checksum provenance in Release #50; tagged release notes contain the matching checksums.
- [x] Annotated tag `v2.5.0` targets accepted main commit; tagged GitHub Release #51 succeeded and published five assets.
- [x] NuGet was separately pushed on the published tag by Release #52 with OIDC; log confirms `Your package was pushed`.
- [x] PR #148 Published NuGet Smoke #48 passed independent nuget.org-only x64/x86 restore/build/run/publish, XML/native assets, all five new Try API consumer checks and AnyCPU/ARM64 rejection.
- [ ] Merge PR #148 only after final x64/x86 Build passes; then verify the new main Build and main Published NuGet Smoke on 2.5.0 (the PR CI already passed).
- [x] Preserve Windows x64/x86 / .NET 8 only, pinned native libmem, no AnyCPU/ARM64/cross-bitness or game-specific business logic.

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
- [x] Candidate PR #133 passed Build #423 and merged; main Build #425 passed full Windows x64/x86 Release, regression, Public API baseline, packaging and local NuGet consumer CI.
- [x] Candidate PR #133 merged as `e73393d9805862db32c4e58dba1eadd91a191e56`, verified by main Build #425.
- [x] `release/v2.4.0` at accepted main commit passed Release #44 dry run: exact-version x64/x86 ZIPs, SHA-256, manifest provenance, multi-architecture NuGet and formal notes; no publication.
- [x] Annotated `v2.4.0` tag resolves to accepted commit; tagged GitHub Release #45 published both runtime ZIPs, checksums and NuGet nupkg.
- [x] Tagged Release #46 explicitly enabled `publish-nuget`: OIDC login and NuGet push succeeded; public-feed x64/x86 smoke #36 verified published 2.4.0.
- [x] PR #135 advances the 2.4.0 public NuGet smoke baseline and updates Chinese/English stable documentation after public verification.
- [x] Enter stability-maintenance mode; do not begin ARM64, Linux, macOS or new feature work.

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

- x64 and x86 are supported release architectures in 2.4.0.
- Consumers must explicitly select x64 or x86; AnyCPU is unsupported.
- Cross-bitness operation is not promised.
- ARM64 remains a future architecture and is not a 2.4.0 production target.
- Game state, snapshots, IPC, Unity/Mono/Hearthstone policy and other application logic remain consumer responsibilities.

See [RELEASES.md](RELEASES.md) and [CONSUMPTION.md](CONSUMPTION.md).
