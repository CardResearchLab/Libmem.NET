# Libmem.NET v1.0 Release Readiness Checklist

> Scope: Windows x64 / .NET 8.  
> Status values: **PASS**, **BLOCKED**, **TODO**, **N/A**.

## Release summary

| Area | Status | Evidence / remaining work |
|---|---|---|
| Public API freeze | **PASS** | `api/LibmemCli.PublicApi.txt` is enforced by `eng/check-public-api.py` from `tests/check_sources.py`. |
| Version metadata | **PASS** | Root `VERSION` is `1.0.0`; assembly metadata is contract-checked against it. |
| Windows x64 build | **PASS** | Default Build workflow compiles and packages Release x64. |
| Runtime smoke tests | **PASS** | Process/thread/module/symbol/memory/scan/assembly/disassembly/code-length coverage is exercised on x64. |
| External-process tests | **PASS** | Repository-owned TestTarget covers attach, remote read/write, allocation/protection/free, scan, segment lookup, process exit, stale identity, and dead-target behavior. |
| Hook / VMT lifecycle | **PASS** | Dedicated runtime workflow covers install/remove, trampoline execution, repeated Dispose, Reset, and post-dispose rejection. |
| Injector lifecycle | **PASS** | Dedicated runtime workflow covers inject, discovery, explicit unload, repeated Dispose, idempotent unload, and missing-file failure. |
| Ownership / disposal contracts | **PASS** | Session/resource idempotency and target-exit cleanup are runtime-tested; finalizers do not perform unsafe remote restoration. |
| Error / sentinel contracts | **PASS** | Null/invalid argument, definite native failure, normal miss, empty scan, zero-size, and architecture/protection validation are frozen in source contracts and runtime tests. |
| Runtime ZIP integrity | **PASS** | Manifest v2, per-file SHA-256, archive checksum, repository commit, platform, configuration and version are verified. |
| NuGet package layout | **PASS** | `Libmem.NET` package layout/provenance is verified before consumer tests. |
| Independent NuGet consumer | **PASS** | PackageReference restore/build/run/publish is tested; native assets and `Ijwhost.dll` are verified; non-x64 consumers are rejected. |
| Release notes | **PASS** | Release notes are rendered from the matching CHANGELOG section plus verified manifest/checksum data. |
| GitHub Release safety | **PASS** | `release/v*` is dry-run only; real GitHub Release creation is tag-only. |
| NuGet publish safety | **PASS** | Real nuget.org login/push is tag-only and uses Trusted Publishing (OIDC), not a long-lived API key. |
| NuGet account-side setup | **BLOCKED** | Requires access to nuget.org: create Trusted Publishing policy for `HearthstoneModding/Libmem.NET` + `release.yml`, then add GitHub Actions secret `NUGET_USER`. |
| First live `v1.0.0` publication | **BLOCKED** | Cannot perform until the nuget.org account is accessible and Trusted Publishing is configured. |

## Required before creating the v1.0.0 tag

1. **PASS** — `VERSION` is exactly `1.0.0`.
2. **PASS** — `CHANGELOG.md` contains a `1.0.0` section.
3. **PASS** — all PR CI gates are green on the release commit.
4. **PASS** — runtime ZIP and NuGet package are generated from the same commit and exact version.
5. **PASS** — release branches cannot publish externally; they only validate the release candidate.
6. **BLOCKED** — configure nuget.org Trusted Publishing.
7. **BLOCKED** — add `NUGET_USER` to GitHub Actions secrets.
8. **TODO** — after account recovery, create a `release/v1.0.0` branch once to exercise the complete dry-run path on the final release commit.
9. **TODO** — if the dry-run is green, create the `v1.0.0` tag to perform the first real GitHub Release + nuget.org publication.
10. **TODO** — after publication, verify the public NuGet page, install with `dotnet add package Libmem.NET --version 1.0.0`, and run the independent sample/consumer once against nuget.org rather than the local feed.

## Non-blocking post-v1.0 improvements

These are useful hardening items but are **not v1.0 blockers**:

- **TODO** — evaluate Source Link / deterministic-source metadata beyond the current repository commit provenance.
- **TODO** — evaluate a symbol package (`.snupkg`) if it materially improves mixed-mode debugging.
- **TODO** — evaluate SBOM / additional supply-chain provenance.
- **TODO** — restore an x86 acceptance matrix only when x86 becomes an active support target again.
- **N/A** — Snapshot, Entity, GameState, IPC, Hearthstone/version logic and bot state do not belong in Libmem.NET.

## Release boundary

Libmem.NET v1.0 is considered code-ready when all **PASS** items above remain green. The only current external blocker is nuget.org account access / Trusted Publishing setup; it does not block continued library development or use through source/reusable-build/runtime ZIP paths.
