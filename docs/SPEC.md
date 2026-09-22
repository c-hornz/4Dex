# 4D VR MAZE / PERCEPTION ENGINE — Master Engineering Specification

The authoritative requirements document for this project. Architecture decisions
derived from it live in `ARCHITECTURE.md`.

Conceptual inspiration: the 4D Maze at https://www.urticator.net/maze/ — not to be
cloned in implementation or visual presentation. The relevant conceptual ideas are
genuine four-dimensional maze topology, eight movement directions, 4D orientation,
and navigation through a four-spatial-dimensional environment. This project extends
those ideas into VR and focuses heavily on perceptual methods for helping a human
develop intuition for four-dimensional space.

The central design question:

> How can VR give a human an intuitive sensory language for perceiving, inspecting,
> rotating through, and navigating four spatial dimensions?

The project must be designed as a **4D perception and navigation engine first and a
maze game second**.

---

## 1. Fundamental requirement: real 4D state

The world must have genuine four-dimensional spatial state. Position is
`P = (x, y, z, w)`, with `+W = Ana` and `-W = Kata`.

W must NOT be implemented as merely: scene switching; teleportation between ordinary
3D levels; hidden Unity scenes; a stack of unrelated rooms; a cosmetic shader
parameter.

Objects, players, cells, lights where appropriate, orientation, collision,
perception, and maze topology must be capable of containing meaningful
four-dimensional information.

A 4D grid cell has eight cardinal neighbors: +X/-X, +Y/-Y, +Z/-Z, +W/-W. The
architecture must preserve this model even where prototype rendering uses
approximations.

## 2. Target platform

Initial target: **Oculus Rift PCVR**. Prefer current Unity XR/OpenXR abstractions
where practical so hardware dependencies remain isolated. Do not design around Quest
standalone/mobile performance limitations.

However: isolate Rift/OpenXR input behind an abstraction layer; do not make
mathematical systems depend on Oculus APIs; do not make world systems depend on VR
APIs; preserve the possibility of future headset support.

Before committing to specific Unity packages or APIs, inspect the existing project if
one exists and explain the proposed technical choice.

## 3. Core architectural principle

Strictly separate: (1) 4D mathematics, (2) 4D world state, (3) player state,
(4) perception/projection, (5) XR tracking, (6) input mapping, (7) rendering,
(8) shadows/lighting, (9) maze generation, (10) debug/experimental tooling.

Suggested conceptual architecture:

    4D Engine
    |
    +-- Math4D
    |   +-- Vector4 / native equivalent where sensible
    |   +-- Basis4D
    |   +-- PlaneRotation4D
    |   +-- Rotation4D
    |   +-- Projection4Dto3D
    |   +-- Transform4D
    |
    +-- World4D
    |   +-- Object4D
    |   +-- Cell4D
    |   +-- Maze4D
    |   +-- Collision4D
    |   +-- MazeGenerator4D
    |
    +-- Player4D
    |   +-- Position4D
    |   +-- Orientation4D
    |   +-- MovementBasis
    |   +-- ViewBasis
    |
    +-- Input4D
    |   +-- XRInputAdapter
    |   +-- HMDPoseInput
    |   +-- ControllerInput
    |   +-- InputMappingProfile
    |   +-- MappingMatrix
    |
    +-- Perception4D
    |   +-- WFocus
    |   +-- WDepth
    |   +-- Projection
    |   +-- PhaseRenderer
    |   +-- OcclusionModel
    |   +-- SurveyMode
    |
    +-- Shadow4D
    |   +-- Light4D
    |   +-- Occluder4D
    |   +-- ShadowField
    |   +-- PhaseOcclusion
    |   +-- ShadowCoherence
    |   +-- ShadowRipple
    |   +-- ShadowPalette
    |
    +-- XR
    |   +-- Rift/OpenXR integration
    |   +-- StereoCameraRig
    |   +-- Tracking
    |   +-- ComfortSettings
    |
    +-- Debug4D
        +-- CoordinateHUD
        +-- BasisVisualizer
        +-- WSliceViewer
        +-- MappingVisualizer
        +-- ShadowVisualizer
        +-- RuntimeParameterPanel

Math4D must know nothing about XR. World4D must know nothing about Oculus. Input
mapping must not own world topology. Rendering must not define the mathematical rules
of the world.

## 4. Four-dimensional rotation

4D Euclidean space has six fundamental coordinate rotation planes: XY, XZ, YZ, XW,
YW, ZW.

Implement plane rotations as first-class operations. Do NOT force the project into a
conventional 3D yaw/pitch/roll mental model. A 4D orientation must support
compositions of rotations across these six planes.

