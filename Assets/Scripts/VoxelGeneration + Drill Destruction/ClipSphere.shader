// ClipSphere.shader
// Apply this to the skinned mesh material to punch a hole in it
// at the drill contact point. The voxel patch fills that hole.
//
// Usage:
//   1. Create a new Material using this shader
//   2. Assign it to your skinned mesh renderer
//   3. The _ClipCenter and _ClipRadius properties are set automatically
//      by VoxelizedModelExample.UpdateClipShader() at runtime

Shader "Custom/ClipSphere"
{
    Properties
    {
        _BaseColor  ("Color", Color) = (1,1,1,1)
        _BaseMap    ("Albedo (RGB)", 2D) = "white" {}
        _ClipCenter ("Clip Center (World)", Vector) = (0,0,0,0)
        _ClipRadius ("Clip Radius", Float) = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float3 _ClipCenter;
                float  _ClipRadius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Discard fragments inside the clip sphere
                float dist = distance(IN.positionWS, _ClipCenter);
                clip(dist - _ClipRadius);

                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                return tex * _BaseColor;
            }
            ENDHLSL
        }
    }
}