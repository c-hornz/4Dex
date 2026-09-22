#ifndef FOURDEX_PHASE4D_PASS_INCLUDED
#define FOURDEX_PHASE4D_PASS_INCLUDED

// One vertex/fragment pair for both passes, so colour and depth clip identical coverage.
// FOURDEX_DEPTH_ONLY selects the depth pass.

#include "Phase4DCommon.hlsl"
#ifndef FOURDEX_DEPTH_ONLY
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 surfaceMetres : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings Phase4DVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.surfaceMetres = FourDexSurfaceMetres(input.positionOS.xyz);
    return output;
}

half4 Phase4DFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float time = _Time.y;
    float3 normalWS = normalize(input.normalWS);
    float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
    float phase = FourDexPhase(input.positionWS.y, time);

    // Dither-to-opaque rather than alpha blending: density without sort-order artefacts or lost depth (ARCHITECTURE §6).
    float coverage = FourDexCoverage(phase, normalWS, viewDirWS);
    clip(coverage - FourDexDitherThreshold(input.surfaceMetres, time) - 1e-4);

#ifdef FOURDEX_DEPTH_ONLY
    return 0;
#else
    Light mainLight = GetMainLight();
    half3 lighting = SampleSH(normalWS) + mainLight.color * saturate(dot(normalWS, mainLight.direction));
    half3 color = _BaseColor.rgb * lighting;

    // Phase also shows as brightness bands, so it reads even where density is full.
    color *= 1.0 + 0.5 * _PhaseAmplitude * phase;

    // Palette: one redundant cue, configurable, never the only carrier of ana/kata (SPEC §9).
    half3 palette = _Tint >= 0 ? _FourDexAnaColor.rgb : _FourDexKataColor.rgb;
    color = lerp(color, palette * max(lighting, 0.35), saturate(abs(_Tint)) * _FourDexTintStrength);

    return half4(color, 1);
#endif
}

#endif
