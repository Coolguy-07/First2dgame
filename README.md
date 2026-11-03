# Hollow Grove — First Level

A small story-driven 2D action level made in **Unity 6000.6.7f1** (URP). Every sprite
and every sound is generated procedurally by the project — no downloaded assets.

## Play it

- **Built game:** `Builds/First2dgame/First2dgame.exe`
- **Open in Unity:** `Assets/Scenes/Level1.unity` (the only scene in Build Settings)

| Action | Keys |
| --- | --- |
| Move | `A` / `D` or `←` / `→` |
| Jump | `Space` or `W` |
| Attack | `J` or left mouse button |
| Restart (death / victory) | `R` |

## The level

Ash, a hooded wanderer, crosses **Hollow Grove** to reach **The Dusk Gate**.

- **6 shades:** five slimes and the brute **Thorne** (x ≈ 54). The gate stays sealed
  until all six are dead.
- **2 checkpoints:** lanterns at x ≈ 18.2 and 38.2. Falling into a pit respawns you at
  the last lantern with **no health loss**; losing all three hearts ends the run.
- **2 pits** (14–17 and 34–37) break up the walk, plus two optional platforms.
- The gate at x ≈ 61.5 ends the level when touched after the grove is cleared.

## Tech notes

- `Assets/Editor/Level1Builder.cs` builds the entire scene from code:
  sky, parallax, ground, monsters, checkpoints, gate, UI, audio, particles and the
  post-processing volume. Rebuild with the bridge command
  `invoke type=Level1Builder method=BuildAll`.
- `Assets/Editor/PixelArt.cs` draws every sprite (hero, slimes, Thorne, tiles, portal,
  glow dot) and imports it as 32-PPU point-filtered pixel art.
- `Assets/Scripts/Runtime/AudioManager.cs` synthesizes the music and all SFX at
  runtime (sword swing, clash, hurt, jump, checkpoint, portal, victory).
- **GPU pass (RTX 4050):** HDR camera, URP bloom + vignette + film grain + chromatic
  aberration + Neutral tonemapping (`Assets/Settings/HollowGroveVolume.asset`),
  SMAA High, 4× MSAA and 1.25× render scale, HDR glow sprites on the moon, lanterns
  and portal, plus GPU particle embers, fireflies and brazier sparks.
- The editor bridge (`Assets/Editor/OpenCodeBridge.cs` + `unity-bridge/ubridge.py`)
  allows inspection, play-mode testing, screenshots and builds from the terminal.
  `unity-bridge/smoke.py` runs the full gameplay smoke test.

## Team

Built by **Big Pickle** (orchestration, Unity, code, builds) with the free OpenCode
models listed in `unity-bridge/TEAM.md` handling story, visual review, game design,
dialogue and logic review.
