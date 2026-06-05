Shader "Custom/EndoscopeFisheye"
{
    Properties
    {
        _MainTex ("Render Texture", 2D) = "white" {}
        _FisheyeStrength ("Fisheye Strength", Range(0.0, 1.0)) = 0.4
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.1)) = 0.02
        _Vignette ("Vignette Strength", Range(0.0, 1.0)) = 0.4
        _Brightness ("Brightness", Range(0.0, 2.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float _FisheyeStrength;
            float _EdgeSoftness;
            float _Vignette;
            float _Brightness;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // UV van midden af meten (-0.5 tot 0.5)
                float2 uv = i.uv - 0.5;

                // Afstand tot midden
                float dist = length(uv);

                // Alles buiten de cirkel weggooien
                if (dist > 0.5) return fixed4(0, 0, 0, 0);

                // Fisheye vervorming
                float2 normDir = normalize(uv);
                float r = dist / 0.5;
                float fishR = asin(r * _FisheyeStrength) / (_FisheyeStrength * UNITY_PI * 0.5);
                float2 fishUV = normDir * fishR * 0.5 + 0.5;

                // Interpoleer tussen normaal en fisheye
                float2 finalUV = lerp(i.uv, fishUV, _FisheyeStrength);

                // Sample de texture
                fixed4 col = tex2D(_MainTex, finalUV);

                // Vignette
                float vignette = 1.0 - smoothstep(0.3, 0.5, dist) * _Vignette;
                col.rgb *= vignette * _Brightness;

                // Zachte rand van de cirkel
                float circle = 1.0 - smoothstep(0.5 - _EdgeSoftness, 0.5, dist);
                col.a = circle;

                return col;
            }
            ENDCG
        }
    }
}
