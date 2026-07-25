# Localization — French & English

PostalBen ships fully bilingual: menus, subtitles **and voice-over**. This document is the
contract for keeping it that way.

## The one rule

**No string that a player can see or hear is ever written in code.** Everything is a
translation key. If you find yourself typing user-facing prose into a `.cs` file, stop.

## Where things live

| File | Holds |
|---|---|
| `game/localization/ui.csv` | Menus, HUD, prompts, errand names |
| `game/localization/vo.csv` | Every spoken line — doubles as the subtitle source |
| `tools/vo-directions.csv` | Speaker + performance notes per line (not shipped) |
| `game/assets/audio/vo/en/` | English voice clips, one OGG per key |
| `game/assets/audio/vo/fr/` | French voice clips, same key set |

CSV format is Godot's: first column `keys`, one column per locale.

```csv
keys,en,fr
ben.wake.day1,Another beautiful day in the neighbourhood.,Encore une belle journée dans le quartier.
```

Godot compiles these into `.translation` resources on import. Never edit those.

## How a line resolves at runtime

The **same key** drives the audio file and the subtitle. They cannot drift apart.

```
VoiceBank.Say("ben.wake.day1")
      |
      +-- subtitle: TranslationServer.Translate(key)     -> always fires
      |
      +-- audio: assets/audio/vo/<locale>/<key>.ogg      -> active language
                 |
                 +-- missing? assets/audio/vo/en/<key>.ogg   -> English fallback
                                |
                                +-- missing? subtitle only, warning in log
```

This three-step fallback is the reason a half-recorded language is still shippable. French
subtitles over an English take is a perfectly playable state, and it is what the game does
today.

## Adding a line

1. Add the row to `game/localization/vo.csv` with **both** languages filled in.
2. Add speaker and direction to `tools/vo-directions.csv` for the same key.
3. Regenerate the recording script:
   ```powershell
   pwsh tools/Build-VoSheet.ps1
   ```
4. Call it: `VoiceBank.Say("your.new.key")`.

The line works immediately as a subtitle. Audio can land months later.

## Adding a language

1. Add a column to both CSVs (e.g. `es`).
2. Register it in `LocaleManager.Supported`.
3. Add the `.translation` paths to `internationalization/locale/translations` in `project.godot`.
4. Create `game/assets/audio/vo/es/`.

`LocaleManager.Normalize` maps regional variants onto the base language, so `fr_BE`,
`fr-CA` and `fr` all resolve to the French build.

## Voice-over pipeline

```
audio-raw/fr/ben.wake.day1.wav        <- raw take, git-ignored
        |
        |  pwsh tools/Normalize-Vo.ps1 -Locale fr
        |    - trim silence (keeps 120 ms room tone)
        |    - two-pass EBU R128 -> -16 LUFS / -1.5 dBTP
        |    - mono, 48 kHz, OGG Vorbis
        v
game/assets/audio/vo/fr/ben.wake.day1.ogg
```

Two-pass loudness normalization is what lets a take recorded in June sit next to one from
December without hand-riding gain.

### Recording

Read [`VO_SCRIPT.md`](VO_SCRIPT.md) — generated, includes every line with its direction.
Mono, 48 kHz, 24-bit WAV. One file per key, named exactly the key. Leave ~1 s of silence at
head and tail; the normalizer needs the room tone to trim against.

Keep the mic position identical across a session. Lines from different sessions play
back-to-back in-game and tone shifts are audible.

### Placeholders

```powershell
pwsh tools/New-PlaceholderVo.ps1 -Locale en
```

Generates SAPI text-to-speech stand-ins so gameplay and timing can be built before anyone
records. Each one drops a `.placeholder` marker beside the OGG.

Placeholders **never count as recorded** — `Get-VoCoverage.ps1` excludes them and the
dashboard reports them separately. `Normalize-Vo.ps1` overwrites a placeholder the moment a
real take lands on the same key, and it will never overwrite a real take.

## Coverage

```powershell
pwsh tools/Get-VoCoverage.ps1
```

Compares declared keys against files on disk, per language and per speaker, and writes
`dashboard/vo-coverage.json`. It also flags **orphans** — audio files with no matching key,
which almost always means a typo in a filename.

This runs in CI, so the dashboard shows real recording progress rather than a number
somebody remembered to update.

## Writing notes

- **English is the authoring language.** It is always complete; it is the fallback.
- French is a **translation, not a transcription**. Match the rhythm and the joke, not the
  word order. `ben.support.hold` lands on the number in both languages because that is
  where the joke is — not because the sentence structure matches.
- Subtitles are sized by reading speed (14 chars/sec, clamped 1.4–7 s). French runs ~15–20%
  longer than English; the clamp absorbs it, but keep long lines tight.
- Speaker colours are set in `SubtitleLayer.cs`. New speaker types need an entry there or
  they fall back to white.
