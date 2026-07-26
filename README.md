<div align="center">

# PostalBen

**An open-world errand simulator where the list is mundane and the week is not.**

*Un bac à sable en monde ouvert où la liste de courses est banale — et la semaine ne l'est pas.*

[![CI](https://github.com/benoitbac/PostalBen/actions/workflows/ci.yml/badge.svg)](https://github.com/benoitbac/PostalBen/actions/workflows/ci.yml)
[![Dashboard](https://img.shields.io/badge/dashboard-live-6f42c1)](https://benoitbac.github.io/PostalBen/)
[![Godot](https://img.shields.io/badge/Godot-4.7.1%20.NET-478cbf?logo=godotengine&logoColor=white)](https://godotengine.org)
[![.NET](https://img.shields.io/badge/.NET-9.0-512bd4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Languages](https://img.shields.io/badge/langues-FR%20%7C%20EN-blue)](docs/LOCALIZATION.md)
[![License](https://img.shields.io/badge/code-MIT-green)](LICENSE)

[English](#english) · [Français](#français)

</div>

---

## English

### What this is

PostalBen is an original open-world sandbox in the spirit of the early-2000s errand-'em-up:
you are handed a list of tedious daily chores, and the only real question is how much of
your composure survives finishing it.

Each in-game day gives you a short list — *get milk*, *cash the paycheck*. Every task is
completable in under ten minutes by a patient person. The world is built to test whether
you are one. Queues, forms, HOA inspectors, people filming you. The game never forces
escalation; it just leaves the option lying around.

**The full week is finishable without killing anyone.** That is not an achievement tucked
in a menu — it is the design axis the whole game is scored on.

### Status

Pre-alpha. The engine foundations, localization pipeline and Day 1 skeleton are in place;
the district, NPC AI and combat are in progress. Track it on the
**[live dashboard](https://benoitbac.github.io/PostalBen/)**, or read
[`docs/PROGRESS.md`](docs/PROGRESS.md).

### Playing it

```powershell
git clone https://github.com/benoitbac/PostalBen.git
cd PostalBen
pwsh tools/Play.ps1
```

That builds the C# assembly, imports assets on first run, and launches. Useful flags:
`-Locale fr` to force French, `-Tests` to run the invariant suite, `-Editor` to open the
Godot editor.

| | |
|---|---|
| Move | `W` `A` `S` `D` |
| Look | Mouse |
| Sprint / crouch | `Shift` / `C` |
| Interact | `E` |
| Errand list | `J` |
| Release mouse | `Esc` |

**Day 1:** get milk, cash the paycheck. The shop is south-east, the bank north-west. Both
errands are completable without hurting anyone — and the milk has a second solution if you
would rather not pay for it.

Requires [Godot 4.7.1 — **.NET build**](https://godotengine.org/download) (the standard
build cannot run C#) and the [.NET 9 SDK](https://dotnet.microsoft.com/download). If Godot
isn't on your `PATH`, set `$env:GODOT` to the executable and `Play.ps1` will find it.

### Languages

Fully bilingual **French and English** — menus, subtitles, and voice-over. Nothing is
hardcoded: every string is a translation key, and every voice line resolves per-locale
with an English fallback so a partially-recorded language is always playable.

See [`docs/LOCALIZATION.md`](docs/LOCALIZATION.md) for how to add a line or a language.

### Documentation

| Document | What's in it |
|---|---|
| [`docs/GDD.md`](docs/GDD.md) | Game design document — the loop, the systems, the tone |
| [`docs/PROGRESS.md`](docs/PROGRESS.md) | Living status: what's built, what's next |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | Architecture decision log, with the reasoning |
| [`docs/LOCALIZATION.md`](docs/LOCALIZATION.md) | How FR/EN text and audio work |
| [`docs/VO_SCRIPT.md`](docs/VO_SCRIPT.md) | Generated voice-over recording script |

### Contributing

Read [`CONTRIBUTING.md`](CONTRIBUTING.md). Issues labelled
[`good first issue`](https://github.com/benoitbac/PostalBen/labels/good%20first%20issue)
are a reasonable place to start.

### Legal

PostalBen is an **original work and a parody**. It contains no code, assets, audio, or text
from *Postal 2* or any other Running With Scissors product, and is not affiliated with or
endorsed by them. Code is MIT; original assets are CC BY-NC 4.0. See [`LICENSE`](LICENSE).

---

## Français

### Le concept

PostalBen est un bac à sable en monde ouvert, dans l'esprit des simulateurs de corvées du
début des années 2000 : on te file une liste de tâches banales, et la seule vraie question
c'est combien de ton sang-froid survit à la journée.

Chaque journée in-game vient avec une petite liste — *acheter du lait*, *encaisser la paie*.
Chaque tâche se termine en moins de dix minutes si tu es patient. Le monde est construit
pour vérifier si tu l'es. Files d'attente, formulaires, syndic de copropriété, gens qui te
filment. Le jeu ne force jamais l'escalade ; il laisse juste l'option traîner par terre.

**La semaine entière est finissable sans tuer personne.** Ce n'est pas un succès planqué
dans un menu — c'est l'axe de notation de tout le jeu.

### État

Pré-alpha. Les fondations moteur, le pipeline de localisation et le squelette du Jour 1
sont en place ; le quartier, l'IA des PNJ et le combat sont en cours. Suivi sur le
**[dashboard live](https://benoitbac.github.io/PostalBen/)** ou dans
[`docs/PROGRESS.md`](docs/PROGRESS.md).

### Lancer le jeu

```powershell
git clone https://github.com/benoitbac/PostalBen.git
cd PostalBen
pwsh tools/Play.ps1
```

Le script compile le C#, importe les assets au premier lancement, et démarre. Options
utiles : `-Locale fr` pour forcer le français, `-Tests` pour la suite d'invariants,
`-Editor` pour ouvrir l'éditeur Godot.

| | |
|---|---|
| Se déplacer | `W` `A` `S` `D` |
| Regarder | Souris |
| Courir / s'accroupir | `Shift` / `C` |
| Interagir | `E` |
| Liste de courses | `J` |
| Libérer la souris | `Échap` |

**Jour 1 :** acheter du lait, encaisser la paie. Le magasin est au sud-est, la banque au
nord-ouest. Les deux courses se terminent sans faire de mal à personne — et le lait a une
seconde solution si tu préfères ne pas le payer.

Nécessite [Godot 4.7.1 — **build .NET**](https://godotengine.org/download) (le build
standard ne peut pas exécuter du C#) et le [SDK .NET 9](https://dotnet.microsoft.com/download).
Si Godot n'est pas dans le `PATH`, définis `$env:GODOT` vers l'exécutable et `Play.ps1` le
trouvera.

### Langues

Entièrement bilingue **français et anglais** — menus, sous-titres et voix. Rien n'est en
dur : chaque texte est une clé de traduction, et chaque réplique se résout par langue avec
repli sur l'anglais, pour qu'une langue partiellement enregistrée reste toujours jouable.

Voir [`docs/LOCALIZATION.md`](docs/LOCALIZATION.md) pour ajouter une réplique ou une langue.

### Contribuer

Lis [`CONTRIBUTING.md`](CONTRIBUTING.md). Les issues étiquetées
[`good first issue`](https://github.com/benoitbac/PostalBen/labels/good%20first%20issue)
sont un bon point de départ.

### Mentions légales

PostalBen est une **œuvre originale et une parodie**. Le projet ne contient aucun code,
asset, audio ou texte issu de *Postal 2* ni d'aucun autre produit Running With Scissors,
et n'est ni affilié ni approuvé par eux. Le code est sous MIT ; les assets originaux sous
CC BY-NC 4.0. Voir [`LICENSE`](LICENSE).
