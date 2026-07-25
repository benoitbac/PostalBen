# What this changes

<!-- One or two sentences. Link the issue if there is one: Closes #123 -->

## Why

<!-- The reasoning, not the diff. If this was a judgement call, say what you traded off. -->

## Verification

<!-- What you actually ran, and what it said. Not "should work". -->

- [ ] `dotnet build PostalBen.csproj` — 0 warnings, 0 errors
- [ ] `godot --headless --import` — no errors
- [ ] `godot --headless --quit-after 60` — exit 0
- [ ] Played it and confirmed the change in-game

## Localization

<!-- Skip only if you touched no player-facing text or audio. -->

- [ ] No user-facing string is hardcoded — everything is a translation key
- [ ] Both `en` and `fr` columns are filled for every new key
- [ ] `tools/vo-directions.csv` has a speaker + direction for every new VO key
- [ ] `pwsh tools/Build-VoSheet.ps1` re-run and `docs/VO_SCRIPT.md` committed

## Design

- [ ] Any obstruction I added has a documented non-violent route through it
- [ ] The pacifist path is still viable end to end
- [ ] Satire stays within GDD section 5 (institutions, not identities)

## Documentation

- [ ] `docs/PROGRESS.md` updated if this completes or starts a roadmap item
- [ ] `dashboard/roadmap.json` updated to match
- [ ] New ADR in `docs/DECISIONS.md` if this locks in a structural choice

## Notes for the reviewer

<!-- Anything you're unsure about, or deliberately left for later. -->
