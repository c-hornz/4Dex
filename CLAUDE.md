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

Milestone 0 in progress, on Windows. `src/4Dex.Math4D` and `src/4Dex.World4D`
build and `tests/4Dex.Tests` passes (78/78, .NET SDK 10.0.401). World4D's
namespace is `FourDex.World`, not `FourDex.World4D`: a namespace named like its
`World4D` class would shadow the class from every other `FourDex.*` namespace.
Still to do for Milestone 0: the Unity project (URP + OpenXR/Oculus), Player4DState (owns the focusW /
position.w invariants), HmdPoseSource/XRRig, CoordinateHUD, Lab4D scene.

**Unity version: 6000.6.2f1** (Unity 6.6, a non-LTS update release). Chosen
over the originally recommended 6000.0 LTS; only `Packages/manifest.json` is
version-specific.

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
dotnet test 4Dex.Core.sln
```

(Name the solution explicitly: Unity generates its own `4Dex.sln` at the root.)
Namespaces are `FourDex.*`, since C# identifiers cannot start with a digit.

Do this before touching Unity-side code — a failing invariant there will
otherwise surface as a baffling rendering bug (SPEC §20).

## Debugging

4D bugs routinely look like rendering bugs. Debug tooling is a first-class
feature, not an afterthought — build the coordinate HUD, basis display and
freeze controls early (SPEC §20, §21).
