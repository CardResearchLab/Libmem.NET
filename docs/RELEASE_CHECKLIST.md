# Libmem.NET Release Checklist

Scope: Windows x64 / .NET 8. Apply this checklist to the exact candidate commit for each new release. Repository checks and account-side publication setup are separate evidence.

Historical `v1.0.0` was published on 2026-09-30 under the `LibmemCli` identity. Do not recreate that tag or replace its assets. Current main uses `Libmem.NET` but still carries `VERSION` = `1.0.0`; select and commit the next version before a release dry run.

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
| NuGet package layout | Package layout, XML documentation and commit provenance are verified. |
| Independent NuGet consumer | PackageReference restore/build/run/publish passes; native dependencies reach output; non-x64 consumers fail. |
| Release notes | Rendered from the matching CHANGELOG section and verified manifest/checksum. |
| GitHub Release safety | `release/v*` is dry-run only; `gh release create` is tag-only; no automatic branch deletion. |
| NuGet publish safety | OIDC login and push are tag-only; branch validation requires no NuGet account credentials. |

## Publication prerequisites — verify for the chosen version

- [ ] Select a version that accounts for the breaking `LibmemCli` → `Libmem.NET` identity migration; update `VERSION`, assembly metadata and CHANGELOG together.
- [ ] Confirm green Release x64 Build results on the exact release candidate.
- [ ] Verify NuGet account-side setup: Trusted Publishing policy for `HearthstoneModding/Libmem.NET` + `release.yml`, and GitHub Actions `NUGET_USER` secret. Their live readiness is not established by source review.
- [ ] Create `release/v<version>` from that candidate and verify the complete dry run. It must build and validate ZIP/NuGet and render notes, with all three external publication steps skipped.
- [ ] Review exact-version artifacts and generated notes; confirm ZIP and NuGet share the same source commit.
- [ ] After explicit publication authorization, create the matching `v<version>` tag on that candidate. Creating it triggers external publication.
- [ ] Verify the GitHub Release files/checksum and public NuGet page after publication.
- [ ] Restore an independent x64 consumer from nuget.org using that exact version, then run and publish it to verify the public delivery path.

## Optional follow-up

Evaluate Source Link/deterministic source metadata, mixed-mode symbol packaging (`.snupkg`), SBOM and additional provenance when useful. Reintroduce an x86 acceptance matrix only if x86 becomes an active support target. Application state, snapshots, IPC and other product logic remain consumer responsibilities.

See [RELEASES.md](RELEASES.md) and [CONSUMPTION.md](CONSUMPTION.md).
