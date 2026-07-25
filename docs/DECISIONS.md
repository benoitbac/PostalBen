# Architecture decisions

Load-bearing choices and why they were made. Newest first.

---

## ADR-008 — Sub-linear witness scaling for notoriety

**2026-07-25 · Accepted**

Heat from a witnessed incident scales `1 + ln(witnesses)/2`, not linearly.

Linear scaling made crowded areas unplayable — one shove in a busy street jumped straight
to `Hunted`, which removed the player's ability to make a small mistake. Flat scaling made
crowds meaningless. The log curve keeps "where it happened" relevant while keeping a single
loss of temper survivable.

A single kill is 70 heat against a `Hunted` threshold of 100. One kill is `Reported`; two is
a manhunt. That gap is where the game's central choice lives.

---

## ADR-007 — Notoriety decays; escalation is not a one-way door

**2026-07-25 · Accepted**

Heat bleeds at 2.2/s after a 6 s grace period, and `Calm` clears the last-known position.

The design pillar is that violence is always the player's choice. If a single incident
permanently locked the day into a police state, the first mistake would end the run and the
player would reload rather than live with it — which kills the tension the game is built on.
Decay makes hiding and behaving genuine strategies, and makes the *second* temptation
interesting rather than academic.

---

## ADR-006 — Errands are data with completion tokens, not scripts

**2026-07-25 · Accepted**

An errand is an ordered list of stages; each stage names a *token* that the world emits.
Several different interactions may emit the same token.

The alternative — a script per quest — forces every alternate route to be authored
explicitly, and in practice means only the intended route gets built. With tokens, paying
at the till and walking out with the milk both emit `shop.settled`, so shoplifting is a real
route that costs notoriety instead of money, and it required no extra branching.

**Cost:** tokens are stringly-typed and a typo fails silently. Accepted for now because the
token set is small; if it grows past ~40 this becomes a generated constants file.

---

## ADR-005 — TTS placeholders are marked and never counted as recorded

**2026-07-25 · Accepted**

`New-PlaceholderVo.ps1` writes a `.placeholder` marker beside every generated OGG.
`Get-VoCoverage.ps1` excludes marked files; `Normalize-Vo.ps1` refuses to overwrite an
unmarked (real) take.

Without the marker, robot voices would silently inflate recording coverage to 100% and the
dashboard would report a finished VO pass that does not exist. The marker also makes
"replace all placeholders" a safe one-command operation.

---

## ADR-004 — One key drives both the voice clip and the subtitle

**2026-07-25 · Accepted**

`VoiceBank.Say("ben.wake.day1")` looks up `vo/<locale>/ben.wake.day1.ogg` and translates
`ben.wake.day1` for the subtitle.

Separate identifiers for audio and text drift the moment a line is rewritten — the classic
result is a subtitle that does not match what is being said, in one language only. Sharing
the key makes that structurally impossible.

Fallback chain: active locale → English → subtitle-only with a warning. A missing clip is
never fatal, which is what makes a partially-recorded language shippable and lets Ben record
one session at a time.

---

## ADR-003 — Bilingual FR/EN from the first commit, including audio

**2026-07-25 · Accepted**

Retrofitting localization means auditing every string and every `AudioStreamPlayer` in the
codebase, and it is never done completely. Building it in from the start costs almost
nothing: `tr()` instead of a literal, a key instead of a path.

The audio half is the part usually skipped. Doing it up front is what makes per-locale VO a
folder drop rather than an engine change.

---

## ADR-002 — PowerShell 7 is the tooling baseline

**2026-07-25 · Accepted**

Windows PowerShell 5.1 reads `.ps1` files as ANSI, which corrupts any non-ASCII character in
the source — French text in the VO tooling broke the parser outright. It also defaults
`Import-Csv` and `Out-File` to ANSI.

pwsh 7 is UTF-8 end to end and is preinstalled on GitHub Actions Windows runners, so CI and
local runs behave identically.

Scripts additionally avoid non-ASCII *in source* (accented characters are built from char
codes) so they degrade gracefully if run under 5.1 anyway.

---

## ADR-001 — Godot 4 with C#, over Unreal

**2026-07-25 · Accepted**

Considered: Unreal Engine 5, Unity 6, Three.js, Godot 4.

Godot chosen because the entire game — scenes, scripts, assets — is plain text or small
binaries that live in a normal public git repo with no LFS. UE5 would look better sooner
but turns the repo into a multi-gigabyte artifact store, makes CI slow and awkward, and
makes drive-by contribution effectively impossible. C# matches the existing toolchain, and
headless export means CI can actually build and smoke-test the game.

**Accepted cost:** rendering polish is manual work that UE5 gives away via Lumen/Nanite.
Mitigated by committing to a stylized look, which suits the tone and ages better anyway.
