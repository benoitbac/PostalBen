# PostalBen — Progress & status

> **Living document.** Updated at every meaningful step.
> Machine-readable twin: [`dashboard/roadmap.json`](../dashboard/roadmap.json), rendered at
> the [live dashboard](https://benoitbac.github.io/PostalBen/).
> Last update: **2026-07-25**.

## Where we are

**Sprint 1 — Foundations.** Engine, localization, audio and the core simulation systems.
The project builds clean, boots headless, and loads Day 1 with both errands registered and
subtitles firing in both languages.

## Sprint 1 — Foundations

| # | Component | File(s) | State |
|---|---|---|---|
| 1 | Godot 4.7.1 .NET project | `game/project.godot`, `game/PostalBen.csproj` | ✅ Done — Forward+, .NET 9, warnings-as-errors, 8 physics layers, full input map (KB/M + gamepad) |
| 2 | Locale manager | `game/scripts/systems/LocaleManager.cs` | ✅ Done — FR/EN, OS detection, regional-variant normalization (`fr_BE` → `fr`), persisted, runtime switch |
| 3 | Localized voice bank | `game/scripts/audio/VoiceBank.cs` | ✅ Done — per-locale clip resolution, EN fallback, subtitle-only third fallback, 6-voice pool with stealing |
| 4 | Audio mixer | `game/scripts/audio/AudioDirector.cs`, `assets/audio/default_bus_layout.tres` | ✅ Done — Master/Voice/Sfx/Music/Ambience, music ducks −12 dB under dialogue (fast attack, slow release) |
| 5 | Subtitles | `game/scripts/ui/SubtitleLayer.cs` | ✅ Done — speaker-coloured, reading-speed timing, max 3 on screen, drops untranslated keys rather than showing them |
| 6 | Translation data | `game/localization/{ui,vo}.csv` | ✅ Done — 78 UI keys + 53 VO lines, FR & EN complete |
| 7 | Errand system | `game/scripts/systems/ErrandLog.cs` | ✅ Done — data-driven staged errands, token-based completion (multiple routes per stage), no per-quest scripts |
| 8 | Notoriety system | `game/scripts/systems/NotorietySystem.cs` | ✅ Done — 4 levels, witnessed-only heat, sub-linear witness scaling, time decay, last-known-position |
| 9 | Day clock & save | `game/scripts/systems/GameState.cs` | ✅ Done — 08:00→22:00 at 40×, hour signals, pacifist tracking (day + run), ConfigFile save slots |
| 10 | Player controller | `game/scripts/player/BenController.cs` | ✅ Done — FP movement, sprint w/ stamina lockout, crouch with ceiling check, damage/death wired to VO |
| 11 | Boot & Day 1 scenes | `game/scenes/{Boot,Ben,Day1}.tscn` | ✅ Done — boots headless, exit 0, both errands registered |
| 12 | VO recording pipeline | `tools/*.ps1` | ✅ Done — sheet generator, EBU R128 normalizer, TTS placeholders, coverage reporter |
| 13 | Dashboard | `dashboard/` | 🟡 In progress |
| 14 | Public repo + CI | `.github/` | 🟡 In progress |

## Voice-over recording

| Language | Recorded | Total | Notes |
|---|---|---|---|
| English | 0 | 53 | Awaiting Ben's session — subtitles carry all lines meanwhile |
| French | 0 | 53 | Same |

Live numbers: `pwsh tools/Get-VoCoverage.ps1` → `dashboard/vo-coverage.json`.

## Backlog

### Sprint 2 — The district
- [ ] Blockout: house, shop, bank, park, connecting streets
- [ ] Interaction system (raycast + prompt, `ui.hud.interact_prompt`)
- [ ] Doors, pickups, tills, queue volumes
- [ ] Wire Day 1 completion tokens to real world objects
- [ ] Errand log UI (`J` key) with markers

### Sprint 3 — People
- [ ] NPC base: navmesh, daily schedule, personal space
- [ ] Queue behaviour — the central obstruction mechanic
- [ ] Reaction states: neutral → annoyed → alarmed → fleeing
- [ ] Witness reporting into `NotorietySystem`
- [ ] Ambient bark system using the `npc.*` VO keys

### Sprint 4 — Consequences
- [ ] Police AI: dispatch to last known position, search, pursue, arrest
- [ ] `Hunted` state behaviour
- [ ] Melee combat
- [ ] Firearms
- [ ] Gore system with Full/Reduced/Off setting
- [ ] Verify Day 1 is completable with zero kills *and* zero notoriety

### Sprint 5 — Ben
- [ ] Photo reference session
- [ ] Stylized head sculpt + retopo
- [ ] Body model, rig, animation set
- [ ] First-person hands
- [ ] Replace placeholder capsule

### Sprint 6 — The week
- [ ] Days 2–5 with escalating lists
- [ ] Day report screen (`ui.stats.*`)
- [ ] Main menu + settings UI (language, audio, gore, controls)
- [ ] Full VO recording pass, FR + EN
- [ ] Export presets, release workflow

## Verified

Recorded because "it builds" and "it runs" are different claims:

- `dotnet build PostalBen.csproj` — **0 warnings, 0 errors** (warnings-as-errors on)
- `godot --headless --import` — **no errors**, both CSVs compile to `.translation`
- `godot --headless --quit-after 60` — **exit 0**; locale resolves, Boot loads Day 1, both
  errands register, VO falls back to subtitle-only as designed
- `tools/Build-VoSheet.ps1` — 53 lines × 2 locales, correct UTF-8 output
- `tools/Get-VoCoverage.ps1` — runs under pwsh 7, writes `vo-coverage.json`
