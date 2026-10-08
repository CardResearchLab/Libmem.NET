# Libmem.NET Release Checklist

Scope: Windows x64/x86 / .NET 8. Apply this checklist to the exact candidate commit for each new release. Repository checks and account-side publication setup are separate evidence.

The source is preparing **2.4.0** (assembly/file `2.4.0.0`); current published stable remains **2.3.0**. Prior 2.3.0 feature, candidate, tagged release, and public consumer smoke gates are complete.

## 2.4.0 candidate and release gates

- [x] PR #129 merged; main Build #419 passed (Windows x64/x86 Release and Multi-arch NuGet).
- [x] PR #132 merged; main Build #422 and Published NuGet Smoke #35 passed.
- [ ] Release-candidate PR passes exact 2.4.0 version metadata, x64/x86 runtime tests, Public API audit and local dual-arch NuGet consumers.
- [ ] Merge release-candidate PR and verify post-merge main Build for its exact commit.
- [ ] Create `release/v2.4.0` from accepted commit; complete Release dry run (ZIP manifests, SHA-256 checksums, exact-version NuGet package and rendered notes) without publishing.
- [ ] Confirm both architectures and provenance match the accepted commit; review CHANGELOG, bilingual notes, API shape and pinned native libmem revision.
- [ ] Only after dry run succeeds, create immutable `v2.4.0` tag and verify tagged GitHub Release assets.
- [ ] Explicitly run NuGet Trusted Publishing on the published tag and verify nuget.org package.
- [ ] Independently smoke-test published 2.4.0 on x64/x86 and update stable docs/public-feed smoke baseline in a post-release PR.
- [ ] Freeze new feature development after 2.4.0 and switch to stability maintenance.

## 2.3.0 release gates

- [x] PR #128 accepted, x64/x86 Release and multi-arch NuGet checks succeeded, and feature merged into main.
- [x] Candidate metadata PR #130 passed Release x64/x86 and multi-arch NuGet tests (Build #412).
- [x] PR #130 merged to main; accepted commit `561bf1dfa82b7df2da4dc649a016c103f3af71ff` passed Build #413.
- [x] Branch `release/v2.3.0` Release #41 dry run passed without publishing.
- [x] Tag `v2.3.0` references the accepted commit; GitHub Release #42 published validated x64/x86 assets and checksums.
- [x] Tagged Release #43 completed NuGet Trusted Publishing (OIDC and push), and public x64/x86 Published NuGet Smoke #30 passed.
- [x] After public consumer validation, PR #131 advances the public NuGet CI baseline to 2.3.0 and updates stable documentation.
- [x] PR #129 (2.4.0) remained unmerged throughout 2.3.0 release and public verification.

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
