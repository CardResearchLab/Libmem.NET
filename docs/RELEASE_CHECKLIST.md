# Libmem.NET Release Checklist

Scope: Windows x64/x86 / .NET 8. Apply this checklist to the exact candidate commit for each new release. Repository checks and account-side publication setup are separate evidence.

Current source and published GitHub/nuget.org stable: `2.2.0`. Numeric assembly/file versions are `2.2.0.0`. The release preserves the 2.1.1 Public API and pinned rdbo/libmem revision while promoting x86 to official support.

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
| GitHub Release safety | `release/v*` is dry-run only; `gh release create` is tag-only; no automatic branch deletion. |
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

## Compatibility boundary

- x64 and x86 are supported release architectures in 2.2.0.
- Consumers must explicitly select x64 or x86; AnyCPU is unsupported.
- Cross-bitness operation is not promised.
- ARM64 remains a future architecture and is not a 2.2.0 production target.
- Game state, snapshots, IPC, Unity/Mono/Hearthstone policy and other application logic remain consumer responsibilities.

See [RELEASES.md](RELEASES.md) and [CONSUMPTION.md](CONSUMPTION.md).
