# 4Dex — 4D VR Perception Engine

An experimental four-spatial-dimensional VR environment for Oculus Rift PCVR.
A perception and navigation engine first; a maze game second.

The central design question:

> How can VR give a human an intuitive sensory language for perceiving,
> inspecting, rotating through, and navigating four spatial dimensions?

## Status

**Pre-Milestone 0.** No implementation code exists yet. This repository currently
holds the specification and the architecture decided before implementation began.

Work started on a Linux machine with no Unity installed and continues in a Windows
environment where Unity and the Rift are available.

## Documents

| File | Contents |
| --- | --- |
| `docs/SPEC.md` | The master engineering specification. The authoritative requirements. |
| `docs/ARCHITECTURE.md` | Technical architecture, mathematical representation, milestone file plans, risks. |

## Planned layout

```
src/4Dex.Math4D/     pure .NET, zero deps, no UnityEngine reference
src/4Dex.World4D/    cells, maze topology, collision
tests/4Dex.Tests/    runs via `dotnet test` on any platform
Assets/4Dex/         Unity: Player4D, Input4D, Perception4D, Shadow4D, XR, Debug4D
```

The mathematical core is deliberately a plain .NET library so it can be built and
tested without Unity, and so "Math4D must know nothing about XR" is enforced by the
compiler rather than by convention.

## Open decision

Which Unity version the Windows machine has installed. Unity 6 LTS (6000.0) is the
recommendation; Unity 2022.3 LTS is the safe fallback. Only `Packages/manifest.json`
differs — the architecture is unaffected.
