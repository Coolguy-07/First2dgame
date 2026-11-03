# Hollow Grove — a 2D story fight

A small story-driven 2D action level made in **Unity 6000.6.7f1** (URP). Every sprite and
every sound is generated procedurally by the project — no downloaded art or audio.

Ash, a hooded wanderer, crosses **Hollow Grove** to reach **The Dusk Gate**. The grove's
memory-keeper, **The Ember**, guides Ash through eight shades and a final duel with
**Thorne**, the warden of the gate.

## Play it

- **Built game:** `Builds/First2dgame/First2dgame.exe` (Windows x64)
- **Open in Unity:** `Assets/Scenes/Level1.unity` (the only scene in Build Settings)

| Action | Keys |
| --- | --- |
| Move | `A` / `D` or `←` / `→` |
| Jump | `Space` or `W` |
| Attack | `J` or left mouse button |
| Advance dialogue | `E`, `Space`, `Enter` or left click |
| Restart (death / victory) | `R` |

## The level

One continuous level in three acts, built as a single scene:

- **Act I — the waking wood:** three slimes and the first **Stalker**, a two-health hunter
  that winds up and lunges. Jump the lunge and punish the recovery.
- **Act II — the deep grove:** two **Spitters** lob telegraphed arcing bile (jump it or
  close the distance), a second Stalker, and the last slime.
- **Act III — Thorne:** the ruined arch at x ≈ 49.4 marks the arena. **Thorne** has six
  health, a boss bar, and two phases: at three health he roars, turns ember-orange and
  adds a charge to his ground-slam shockwaves. The camera pushes in, the music drops a
  step, and his death runs in slow motion.
- **8 shades** in total. The Dusk Gate stays sealed until every one of them falls.
- **2 checkpoints:** lanterns at x ≈ 18.2 and 38.2. Falling into a pit respawns you at the
  last lantern with **no health loss**; losing all three hearts ends the run.
- **2 pits** (x ≈ 14–17 and 34–37), two optional platforms, foreground grass, drifting mist,
  embers, fireflies, braziers and a parallax dusk sky.

### Conversations

Six dialogue beats, all data-driven (`Speaker|Text`) and letterboxed:

1. **Intro** — Ash wakes at the grove's edge; The Ember introduces the gate.
2. **Pit warning** (x ≈ 13.2) — the grove remembers where you fall.
3. **Midpoint** (x ≈ 30) — the price of cutting shades.
4. **Boss intro** (x ≈ 48.2) — Thorne refuses to yield the gate.
5. **Boss defeat** — Thorne's last words, in slow motion.
6. **Ending** (x ≈ 58.2, after the gate opens) — what lies beyond, and who remembers.

Speakers: **Ash** (the wanderer), **The Ember** (the grove's memory-keeper, not entirely
trustworthy) and **Thorne** (the warden). Monsters and boss attacks freeze while dialogue is
open. Test-only helpers in `GameTest` let the smoke test advance conversations with `E`.

## Tech notes

- `Assets/Editor/Level1Builder.cs` builds the entire scene from code: sky, parallax, ground,
  monsters, dialogue volumes, checkpoints, gate, HUD, boss bar, damage flash, audio,
  particles, arena dressing and the post-processing volume. Rebuild with the bridge command
  `invoke type=Level1Builder method=BuildAll`.
- `Assets/Editor/PixelArt.cs` draws every sprite (Ash, slime, stalker, spitter, Thorne,
  acid glob, shockwave, arch, tiles, portal, glow dot) and imports it as 32-PPU
  point-filtered pixel art.
- `Assets/Scripts/Runtime/AudioManager.cs` + `AudioSynth.cs` synthesize the music, boss
  variation and all SFX at runtime (swing, clash, hurt, jump, land, spit, splat, lunge,
  slam, roar, dialogue blip, checkpoint, portal, victory).
- Combat feedback: hit-stop on every connect, spawn bursts and sword sparks, camera shake,
  red damage flash, pulsing lost hearts, low-health bark.
- **GPU pass (RTX 4050):** HDR camera, URP bloom + vignette + film grain + chromatic
  aberration + Neutral tonemapping (`Assets/Settings/HollowGroveVolume.asset`), SMAA High,
  4× MSAA and 1.25× render scale, HDR glow sprites on the moon, lanterns, braziers and
  portal, plus GPU particle embers, fireflies and brazier sparks.
- The editor bridge (`Assets/Editor/OpenCodeBridge.cs` + `unity-bridge/ubridge.py`) allows
  inspection, play-mode testing, screenshots and builds from the terminal.
  `unity-bridge/smoke.py` runs the full gameplay smoke test: intro dialogue, jump/land,
  walk, sword kill, checkpoint + pit respawn, debug clears, boss bar, Thorne's defeat
  dialogue, 8/8 gate, victory, death and both `R` restarts.

## Team

Built by **Big Pickle** (orchestration, Unity, code, builds) with the free OpenCode models
listed in `TEAM.md` handling story, visual review, game design, dialogue and logic review.
