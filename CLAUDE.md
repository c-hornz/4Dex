# 4Dex — working notes for Claude

Experimental four-spatial-dimensional VR perception engine for Oculus Rift PCVR.
A **perception and navigation instrument first, a maze game second.**

## Read these before doing anything

- `docs/SPEC.md` — the authoritative requirements (32 sections). Answers most
  design questions; don't ask the user what it already specifies.
- `docs/ARCHITECTURE.md` — decisions already made, and *why*.

**The architecture is decided. Do not relitigate it.** If you believe a decision
is wrong, say so in a sentence and keep working under it; don't quietly
substitute a different design.

## Current status

Pre-Milestone 0. No implementation code exists yet. The design was done on a
Linux box with no Unity installed; implementation continues on Windows.

**Open decision:** which Unity version is installed. Unity 6 LTS (6000.0)
recommended, 2022.3 LTS is the fallback. Only `Packages/manifest.json` differs.

Next task: Milestone 0 per `docs/ARCHITECTURE.md` §7.

## Non-negotiables

These are load-bearing. Violating one is a design regression, not a shortcut.

1. **Math4D and World4D never reference `UnityEngine`.** They are plain .NET
   libraries with their own `Vec4` and quaternion, so the mathematical core is
   testable without Unity and SPEC §3's separation is enforced by the compiler.
2. **HMD pose fans out to two independent consumers.** It drives the camera
   transform unmodified (stereo must stay correct), *and* separately feeds the
   4D mapping system, whose output changes only how geometry is **projected** —
   never the camera matrices. No `TrackedPoseDriver` on the camera.
3. **Move / Rotate / Focus stay three distinct operations** (SPEC §7).
   `position.w`, the XW/YW/ZW view basis, and `focusW` are independent. Never
   collapse them into one "fourth dimension" control, and never let one
   implicitly change another.
4. **Orientation is a `Rotor4` = pair of unit quaternions**, not a 4x4 matrix
   basis. `Basis4` is a derived read-only view for debug display and shader
   upload. See `docs/ARCHITECTURE.md` §4 for the derivation and the
   plane-to-generator table.
5. **Signed deltaW must stay perceptually recoverable.** Ana and kata must be
   distinguishable without relying on colour — direction, frequency, coherence
   and density carry it too (SPEC §9).
6. **Every visual effect encodes information** (SPEC §27). Phase, ripple,
   shimmer and density are not decoration. No meaningless psychedelic noise.
7. **Parameterize, don't hardcode.** Perceptual mappings live in configuration
   assets exposed at runtime, never in bespoke per-experiment scripts.

## Conventions

- `P = (x, y, z, w)`; `+W = ana`, `-W = kata`.
- Quaternion mapping for rotors: `q = w + xi + yj + zk` — **w is the real part.**
- Comment *why* a mathematical transformation exists, not every line of C#.

## Building and testing

The math core builds and tests anywhere, without Unity or a headset:

```
dotnet test
```

Do this before touching Unity-side code — a failing invariant there will
otherwise surface as a baffling rendering bug (SPEC §20).

## Debugging

4D bugs routinely look like rendering bugs. Debug tooling is a first-class
feature, not an afterthought — build the coordinate HUD, basis display and
freeze controls early (SPEC §20, §21).
