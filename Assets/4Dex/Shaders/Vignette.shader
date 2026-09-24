// Comfort vignette (SPEC §22): a head-locked disc that closes in from the edges while the player is moving.
// Drawn as geometry rather than as a post-process so it costs nothing per pixel elsewhere and behaves correctly
// under single-pass instanced stereo, where a screen-space effect would need care to stay eye-consistent.
Shader "4Dex/Vignette"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 1)
        [HideInInspector] _Aperture ("Aperture", Range(0, 1)) = 1
        _Softness ("Edge softness", Range(0.01, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" }

        Pass
        {
            Name "Vignette"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always      // always in front: it is on the face, not in the world
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Aperture;
                float _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;   // u = distance from the centre of the disc, 0..1
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float radius : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.radius = input.uv.x;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // Clear inside the aperture, opaque outside it, with a soft edge between.
                float alpha = smoothstep(_Aperture, min(_Aperture + _Softness, 1.001), input.radius);
                return half4(_Color.rgb, alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
