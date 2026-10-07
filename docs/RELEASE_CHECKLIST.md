# Libmem.NET Release Checklist

Scope: Windows x64 / .NET 8. Apply this checklist to the exact candidate commit for each new release. Repository checks and account-side publication setup are separate evidence.

Published stable: `2.0.0`. Current candidate: `2.1.0`; numeric assembly/file versions are `2.1.0.0`. This release preserves the 2.0.0 Public API baseline and hardens Hook / VMT behavior without expanding official x86 support.

## Repository acceptance

| Area | Required evidence |
| --- | --- |
| Public API freeze | `api/Libmem.NET.PublicApi.txt` matches `src/Libmem.NET.h`; intentional breaking changes are documented and versioned. |
| Version metadata | `VERSION`, assembly attributes, tag and a non-empty versioned CHANGELOG section agree. |
| Windows x64 build | The exact candidate passes automatic Build in Release x64. Debug may be checked manually with `debug`; it is not part of the default gate. |
| Runtime smoke tests | Process/thread/module/symbol/memory/scan/assembly/disassembly/code-length cases pass. |
| External-process tests | Independent TestTarget covers remote memory, identity, process exit and dead-target behavior. |
| Hook / VMT lifecycle | Install/remove, trampoline execution, repeated Dispose, Reset and post-dispose rejection pass. |
| Injector lifecycle | Inject, discovery, explicit unload, repeated Dispose, idempotent unload and missing-file failure pass. |
| Ownership / disposal contracts | Session/resource idempotency and target-exit cleanup pass; finalizers do not perform remote restoration. |
| Error / sentinel contracts | Argument, definite failure, normal miss, empty scan, zero-size and architecture/protection checks pass. |
| Runtime ZIP integrity | Manifest v2, file inventory/hashes, archive checksum, exact commit, version, platform and configuration are verified. |
| NuGet package layout | Package layout, XML documentation, commit provenance, `PackageReadmeFile`, and the English + 简体中文 package README sections are verified. |
| Independent NuGet consumer | PackageReference restore/build/run/publish passes; native dependencies reach output; non-x64 consumers fail. |
| Release notes | Rendered from the matching CHANGELOG section and verified manifest/checksum. |
| GitHub Release safety | `release/v*` is dry-run only; `gh release create` is tag-only; no automatic branch deletion. |
| NuGet publish safety | OIDC login and push require a manual run on a published tag with `publish-nuget` enabled; branch validation requires no NuGet account credentials. |

## Publication prerequisites — verify for each future version

- [x] Select `2.1.0` for the backward-compatible Hook / VMT hardening release; update `VERSION`, assembly metadata, CHANGELOG and NuGet package-page metadata together.
- [x] Confirm green Release x64 Build results on the exact release candidate. `main` Build run #338 passed on candidate `f38eb2810b9b7823ba0fd921372e506bdae07ceb`.
- [ ] Verify NuGet account-side setup: Trusted Publishing policy for `CardResearchLab/Libmem.NET` + `release.yml`, and GitHub Actions `NUGET_USER` secret. Their live readiness is not established by source review.
- [x] Create `release/v<version>` from that candidate and verify the complete dry run. `release/v2.1.0` Release run #27 passed on `f38eb2810b9b7823ba0fd921372e506bdae07ceb`; ZIP/NuGet validation and release-note rendering succeeded, while GitHub Release creation, OIDC login, and NuGet push were skipped.
- [x] Review exact-version artifacts and generated notes; confirm ZIP and NuGet share the same source commit. The dry-run artifact `Libmem.NET-windows-x64` is bound to candidate `f38eb2810b9b7823ba0fd921372e506bdae07ceb`, and both runtime/NuGet verification steps enforce that same repository commit.
- [ ] After explicit publication authorization, create the matching `v<version>` tag on that candidate. Creating it triggers GitHub downloads; a preview tag must be marked as a prerelease. It does not push to nuget.org.
- [ ] Verify GitHub downloads/checksum, then manually run Release on the published tag with `publish-nuget` enabled once account setup is ready. Verify the public NuGet page after that run.
- [ ] Restore an independent x64 consumer from nuget.org using that exact version, then run and publish it to verify the public delivery path.

## Optional follow-up

Evaluate Source Link/deterministic source metadata, mixed-mode symbol packaging (`.snupkg`), SBOM and additional provenance when useful. Reintroduce an x86 acceptance matrix only if x86 becomes an active support target. Application state, snapshots, IPC and other product logic remain consumer responsibilities.

See [RELEASES.md](RELEASES.md) and [CONSUMPTION.md](CONSUMPTION.md).
