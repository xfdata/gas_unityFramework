//Kawase模糊
//by taecg
Shader "Hidden/taecg/PostProcessing/KawaseBlur"
{
    Properties
    {
        _MainTex("MainTex", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline"}
        ZTest Always 
        ZWrite Off 
        Cull Off

        Pass
        {
            Name "KawaseBlur"
            Tags {"LightMode" = "SRPDefaultUnlit"}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS           : POSITION;
                float2 uv                   : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS           : SV_POSITION;
                float2 uv                   : TEXCOORD0;
                float4 uv01                 : TEXCOORD1;
                float4 uv23                 : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
            CBUFFER_END
            half _Strength;
            TEXTURE2D (_MainTex);SAMPLER(sampler_MainTex);

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                float4 offset = (_ScreenParams.zwzw-1) * _Strength;
                o.uv01 = v.uv.xyxy + offset * float4(-1,-1,1,-1);
                o.uv23 = v.uv.xyxy + offset * float4(-1,1,1,1);
                return o;
            }

            half4 frag(Varyings i) : SV_TARGET
            {
                half4 c = 0;
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv01.xy);
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv01.zw);
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv23.xy);
                c += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv23.zw);
                c *= 0.25;
                return c;
            }
            ENDHLSL
        }
    }
}