Pay special attention to XW, YW, ZW, because these rotate the observer or object
through Ana/Kata. The implementation should be numerically stable and suitable for
continuous interaction. Document the mathematical representation chosen and why.

## 5. Physical head tracking vs 4D view transformation

DO NOT permanently map physical headset motion to only ordinary XYZ viewing.

The Rift supplies 6-DoF HMD tracking — translation (left/right, up/down,
forward/back) and rotation (yaw, pitch, roll). **All six physical signals must be
available independently to the 4D input mapping system.**

    HMD TRACKING
    |
    +------> physical VR/stereo tracking
    |
    +------> configurable 4D input mapping
             |
             +--> XYZ translation
             +--> W translation
             +--> XY rotation
             +--> XZ rotation
             +--> YZ rotation
             +--> XW rotation
             +--> YW rotation
             +--> ZW rotation

Physical headset tracking required for stable stereo VR rendering must remain
correct. However, HMD pose deltas may simultaneously drive 4D transformations.

Valid experimental mappings include: head pitch -> YW rotation; head roll -> XW
rotation; physical forward/back lean -> W translation; head pitch -> 70% YZ rotation
+ 30% YW rotation. Do not hardcode a single interpretation.

## 6. Generalized input mapping

Create a configurable mapping system supporting: one-to-one mapping; one-to-many
mapping; weighted mappings; gain; inversion; dead zones; sensitivity curves; optional
clamping; absolute versus delta behavior where appropriate.

A physical input may contribute simultaneously to multiple virtual dimensions:

    HeadPitch:    YZ = 0.70,  YW = 0.30
    HeadRoll:     XW = 0.50
    HeadForward:  Z  = 0.80,  W  = 0.20

Do not implement experimental control schemes as separate hardcoded controller
scripts. Implement one generalized mapping system and express schemes as
profiles/configuration.

Initial profiles:

- **CLASSIC** — HMD primarily conventional XYZ; controller handles Ana/Kata.
- **HEAD_TILT** — pitch and/or roll contribute to W-related rotations.
- **HEAD_TRANSLATE** — physical leaning contributes to W translation.
- **HYBRID** — familiar motion primarily behaves normally but partially leaks into W.
- **FULL_4D** — all available HMD/controller axes independently mappable.
- **CUSTOM** — exposes the complete mapping configuration.

These are presets over one underlying system.

## 7. Three distinct W interactions

These must remain independent. Do not collapse them into one "fourth dimension"
control.

**A. Move through W** — change `player.position.w`. The player actually moves Ana or
Kata.

**B. Rotate toward W** — change the player's/viewer's 4D orientation through XW, YW,
ZW. The player does not necessarily change W position. The perceived 3D subspace
rotates through 4-space.

**C. Focus through W** — change `focusW`. The player's physical 4D position does not
change; the view orientation does not necessarily change. Instead the perceptual
system emphasizes geometry around another W distance.

## 8. W-focus and depth of field

Introduce `focusW` and `focusWidth`. For an object or geometric contribution,
`deltaW = objectW - focusW`.

A useful initial visibility model:

    V = exp(-(abs(deltaW) / sigma)^2)

where sigma derives from `focusWidth`. This is only a starting function; the system
must permit experimentation with alternative response curves.

At `deltaW ~ 0`: geometry should be coherent, stable, strongly visible, spatially
readable. As `abs(deltaW)` increases: coherence decreases; phase effects increase;
density may decrease; transparency may increase; edges may distort; temporal behavior
may change.

**Signed deltaW must remain perceptually recoverable so Ana and Kata are
distinguishable.**

## 9. Phase-depth visualization

Do not rely primarily on conventional wireframes. Use phase-like visual behavior to
communicate fourth-dimensional distance.

Candidate channels: opacity; dither density; surface density; spatial frequency; edge
coherence; phase velocity; phase direction; chromatic shift; surface displacement;
shimmer; interference patterns; spatial audio where appropriate.

These effects must be parameterized. Do NOT encode permanent rules such as
`Ana = blue, Kata = red`. Instead expose mappings such as:

    signedWDistance -> palette
    absWDistance    -> phaseFrequency
    signedWDistance -> phaseDirection
    focusError      -> coherence
    absWDistance    -> density

**Color should be only one redundant cue.** Motion/direction, frequency, coherence,
density, and other cues should permit perception without relying entirely on color.

## 10. Survey mode

Implement a temporary "4D Survey" perception mode that increases perceptual W
depth/focus width and exposes neighboring W geometry.

