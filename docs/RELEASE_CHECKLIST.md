# Libmem.NET Release Checklist

Scope: Windows x64 / .NET 8. Apply this checklist to the exact candidate commit for each new release. Repository checks and account-side publication setup are separate evidence.

Published GitHub/nuget.org stable: `2.1.1`; previous stable: `2.1.0`. Numeric assembly/file versions are `2.1.1.0`. This patch preserves the 2.1.0 Public API baseline, fixes VMT bad-address-sentinel validation and untracked-Unhook page protection, and does not expand official x86 support.

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

- [x] Select `2.1.1` for the backward-compatible maintenance fix; update `VERSION`, assembly metadata, CHANGELOG and NuGet package-page metadata together.
- [x] Confirm green Release x64 Build results on the exact 2.1.1 candidate, including Hook/VMT regression coverage and package verification.
- [ ] Verify NuGet account-side setup: Trusted Publishing policy for `CardResearchLab/Libmem.NET` + `release.yml`, and GitHub Actions `NUGET_USER` secret.
- [x] Create `release/v2.1.1` from the accepted candidate and verify the complete dry run.
- [ ] Review exact-version ZIP/NuGet artifacts and generated notes; confirm both are bound to the same source commit.
- [x] After explicit publication authorization, create the matching `v2.1.1` tag. Tag creation publishes GitHub downloads only.
- [ ] Verify GitHub downloads/checksum, then manually run Release on the published tag with `publish-nuget` enabled.
- [x] Restore an independent x64 consumer from nuget.org using exact version `2.1.1`, then run and publish it to verify the public delivery path.

Historical post-release evidence: the first clean nuget.org-only restore of exact `2.1.0` failed with NU1102 before the NuGet publication completed. 2.1.0 was subsequently published. Libmem.NET `2.1.1` is published and its exact-version public nuget.org restore/build/run/publish smoke test now passes.

The completed GitHub 2.1.0 acceptance evidence remains in repository history and the v2.1.0 release metadata.

## Optional follow-up

Evaluate Source Link/deterministic source metadata, mixed-mode symbol packaging (`.snupkg`), SBOM and additional provenance when useful. Reintroduce an x86 acceptance matrix only if x86 becomes an active support target. Application state, snapshots, IPC and other product logic remain consumer responsibilities.

See [RELEASES.md](RELEASES.md) and [CONSUMPTION.md](CONSUMPTION.md).
