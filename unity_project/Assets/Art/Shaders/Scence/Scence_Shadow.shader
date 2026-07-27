Shader "ONEMT/Scence/Scence_Shadow"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        [Enum(Off,0,On,1)]_ZWrite("_ZWrite",int) = 1

    }
    SubShader
    {
       Tags{"Queue"="Transparent+1" "RenderPipeline" = "UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha
	    Zwrite [_ZWrite]
        Pass
        {    Tags
            {
                "LightMode" = "TextureShadow"
            }
//               stencil
//            {
//                Ref 1
//                Comp Greater
//            }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            // #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            // half4 _BaseMap_ST;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                // UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                // UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv.xy);
                // half4 c;
                // c.rgb = baseMap.rgb;
                // c.a = baseMap.a;
                clip(baseMap.a - 0.01);
                return baseMap;
            }
            ENDHLSL
        }
    }
}
