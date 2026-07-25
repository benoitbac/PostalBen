# PostalBen — Game Design Document

> Living document. Last substantive revision: 2026-07-25.
> Decisions with lasting architectural consequences are logged in [`DECISIONS.md`](DECISIONS.md).

## 1. The pitch

You have a list. The list is short. Finishing it should take twenty minutes.

PostalBen is a first-person open-world sandbox built around that gap — between how long a
day's errands *should* take and how long they actually take once other people are involved.
The player is Ben, an ordinary man in an ordinary town, working through an ordinary week.

The game's entire tension comes from one property: **it never requires violence, and never
prevents it.** Every errand has a patient solution. Every errand also has a fast one.

## 2. The core loop

```
Wake up  ->  read the list  ->  leave the house
                                     |
              +----------------------+----------------------+
              |                                             |
         do the errand                              the world obstructs
       (queue, pay, wait, talk)                  (bureaucracy, crowds, jerks)
              |                                             |
              +----------------------+----------------------+
                                     |
                        player chooses: patience or escalation
                                     |
                      notoriety rises <-> notoriety decays
                                     |
                        list cleared -> go home -> sleep
                                     |
                              day report / next day
```

A day is roughly 25 real minutes (`GameState.TimeScale = 40`, 08:00 to 22:00). The clock is
a soft pressure, not a fail state: shops close, the bank shuts at five, and an unfinished
list rolls into tomorrow — longer.

## 3. Pillars

### 3.1 The mundane is the content
The interesting thing is never the objective, it is the friction around it. Design effort
goes into queues, forms, opening hours and NPC stubbornness — not into set pieces.

### 3.2 Escalation is always the player's choice
The game must never corner the player into violence. Every obstruction has at least one
patient route. When the player escalates, that has to read as *their* decision, which is
what makes it land.

### 3.3 The pacifist run is the real scoreboard
`GameState.PacifistRun` is tracked across the whole week and shown in every day report. A
no-kill week is the hardest and most respected way to finish the game.

### 3.4 Consequences decay
Notoriety bleeds off over time ([`NotorietySystem`](../game/scripts/systems/NotorietySystem.cs)).
Hiding, walking away and behaving are real strategies. A player who loses their temper once
can walk it back — that is what makes the second temptation interesting.

## 4. Systems

### 4.1 Errands — [`ErrandLog.cs`](../game/scripts/systems/ErrandLog.cs)

Errands are **data, not scripts**. An errand is an ordered list of stages; each stage waits
on a *completion token* emitted by the world.

```
Errand "milk"
  stage 0  <- token "shop.entered"
  stage 1  <- token "milk.acquired"
  stage 2  <- token "shop.settled"
```

Multiple interactions can emit the same token. Paying at the till and walking out with the
milk both emit `shop.settled` — so shoplifting is a first-class route that costs notoriety
instead of money, with no branching quest script.

Names and hints are translation keys, never literal text.

### 4.2 Notoriety — [`NotorietySystem.cs`](../game/scripts/systems/NotorietySystem.cs)

Four levels: `Calm -> Noticed -> Reported -> Hunted`.

Heat is added by *witnessed* incidents only. Witness count scales sub-linearly
(`1 + ln(n)/2`) so a busy street matters more than an empty one without a crowd of thirty
being thirty times a passer-by. Heat decays at 2.2/s after a 6 s grace period.

| Incident | Heat |
|---|---|
| Rude | 4 |
| Trespass | 8 |
| Vandalism | 14 |
| Weapon drawn | 22 |
| Assault | 30 |
| Gunshot fired | 38 |
| Kill | 70 |

A single kill (70) lands in `Reported` but not `Hunted` — one loss of temper is survivable,
two is a manhunt. That threshold is deliberate.

### 4.3 Movement — [`BenController.cs`](../game/scripts/player/BenController.cs)

First-person, deliberately weighty. Ben is a man doing errands, not a soldier. Sprint is
stamina-limited with a lockout below 18% so chases have a rhythm rather than being a
hold-shift contest.

### 4.4 Voice & subtitles — [`VoiceBank.cs`](../game/scripts/audio/VoiceBank.cs)

One key drives both the audio file and the subtitle translation, so text and speech cannot
drift apart. A missing clip is never fatal: it falls back to English, then to
subtitle-only. See [`LOCALIZATION.md`](LOCALIZATION.md).

Music ducks 12 dB under any active voice line ([`AudioDirector.cs`](../game/scripts/audio/AudioDirector.cs)),
fast attack and slow release so dialogue is never buried and the mix never pumps.

## 5. Tone

Absurdist and deadpan. The comedy is structural — the *situation* is funny, the delivery
is flat. Ben is not a wisecracker; he is a tired man narrating his own week.

**Targets:** bureaucracy, corporate customer service, HOA authoritarianism, influencer
culture, terms-and-conditions theatre, institutions that waste people's time on purpose.

**Not targets:** ethnicity, religion, sexuality, disability. The original genre aimed at
those and it aged into a liability, not an edge. Punching at institutions is funnier and
does not date.

Violence is bloody and unglamorous. `ben.combat.first_blood` and `ben.combat.kill_regret`
are directed to be played straight, with no comic timing — the tonal pivot of the game is
the moment the joke stops.

## 6. Content & accessibility

- Gore level: Full / Reduced / Off (`ui.settings.gore.*`)
- Subtitles on by default, size adjustable, speaker-coloured
- Separate Master / Voice / SFX / Music sliders
- Full FR + EN, text and audio
- Invertible vertical look, adjustable sensitivity

## 7. Scope

### Sprint 1 — Foundations *(current)*
Engine setup, localization pipeline, VO tooling, core systems (errands, notoriety, clock,
save/load), player controller, dashboard, public repo.

### Sprint 2 — The district
Blockout of one walkable neighbourhood: house, shop, bank, park, streets. Interaction
system, doors, pickups. Day 1 completable start to finish.

### Sprint 3 — People
NPC AI: daily schedules, queues, personal space, reaction states (annoyed, alarmed,
fleeing). Crowd witness reporting into notoriety.

### Sprint 4 — Consequences
Police AI, pursuit, arrest, the `Hunted` state. Melee and firearms. Gore system.

### Sprint 5 — Ben
Player likeness (stylized caricature from photos), rig, animations, first-person hands,
mirror reflections.

### Sprint 6 — The week
Days 2–5, escalating lists. Day report screen. Full VO recording pass in both languages.

## 8. Non-goals

- Multiplayer
- Photorealism — stylized is cheaper, ages better, and suits the tone
- Any asset, mechanic name, character or line lifted from an existing commercial game