Normal view: local slice dominates. Survey view: nearby Ana/Kata structures appear as
phase-shifted echoes or density fields. Releasing survey mode restores normal focus
behavior.

Survey mode must use the same underlying W perception system, not a separately
authored alternate scene.

## 11. Occlusion model

W-distance must participate in visibility but must not simply be treated as another
conventional Z-buffer distance. For the initial prototype, a pragmatic banded
perception model:

- **LOCAL** — `abs(deltaW) < A` — normal or near-normal depth behavior.
- **NEAR_W** — `A <= abs(deltaW) < B` — phase-rendered, partially transmissive,
  depth aware.
- **FAR_W** — `B <= abs(deltaW) < C` — sparse density, edge/interference
  representation.
- **OUTSIDE** — `abs(deltaW) >= C` — not rendered.

A, B, C must be configurable. The architecture must allow this model to be replaced
later.

## 12. 4D shadows are a primary perception system

Do not treat shadows as cosmetic decoration. Shadows should help communicate hidden
four-dimensional structure.

A shadow should potentially encode: conventional XYZ occlusion; signed W displacement
of the occluder; magnitude of W displacement; W extent of the occluder; direction
toward the occluder through W; focus mismatch; 4D orientation.

Candidate perceptual channels:

| Channel | Encodes |
| --- | --- |
| Darkness | conventional/local occlusion strength |
| Hue | signed Ana/Kata displacement |
| Saturation | magnitude of W displacement |
| Ripple direction | Ana/Kata direction or occlusion direction |
| Ripple frequency | W distance |
| Ripple amplitude | occlusion strength |
| Edge coherence | proximity to current W slice |
| Shimmer | focus mismatch |
| Shadow parallax/shear | 4D orientation |
| Echo spread | W thickness/extent |

All mappings must be configurable.

## 13. Invisible objects may cast perceptible 4D shadows

This is intentional. An object outside the currently visible W slice may produce a
shadow/interference field on visible geometry.

Example — player at `w = 0`, hidden object at `w = +1.4`. The object may not render as
visible geometry, but its 4D occlusion influence may generate an Ana-signature shadow.

The player can then: (1) notice the anomalous shadow; (2) infer hidden geometry
exists; (3) adjust W-focus; (4) increase shadow coherence; (5) reveal the object;
(6) rotate toward it; (7) move Ana/Kata to interact with it.

**This interaction loop is a desired gameplay foundation.**

## 14. Shadow coherence and W-focus

Shadow appearance should respond to `focusW`. If `focusW ~ occluderW`, the shadow
becomes more coherent/stable. As focus mismatch increases: shimmer may increase;
edges may split; ripple interference may increase; shadow echoes may separate;
coherence may fall.

Thus shadows can be "focused" independently of actually moving toward their source.
Implement this relationship parametrically.

## 15. Shadow ripple / interference

Prototype a ripple or interference mechanism whose characteristics can encode W
information. Potential mapping:

    rippleFrequency = f(abs(deltaW))
    rippleDirection = sign(deltaW)
    rippleAmplitude = occlusionStrength

Do not assume the first mapping is perceptually optimal. Provide runtime controls so
frequency relationships can be inverted, scaled, disabled, or changed.

## 16. 4D shadow parallax

When the viewing basis rotates through XW, YW, ZW, shadows from 4D geometry should be
capable of: sliding; shearing; splitting; converging; changing coherence; changing
ripple orientation.

This can reveal 4D orientation even when ordinary XYZ geometry changes very little.
Design the shadow system so view-basis changes can influence shadow projection.

## 17. Shadow echoes and W extent

Objects with meaningful extent through W may produce multiple/continuous shadow
echoes. The spread of the shadow response may communicate W thickness.

As focus approaches the relevant W region, echoes may converge into a coherent shadow.
As focus moves away, the shadow may split into increasingly incoherent phase
components. Initially experimental and configurable.

## 18. 4D lighting

Architect for lights that may eventually contain a 4D directional component,
`L = (x, y, z, w)`. This allows conventional XYZ lights, Ana-biased illumination, and
Kata-biased illumination.

The first implementation does not need a physically perfect 4D global illumination
system. **Do not spend the first milestone attempting full 4D path tracing.** Build
abstractions that permit later experimentation with 4D light direction and occlusion.
Prioritize perceptual usefulness and interactive performance.

## 19. Maze representation

Eventually implement a real 4D grid maze:

    Cell4D {
        int x; int y; int z; int w;
        wallPosX; wallNegX;
        wallPosY; wallNegY;
        wallPosZ; wallNegZ;
        wallPosW; wallNegW;
    }

