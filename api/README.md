# Public API baseline

`LibmemCli.PublicApi.txt` is the committed public API contract shared by the current Windows x86/x64 LibmemCli builds.

The baseline is generated from `src/LibmemCli.h` by `eng/check-public-api.py` and is checked by `tests/check_sources.py`.

## Normal development

Do not edit the baseline merely to make CI green. If a change is intended to be internal, keep the public API unchanged.

## Intentional public API change

Regenerate the baseline explicitly:

```powershell
python .\eng\check-public-api.py --write
```

Then review the baseline diff together with the implementation change, document the public change in `CHANGELOG.md`, and update the project version when appropriate.

The project is still pre-1.0. Breaking changes remain possible, but they must be deliberate and visible in review.
