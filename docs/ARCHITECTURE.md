# 4Dex — Technical Architecture

Decided before implementation, from the requirements in `SPEC.md`.
Status: pre-Milestone 0. Nothing here has been compiled.

---

## 1. Environment as found

Audited on the Linux workstation where the design was done:

| Item | Finding |
| --- | --- |
| Repository | empty; git initialized, branch `master`, zero commits, no remote, no identity |
| Unity | not installed (no Hub, no `~/Unity`, no `ProjectSettings/`, no `Packages/manifest.json`) |
| Render pipeline | none |
| XR packages | none |
| Toolchain | .NET SDK 6.0.400 (`linux-x64`) only; no mono, no msbuild |
| OS | Kali Linux 6.18.12, x86_64 |

SPEC §2 targets Oculus Rift PCVR, whose runtime is Windows-only, and Unity was
absent. This does not block the project; it decides **where the verifiable seam
goes**, which is decision 2 below.

---

## 2. Layering

**Math4D and World4D are a plain .NET class library with no `UnityEngine`
reference at all** — their own `Vec4`, their own quaternion.

```
src/4Dex.Math4D/     netstandard2.1, zero dependencies
src/4Dex.World4D/    netstandard2.1, depends on Math4D
tests/4Dex.Tests/    net10.0 + xUnit, runs anywhere
Assets/4Dex/         Unity-only: Player4D, Input4D, Perception4D,
                     Shadow4D, XR, Debug4D
```

Why: SPEC §3 requires that Math4D know nothing about XR and World4D nothing about
Oculus. Making them separate assemblies turns that from a convention into a
compile-time guarantee — the assembly cannot reference what it does not link.
It also means the mathematical core, which is where 4D bugs actually live
(SPEC §20), is fully testable without Unity or a headset.

Unity consumes the libraries as source: `src/` is the local package
`com.fourdex.core` (`Packages/manifest.json` → `file:../src`), with one asmdef per
library, each with `noEngineReferences: true`, so Unity's compiler rejects
`UnityEngine` there as well.

Player4DState's core sits here too, not under `Assets/`, because §7 requires its
focusW / position.w invariants to be testable without Unity.

---

## 3. HMD tracking vs. the 4D view transform  (SPEC §5, §25)

The invariant, fixed before any code exists:

> **Physical HMD pose drives the Unity camera transform, always, unmodified.
> The same pose, read independently, feeds the mapping system, whose output
> mutates the 4D basis and 4D position — which affect only how world geometry is
> *projected*, never the camera matrices.**

```
HMD pose (6 DoF) ─┬─> camera transform        (stereo, unmodified)
                  └─> mapping system ─> 4D basis + position ─> projection of
                                                                world geometry
```

Consequence: a mapping such as "head pitch contributes 30% YW rotation" does not
tilt, offset, or desynchronise the stereo view. It changes *where the world's
geometry lands*. SPEC §25's requirement that HMD tracking remain available even
when the same input drives a 4D mapping is then structurally true rather than
tested-and-hoped.

Practical rule for Unity: **do not put a `TrackedPoseDriver` on the camera.**
The XR layer reads the pose once per frame and fans it out to both consumers.

---

## 4. 4D orientation representation  (SPEC §4)

**Chosen: a pair of unit quaternions, `Rotor4 = {L, R}`, with a 4x4 orthonormal
`Basis4` as a derived read-only view.**

Map `p = (x,y,z,w)` to the quaternion `q = w + xi + yj + zk` — **w is the real
part**. Rotation is:

    p' = L * p * conj(R)

This is the Van Elfrinkhof / isoclinic decomposition, SO(4) = (SU(2) x SU(2))/±1,
the true 4D analogue of quaternions.

### Why not the alternatives

- **4x4 matrix basis.** Drifts under repeated composition and needs Gram-Schmidt
  repair, which silently perturbs the basis — exactly the failure mode SPEC §25
  warns about ("stable after repeated rotations"). Two unit quaternions
  renormalize *exactly and independently*: 8 floats, composition = 2 quaternion
  products.
- **Cl(4,0) rotors.** An even-grade element of Cl(4) is not always a simple rotor
  and its normalization is subtle once the pseudoscalar term is involved. The
  double-quaternion form is the same object with the pathology removed.

### Plane-to-generator correspondence

