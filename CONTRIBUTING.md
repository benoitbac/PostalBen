# Contributing to PostalBen

Thanks for looking. This is a small project with strong opinions — reading this first will
save us both a round trip.

## Setup

You need:

- [Godot 4.7.1 — **.NET build**](https://godotengine.org/download) (the standard build won't run C#)
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [PowerShell 7](https://github.com/PowerShell/PowerShell) for the tooling scripts
- [ffmpeg](https://ffmpeg.org/) on PATH, only if you touch audio

```bash
git clone https://github.com/benoitbac/PostalBen.git
cd PostalBen/game
dotnet build PostalBen.csproj
godot --path .
```

Sanity check before you push:

```bash
cd game
dotnet build PostalBen.csproj          # must be 0 warnings — warnings are errors here
godot --headless --import              # must report no errors
godot --headless --quit-after 60       # must exit 0
```

## The rules that actually matter

### 1. No user-facing text in code, ever

Every string a player reads is a key in `game/localization/ui.csv`. Every string a player
hears is a key in `game/localization/vo.csv`. Both columns — `en` **and** `fr` — must be
filled in the same commit.

```csharp
label.Text = "Get milk";                      // rejected
label.Text = Tr("ui.errand.milk.name");       // correct
```

If your French is shaky, write it anyway and say so in the PR — a rough translation that
gets corrected beats an English string that ships.

### 2. The pacifist path is not optional

Every obstruction you add must have a route through it that involves no violence. If you
add a locked door, add a key, a talkative NPC, or a back window. "The player can just kill
him" is not a solution, it is the failure mode this game is about.

### 3. Errands are data

Add stages and completion tokens to `ErrandLog`. Do not write a bespoke script for a quest.
If a stage can only be completed one way, ask whether it should be.

### 4. Tone

Satire aims at institutions, bureaucracy, corporate absurdity, and people who make other
people's days worse on purpose. It does not aim at ethnicity, religion, sexuality, or
disability. PRs that cross that line get closed without much discussion — see
[`docs/GDD.md` §5](docs/GDD.md).

## Style

- C# follows standard .NET conventions. `TreatWarningsAsErrors` is on; keep it that way.
- Comments explain *why*, never *what*. If the code needs a comment to say what it does,
  rename something instead.
- Scene files (`.tscn`) merge badly. Keep changes to one scene per PR where you can.
- PowerShell scripts stay ASCII in source — accented characters are built from char codes.
  See [ADR-002](docs/DECISIONS.md).

## Commits & PRs

Conventional commits:

```
feat(npc): queue behaviour with personal space
fix(audio): music no longer ducks after a subtitle-only line
docs(gdd): clarify notoriety decay
```

One logical change per PR. Fill in the template — especially the localization checklist if
you touched any text or audio.

## Voice-over

Read [`docs/LOCALIZATION.md`](docs/LOCALIZATION.md) before touching anything under
`game/assets/audio/vo/`. Short version:

```powershell
pwsh tools/Build-VoSheet.ps1                    # regenerate the recording script
pwsh tools/New-PlaceholderVo.ps1 -Locale en     # TTS stand-ins so you aren't blocked
pwsh tools/Get-VoCoverage.ps1                   # what's actually recorded
```

Ben records the real lines. Do not commit a voice take that isn't his.

## Architecture decisions

Anything with lasting structural consequence gets an ADR in
[`docs/DECISIONS.md`](docs/DECISIONS.md) — the decision, the alternatives, and the cost you
accepted. Newest first.

## Reporting bugs

Use the issue templates. For gameplay bugs, the most useful thing you can give us is the
notoriety level and whether you were on a pacifist run — a lot of state hangs off both.
