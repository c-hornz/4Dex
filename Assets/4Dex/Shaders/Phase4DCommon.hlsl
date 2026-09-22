#ifndef FOURDEX_PHASE4D_COMMON_INCLUDED
#define FOURDEX_PHASE4D_COMMON_INCLUDED

// Shared by every Phase4D pass. The shader renders perception channels; it does not decide them.
// Every per-object value below is computed by FourDex.Perception.PerceptionModel on the CPU and arrives through
// a MaterialPropertyBlock, so the mapping rules have one tested source (ARCHITECTURE §6).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float _Density;         // absW -> fraction of the surface drawn
    float _Coherence;       // focus error -> 1 stable .. 0 incoherent
    float _PhaseFrequency;  // absW -> stripes per metre
    float _PhaseVelocity;   // signedW -> stripes per second; + rises (ana), - sinks (kata)
    float _PhaseAmplitude;  // grows with distance from focus; 0 in focus
    float _Tint;            // signedW -> palette weight, -1 kata .. +1 ana
    float _EdgeWeight;      // 1 in FAR_W: keep the silhouette, drop the faces
    float _ShimmerRate;     // dither re-seeds per second; driven by (1 - coherence)
CBUFFER_END

// Globals from PerceptionSettings: shared by every object, never per-object.
float4 _FourDexAnaColor;
float4 _FourDexKataColor;
float _FourDexTintStrength;
float _FourDexDitherCellsPerMetre;
float _FourDexFarFaceFill;

// ---- 4D shadow field (SPEC §12-§14) ----
// Bounded occluder list, uploaded per frame by ProjectionDriver and evaluated per pixel on receivers. Unity's shadow
// system cannot express any of this, so none of it goes through it (ARCHITECTURE §6).
#define FOURDEX_MAX_OCCLUDERS 16
float4 _FourDexOccluderSphere[FOURDEX_MAX_OCCLUDERS]; // xyz centre, w influence radius
float4 _FourDexOccluderW[FOURDEX_MAX_OCCLUDERS];      // x strength, y signed tint, z shimmer Hz, w edge softness
float _FourDexOccluderCount;
float _FourDexShadowDarkness;
float _FourDexShadowTint;
float _FourDexShadowReach;

// Cheap 3D hash in [0, 1).
float FourDexHash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// Dither threshold from a lattice fixed to the object's surface, in metres. Deriving it from screen position would
// give each eye different noise under single-pass stereo, which reads as static (ARCHITECTURE §9, risk 2).
float FourDexDitherThreshold(float3 surfaceMetres, float time)
{
    float3 cell = floor(surfaceMetres * _FourDexDitherCellsPerMetre);
    // Shimmer: the pattern re-seeds at a rate set by focus mismatch, so incoherence is visible as flicker.
    float seed = floor(time * _ShimmerRate);
    return FourDexHash31(cell + seed * 17.0);
}

// Travelling phase stripes along world up. Direction is the ana/kata cue that does not depend on colour (SPEC §9).
float FourDexPhase(float worldHeight, float time)
{
    return sin(6.2831853 * (_PhaseFrequency * worldHeight - _PhaseVelocity * time));
}

// Fraction of this fragment's neighbourhood to draw. Called by every pass so depth and colour agree on coverage.
float FourDexCoverage(float phase, float3 normalWS, float3 viewDirWS)
{
    float fill = _Density;
    // Density bands travel with the phase, so the stripes are also visible as moving gaps, not only brightness.
    fill *= saturate(1.0 + _PhaseAmplitude * phase);
    // FAR_W: the silhouette is emphasised and faces turned toward the viewer thin out, but never to zero:
    // a flat face seen head-on has no silhouette, and must not read as OUTSIDE (SPEC §11, §27).
    float rim = 1.0 - saturate(abs(dot(normalWS, viewDirWS)));
    fill *= lerp(1.0, lerp(_FourDexFarFaceFill, 1.0, rim), _EdgeWeight);
    return fill;
}

// Strongest shadow reaching this point, and the ana/kata sign of the occluder that cast it. The occluder's W distance
// arrives as strength, softness and shimmer, so an unseen occluder still darkens the floor and says which side it is on.
void FourDexShadowField(float3 positionWS, float3 lightDir, float time, out float shadow, out float tint)
{
    shadow = 0;
    tint = 0;
    int count = (int)_FourDexOccluderCount;

    [loop] for (int i = 0; i < count; i++)
    {
        float4 sphere = _FourDexOccluderSphere[i];
        float4 w = _FourDexOccluderW[i];

        // Along the light: how far this point lies behind the occluder.
        float3 d = positionWS - sphere.xyz;
        float t = dot(d, -lightDir);
        if (t <= 0 || t > _FourDexShadowReach)
            continue;

        // Off the shadow axis, against an influence radius that grows with distance (penumbra) and with softness
        // (SPEC §14: an out-of-focus shadow has uncertain edges).
        float3 perpendicular = d + lightDir * t;
        float radius = max(sphere.w * w.w * (1.0 + 0.12 * t), 1e-4);
        float edge = smoothstep(0.0, 1.0, 1.0 - length(perpendicular) / radius);
        if (edge <= 0)
            continue;

        // Fade out at the reach limit, and skip the occluder's own surface.
        float fade = saturate(1.0 - t / _FourDexShadowReach) * saturate(t / max(sphere.w, 1e-3) - 0.5);
        // Shimmer: focus mismatch makes the shadow unsteady.
        float flicker = w.z > 0 ? 0.78 + 0.22 * sin(6.2831853 * w.z * time + sphere.x * 12.9898 + sphere.z * 4.1414) : 1.0;

        float strength = w.x * edge * fade * flicker;
        if (strength > shadow)
        {
            shadow = strength;
            tint = w.y;
        }
    }
}

// Object-space position in metres: world-scale units that stay attached to the object as it moves and turns.
float3 FourDexSurfaceMetres(float3 positionOS)
{
    float3 scale = float3(
        length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x)),
        length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y)),
        length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z)));
    return positionOS * scale;
}

#endif