Derived, and structurally load-bearing for this project.

Left-multiplying by `exp(i*alpha)` gives `w' = cos(a)w - sin(a)x`,
`x' = sin(a)w + cos(a)x`, `y' = cos(a)y - sin(a)z`, `z' = sin(a)y + cos(a)z`
— i.e. **XW by +alpha and YZ by +alpha simultaneously** (left-isoclinic).
Right-multiplying by `conj(exp(i*beta))` gives **XW by -beta and YZ by +beta**
(right-isoclinic). Solving the pair yields exact simple rotations:

| Generator | Plane pair | Pure ana/kata plane | Pure 3D plane |
| --- | --- | --- | --- |
| `i` | XW / YZ | XW: `alpha = t/2, beta = -t/2` | YZ (pitch): `alpha = beta = t/2` |
| `j` | YW / ZX | YW | ZX (yaw) |
| `k` | ZW / XY | ZW | XY (roll) |

Sign note: the derivation above measures XW from W toward X. The implementation
(`Plane4`) uses one convention for all six planes — `+t` turns the first-named
axis toward the second — under which a pure ana/kata plane is `alpha = -t/2,
beta = +t/2`. The pairing is unchanged; only the sign label differs. The tests
check every plane against an independent Givens rotation.

**Each ana/kata rotation plane is the conjugate partner of exactly one ordinary
3D rotation plane.** Pitch and XW are the two halves of one generator; yaw and
YW; roll and ZW. This is a real handle on SPEC §5/§6's weighted head mappings:
"pitch bleeds into W" has a canonical cheapest form (bias the alpha/beta split)
as well as the general cross-generator form.

`Basis4` — the four basis vectors SPEC §20 requires the HUD to display — is
produced on demand by applying the rotor to the unit axes, and is what gets
uploaded to shaders.

---

## 5. Projection  (SPEC §7, §8)

**Slice projection, behind an `IProjection4` interface.**

For world point `P`, with `d = P - player.position4`:

    (dx, dy, dz, dw) = (d.bx, d.by, d.bz, d.bw)      [dots with basis vectors]

`(dx, dy, dz)` is handed to Unity as an ordinary 3D position. **`dw` is the
signed W offset that drives every perception channel**, measured against focus as
`deltaW_eff = dw - focusW`.

Treated as spec-determined rather than open: `focusW`/`focusWidth` (§8), the
banded occlusion model (§11), and Survey mode (§10) are only coherent under "the
player inhabits a 3-space slice with depth of field through W". Urticator-style
4D *perspective* — seeing a projected 4D volume — is a different instrument and
remains available as a second `IProjection4` implementation, not as the default.

---

## 6. Rendering and shaders  (SPEC §9, §11, §26)

**URP.** Single-pass instanced stereo support is mature; hand-written HLSL keeps
SPEC §30's "shader parameters organized and documented" achievable; and HDRP's
expensive lighting is precisely the subsystem SPEC §12 replaces with a custom
perception model, so paying for it would be waste.

Milestone 1 needs one shader, `Phase4D`, plus a shared `Phase4DCommon.hlsl`:

- Per-object values via `MaterialPropertyBlock`, so there's no material
  duplication. *As built:* the mapping from W gap to channels runs in C#
  (`FourDex.Perception.PerceptionModel`, engine-free and unit-tested). The
  block carries the resulting channels (density, coherence, phase
  frequency/velocity/amplitude, tint, edge weight), not a raw `_DeltaW`. That
  keeps one tested source for the mapping while objects are rigid, with one
  gap each. It must move to HLSL once geometry carries per-vertex W.
- Global perception constants (`focusW`, `focusWidth`, band thresholds A/B/C,
  phase frequency / amplitude / velocity, palette) from one `PerceptionSettings`
  ScriptableObject.
- Default visibility `V = exp(-(|deltaW| / sigma)^2)`, behind a swappable curve
  selector — SPEC §8 calls this "only a starting function".
- **Dither-to-opaque rather than alpha blending** in the LOCAL and NEAR_W bands.
  Transparency under single-pass stereo means sort-order artefacts and lost
  depth; a screen-space blue-noise threshold gives density *and* correct depth.
  Alpha is reserved for where it genuinely reads better.