Use a representation that prevents inconsistent shared walls between neighboring
cells. Maze generation should operate on a four-dimensional adjacency graph.

**Do not begin with a huge maze.** The perception engine must be understandable
before maze complexity increases.

## 20. Debugging is a first-class feature

4D bugs will frequently look like rendering bugs even when they are mathematical bugs.
Build strong debugging tools early. Provide at minimum:

- **Coordinate HUD** — player x/y/z/w, `focusW`, `focusWidth`.
- **Orientation display** — the player's four basis vectors, numerically.
- **Rotation monitor** — current contributions to XY, XZ, YZ, XW, YW, ZW.
- **Input mapping monitor** — physical HMD/controller input and the resulting 4D
  transformation. E.g. `HeadPitch = +12 deg` -> `YZ +8.4 deg, YW +3.6 deg`.
- **W slice viewer** — a developer view showing objects/cells distributed across W.
- **Shadow debug view** — shadow source, occluder W, signed deltaW, ripple frequency,
  coherence, influence radius.
- **Freeze controls** — freeze physical movement, W movement, view rotation, focus,
  phase animation, shadow animation. Important for diagnosis.

## 21. Runtime experiment panel

Create an in-development runtime panel for changing perceptual parameters without
recompilation. Expose: `focusW`; `focusWidth`; `surveyWidth`; phase frequency; phase
amplitude; phase velocity; density curve; opacity curve; Ana/Kata palette; shadow
ripple frequency; shadow ripple amplitude; shadow coherence; shadow influence radius;
shadow echo spread; mapping gains; mapping dead zones; rotation gains.

Presets should be saveable if practical. The entire purpose is rapid perceptual
experimentation.

## 22. Comfort

This is experimental VR and some mappings may be uncomfortable. **Do not prevent
experimental mappings merely because they may be disorienting.** Instead provide
configurable comfort controls: snap vs smooth conventional rotation; gain limiting;
dead zones; reduced W rotation gain; vignette option; recenter; instant return to
conventional mapping; freeze 4D view transformation; reset orientation; disable
individual HMD-derived mappings.

**There must always be an emergency/reset action that restores a stable conventional
viewing configuration.**

## 23. First test environment

Do NOT start with procedural maze generation. Create a controlled 4D laboratory with
identifiable test geometry centered approximately at:

    Object A: w =  0
    Object B: w = +1
    Object C: w = -1

Additional objects should span W where useful. The test environment should make it
obvious whether: focus works; Ana/Kata are distinguishable; movement through W works;
XW/YW/ZW rotations work; phase behavior works; hidden shadows work; shadow focus
works.

Use simple geometry initially. **Clarity is more important than art.**

## 24. Implementation milestones

Implement incrementally. Do not jump directly to a finished game.

**Milestone 0 — Project/Foundation.** Unity/XR project structure, Rift/OpenXR
integration, Math4D, Transform4D, debug infrastructure, test scene.
*Acceptance:* headset tracks correctly; ordinary test scene is stable; 4D coordinates
can be inspected; mathematical unit tests exist.

**Milestone 1 — W-Focus.** `focusW`, `focusWidth`, objects at different W,
phase/density visualization.
*Acceptance:* changing `focusW` causes the appropriate W object to become maximally
coherent without moving the player.

**Milestone 2 — Ana/Kata Translation.** Actual player W movement.
*Acceptance:* moving Ana toward an object causes that object's relative W displacement
to approach zero. Focus movement and physical W movement remain independent.

**Milestone 3 — 4D Rotation.** XW, YW, ZW plus required ordinary rotation planes.
*Acceptance:* the perceived 3D subspace can rotate through W without changing player W
position.

**Milestone 4 — Generalized Input Mapping.** Connect HMD and controller inputs to the
mapping system.
*Acceptance:* a runtime/configuration change can make head pitch control ordinary
pitch, YW rotation, or a weighted combination without changing code.

**Milestone 5 — Survey Mode.** Temporary widened W perception.
*Acceptance:* neighboring W geometry becomes perceptible while preserving signed W
cues.

**Milestone 6 — 4D Shadow Prototype.** Hidden-W occlusion cues.
*Acceptance:* an object outside the visible W focus can produce a distinguishable
shadow/interference response; the response changes appropriately with signed W
distance and focus.

**Milestone 7 — Advanced Shadow Behavior.** Ripple, coherence, shadow echoes,
view-dependent shear/parallax, configurable Ana/Kata signatures.
*Acceptance:* developers can manipulate parameters live and compare representations.

