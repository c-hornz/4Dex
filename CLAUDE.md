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

Milestone 0 in progress, on Windows. `src/` holds three engine-free libraries,
Math4D, World4D and Player4D (Player4DState, the §25 focusW / position.w
invariants). They build and `tests/4Dex.Tests` passes (84/84, .NET SDK 10.0.401).
`src/` is also the Unity local package `com.fourdex.core`, so the same sources
compile in Unity, with `noEngineReferences` asmdefs. dotnet output goes to
`artifacts/` (see `Directory.Build.props`) so Unity never sees `obj/`. A new
library under `src/` needs a `.csproj`, an `.asmdef` and a `csc.rsp`.
Namespaces are `FourDex.World` and `FourDex.Player`, not `...World4D` /
`...Player4D`: a namespace named like a class in it would shadow that class from
every other `FourDex.*` namespace.
The Unity project lives at the repo root (URP Blank template, OpenXR 1.18.0,
XR Management 4.7.0). XR is configured by `Assets/4Dex/Editor/XRProjectSetup.cs`
(menu **4Dex > Configure XR**): OpenXR loader on Standalone, Oculus Touch
profile. It is idempotent; rerun it rather than clicking through Project Settings.
Still to do for Milestone 0: HmdPoseSource/XRRig, the MonoBehaviour wrappers
(Player4D, Object4DBehaviour/World4DRegistry), CoordinateHUD, Lab4D scene.

Unity runs headless, so Unity-side code can be compiled without the user:

```
"C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath C:\Users\cthor\4Dex -logFile <log>
```

Then search the log for `error CS`. Add `-executeMethod Namespace.Class.Method` to
run an editor script. Close the editor first; batch mode can't open a project
the editor already has open. Keep `-logFile` on a short path: under a long path
the Library cache exceeds Windows' 260-character limit.

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

(Name the solution explicitly: Unity generates its own `4Dex.slnx` at the root.)
Namespaces are `FourDex.*`, since C# identifiers cannot start with a digit.

Do this before touching Unity-side code — a failing invariant there will
otherwise surface as a baffling rendering bug (SPEC §20).

## Debugging

4D bugs routinely look like rendering bugs. Debug tooling is a first-class
feature, not an afterthought — build the coordinate HUD, basis display and
freeze controls early (SPEC §20, §21).