- Phase direction = `sign(deltaW)`, so ana and kata stay distinguishable without
  relying on hue (SPEC §9: colour is one redundant cue, never the only one).

**Shadows cannot use Unity's shadow system at all.** None of ripple, coherence,
echo, or signed-W displacement is expressible there. Plan: a bounded
`StructuredBuffer<Occluder4D>` evaluated per-pixel on receivers. That is
Milestone 6; its cost bound is occluder count x receiver pixels.

---

## 7. Milestone 0 file plan

    src/4Dex.Math4D/     Vec4  Quat  Plane4(enum)  Rotor4  Basis4  Transform4
                         IProjection4  SliceProjection  Projected4
    src/4Dex.World4D/    Object4D  Cell4D  World4D  (Maze4D/Collision4D stubs)
    src/4Dex.Player4D/   Player4DState
    tests/4Dex.Tests/    Vec4Tests  RotorTests  BasisTests  TransformTests
                         ProjectionTests  InvariantTests  PlayerStateTests
    Assets/4Dex/Runtime/World4D/    Object4DBehaviour  World4DRegistry
    Assets/4Dex/Runtime/Player4D/   (MonoBehaviour wrapper around Player4DState)
    Assets/4Dex/Runtime/XR/         HmdPoseSource  XRRig
    Assets/4Dex/Runtime/Debug4D/    CoordinateHUD
    ProjectSettings/  Packages/manifest.json  Assets/Scenes/Lab4D.unity

### Test invariants  (SPEC §25)

The ones that catch real bugs, all runnable without Unity:

- A pure XW rotation leaves Y and Z untouched.
- `R` composed with `R^-1` recovers the original vector within tolerance.
- The basis remains orthonormal after 1e5 random compositions.
- Changing `focusW` never alters `player.position.w`.
- Changing `player.position.w` never implicitly alters `focusW`.

## 8. Milestone 1 file plan

    Assets/4Dex/Runtime/Perception4D/  WFocusController  PerceptionSettings
                                       ProjectionDriver  PhaseRenderer
                                       OcclusionBands
    Assets/4Dex/Shaders/               Phase4D.shader  Phase4DCommon.hlsl
    Assets/4Dex/Runtime/Debug4D/       WSliceViewer  RuntimeParameterPanel

Lab objects at w = 0, +1, -1 per SPEC §23.

---

## 9. Risks

1. **Rigid objects vs. true hypersurface slicing.** Milestones 0-1 treat a 4D
   object as a rigid body with a *parameterized* W extent, not as a tesseract
   genuinely cross-sectioned by the viewing 3-space. True slicing (marching
   tetrahedra) changes mesh topology every frame and is a later milestone.
   Flagged now because SPEC §17's "shadow echoes encode W thickness" eventually
   wants real extent.
2. **Dither under single-pass instanced stereo.** A blue-noise threshold derived
   from raw screen position produces eye-inconsistent noise and therefore
   binocular rivalry. It must derive from a world- or view-stable coordinate, or
   it will look like static and read as broken.
3. **Comfort.** HMD-derived W rotation is the most likely nausea source. SPEC
   §22's instant reset must exist from Milestone 4, not be retrofitted.
4. **Shadow-field cost** scales with occluder count x receiver pixels. Bounded
   influence radius is mandatory, not an optimization.
5. **No local build path on Linux.** Resolved by moving to Windows, but it means
   the Unity-side code in this repository has never been compiled.

---

## 10. Unity version (resolved)

**Resolved: Unity 6000.6.2f1** is installed on the Windows machine. The options
originally considered:

- Unity 6 LTS (6000.0) — recommended. Best OpenXR + URP support; the render-graph
  URP path is where custom passes are heading.
- Unity 2022.3 LTS — safe fallback if already installed. URP 14, very
  well-documented XR path.

The architecture above holds either way; only `Packages/manifest.json` changes.

## 11. First steps on Windows

1. Clone this repository; confirm the installed Unity version.
2. Create the Unity project with URP and the OpenXR plugin, Oculus feature group
   enabled. XR Interaction Toolkit is *not* needed — we want raw input.
3. Build `src/4Dex.Math4D` and run `dotnet test` to confirm the core passes
   outside the editor.
4. Implement Milestone 0 against §7, ending with a stable tracked scene and an
   inspectable 4D coordinate HUD.