**Milestone 8 — 4D Cells.** A small hand-authored connected 4D cell structure.
*Acceptance:* player can navigate between cells through ordinary directions and
Ana/Kata.

**Milestone 9 — Maze Generation.** A small procedural 4D maze. Only begin after the
perceptual systems are functional.

## 25. Testing requirements

Write tests for mathematical systems wherever practical. At minimum test: Vector4
operations; normalization; basis orthogonality; plane rotations; inverse rotations;
composition; Transform4D behavior; W translation; focus calculations; maze neighbor
consistency.

Important invariants:

- A pure XW rotation must not accidentally alter unrelated axes beyond mathematically
  expected results.
- A rotation followed by its inverse should approximately recover the original vector.
- The orientation basis should remain numerically stable after repeated rotations.
- Moving `focusW` must not change `player.position.w`.
- Moving `player.position.w` must not implicitly change `focusW` unless explicitly
  configured.
- Physical HMD tracking must remain available even when the same input drives a 4D
  mapping.

## 26. Performance philosophy

Target smooth PCVR performance. Do not prematurely optimize at the expense of
comprehensibility. However, avoid architectural decisions that obviously require
rendering every possible W slice independently.

Prefer: GPU-friendly parameterization; shader-based phase effects; bounded W
influence; culling; reusable geometry; instancing where useful. Profile before
performing substantial optimization.

## 27. Design rule: visual effects must carry information

A foundational artistic and engineering rule.

Phase is not decoration. Color is not decoration. Shimmer is not decoration. Ripple is
not decoration. Shadow distortion is not decoration. Density is not decoration.
Changes caused by head movement are not decoration.

Every major effect should ideally encode something about: signed W direction; W
distance; focus mismatch; W extent; 4D orientation; hidden occlusion; movement through
4D space.

**Avoid meaningless psychedelic noise. The goal is to create a learnable perceptual
language.**

## 28. Experimental philosophy

Do not assume we already know the best way for a human to perceive 4D space.
Therefore:

- PARAMETERIZE rather than hardcode.
- MEASURE rather than assume.
- BUILD SIMPLE TESTS before elaborate environments.
- KEEP MATHEMATICS separate from perceptual representation.
- KEEP PHYSICAL HMD TRACKING separate from 4D interpretation.
- KEEP MOVE, ROTATE, and FOCUS conceptually distinct.
- Make experimental visual encodings replaceable.

The project should function as both a game prototype and a laboratory for discovering
useful four-dimensional perceptual mappings.

## 29. Initial engineering task

Before writing large amounts of implementation code: (1) inspect the
repository/project; (2) report existing Unity version, XR setup, render pipeline,
important packages, directory structure, and current code; (3) identify anything
conflicting with this architecture; (4) propose the concrete technical architecture;
(5) identify the minimum classes/components/shaders needed for Milestones 0 and 1;
(6) identify mathematical assumptions requiring explicit decisions; (7) identify likely
VR/rendering risks; (8) produce a short implementation plan; (9) then begin Milestone 0.

Do not ask broad design questions whose answers are already contained in this
specification.

If an implementation detail is ambiguous but safely reversible, choose a reasonable
modular implementation and document the choice. **If a decision would fundamentally
constrain the 4D mathematical model or perception experiments, stop and surface that
decision before embedding it deeply into the architecture.**

## 30. Code quality

Use clear naming. Prefer small, testable mathematical components. Comment WHY a
mathematical transformation exists, not every obvious line of C#. Avoid giant manager
classes. Avoid hidden global state. Prefer configuration assets/data for experimental
mappings. Keep shader parameters organized and documented. Keep experimental rendering
logic replaceable. Use source-control-friendly assets/configuration. When implementing
mathematically nontrivial code, include concise comments explaining the convention
used.

## 31. Success criterion for the first prototype

The first meaningful prototype succeeds when a Rift user can stand inside a simple
environment and, without changing scenes:

1. perceive geometry at their current W location,
2. detect that geometry exists Ana and Kata,
3. change W-focus and bring those structures into perceptual coherence,
4. physically move through W,
5. rotate their viewing basis through W,
6. remap head/controller motion to different 4D transformations,
7. detect hidden W geometry through anomalous shadows,
8. use shadow phase/ripple/coherence to infer something about its W relationship,
9. return instantly to a stable conventional VR orientation.

The prototype does NOT need to be a polished game at this stage. The objective is to
prove that four-dimensional navigation can become perceptually learnable in VR.

**Build the perceptual instrument first.**
