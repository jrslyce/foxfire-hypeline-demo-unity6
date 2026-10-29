# Foxfire enemy models

These are the three owner-supplied Meshy FBX deliveries used by the regular enemy renderer:

- `AmethystBoar`: replaces the normal boar and plays its imported walking clip.
- `Emberboar`: replaces the orange champion and plays its imported walking clip.
- `Shroomling`: replaces the floating wisp. It has no supplied animation, so `EnemyModelVisuals` adds a small vertical float (`0.12` world units amplitude, `2.2` radians/second, approximately `2.86` seconds per cycle).

Original FBX files and PBR maps are retained here as bake inputs, outside Resources so they do not inflate player builds. `Foxfire.Editor.EnemyModelBake.Bake` renders each boar's supplied walking clip into 16 directions by 16 poses; the static shroomling uses 16 views. The bake assigns the supplied albedo explicitly and checks every frame for clipping and empty output. Atlases and timing/pivot metadata live in `Resources/EnemyFrames`.

Runtime presentation follows movement, holds poses when stationary, advances on the simulation clock, and uses the existing sprite pool and ground-based sorting. Charge accelerates the gait; dissolve fades the model sprite instead of reverting to a placeholder. Missing art has a geometric fallback, but release validation rejects missing or invalid atlases. `scripts/check-enemy-atlases.py` checks distinct gait poses and produces a review sheet. Development players accept `-enemy-preview` for an isolated 17-frame render fixture with no live bridge.
