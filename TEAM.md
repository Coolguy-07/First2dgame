# Game dev team — free models (all $0/token)

Orchestrator: **Big Pickle** (me) — owns Unity, files, builds, and final decisions.

| Role | Model | Use for |
| --- | --- | --- |
| Narrative & story design | `opencode/flint-40b-free` | plot, characters, arcs, themes, quest structure |
| Visual direction & screenshot review | `opencode/nemotron-3.5-super-vl-free` | art style, palette, UI look, reviewing captured frames |
| Game design & systems critique | `opencode/beam-free` | mechanics, pacing, loop, scope control |
| Dialogue & scene writing | `opencode/mimo-v2.6-flash-free` | barks, conversations, branching lines, flavor text |
| Narrative systems & logic review | `opencode/deepseek-v4.1-flash-free` | flags/state, branching correctness, code review |

## Protocol
1. I brief each model with only what it needs (fresh context per task).
2. Models advise in parallel; I synthesize and do all file/Unity edits myself.
3. Content lives in the Unity project; dialogue/flags are data-driven so writers can iterate.
4. Reuse a session ID to keep that model's memory of the project.
5. Reasoning effort can be raised per call with a variant suffix, e.g. `opencode/flint-40b-free#high`.

## Cost
Every model above (including Big Pickle) is free: input/output/cache = $0.

## Shipped — Level 1 "Hollow Grove"

- Build: `Builds/First2dgame/First2dgame.exe` (Windows x64, 2026-11-03), 0 errors.
- Scene builder: `Level1Builder.BuildAll`; sprites: `PixelArt`; tests: `smoke.py`.
- Final cast: 3 slimes, 2 stalkers, 2 spitters, boss Thorne (6 HP, 2 phases,
  slam shockwaves, phase-2 charge). Gate opens at 8/8.
- Six dialogue beats (intro, pit, midpoint, boss intro, boss defeat, ending) with
  Ash / The Ember / Thorne; `Speaker|Text` data, monsters frozen during talk.
- Final QA (all pass): movement/jump/land, walk, sword kill, checkpoint + pit respawn
  (no HP loss), boss engagement + bar, Thorne defeat dialogue, 8/8 gate, victory,
  death screen, and `R` restart from both endings.
- GPU pass: bloom soft, particles add depth, HUD legible, no artifacts; moon bloom
  softened after review. Player verified launching on the RTX 4050 (D3D12, no
  runtime exceptions).
- Fixed during QA: the player sprite shared the Rigidbody2D's transform, so the idle
  bob pinned the body to spawn every frame. The sprite now lives on a `Body` child.
- Fixed during final QA: the death veil was drawn above the message/prompt, leaving a
  blank black screen. Messages and the `R` prompt now render above the fade, and the
  veil settles at 82% instead of full black.
