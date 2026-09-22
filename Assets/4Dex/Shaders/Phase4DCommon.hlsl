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
