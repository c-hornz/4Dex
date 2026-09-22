// Renders a 4D object's perception channels (SPEC §8, §9, §11). Opaque with dithered coverage.
// Per-object values are set by PhaseRenderer through a MaterialPropertyBlock each frame; the defaults below
// are the in-focus state, so an object with no PhaseRenderer renders as ordinary solid geometry.
Shader "4Dex/Phase4D"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.82, 0.82, 0.82, 1)
        [HideInInspector] _Density ("Density", Range(0, 1)) = 1
        [HideInInspector] _Coherence ("Coherence", Range(0, 1)) = 1
        [HideInInspector] _PhaseFrequency ("Phase Frequency", Float) = 2
        [HideInInspector] _PhaseVelocity ("Phase Velocity", Float) = 0
        [HideInInspector] _PhaseAmplitude ("Phase Amplitude", Range(0, 1)) = 0
        [HideInInspector] _Tint ("Tint", Range(-1, 1)) = 0
        [HideInInspector] _EdgeWeight ("Edge Weight", Range(0, 1)) = 0
        [HideInInspector] _ShimmerRate ("Shimmer Rate", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        Pass
        {
            Name "Phase4DForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Phase4DVertex
            #pragma fragment Phase4DFragment
            #pragma multi_compile_instancing
            #include "Phase4DPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Back
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Phase4DVertex
            #pragma fragment Phase4DFragment
            #pragma multi_compile_instancing
            #define FOURDEX_DEPTH_ONLY
            #include "Phase4DPass.hlsl"
            ENDHLSL
        }
    }
}
