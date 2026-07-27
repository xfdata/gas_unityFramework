Shader "ONEMT/Scence/UnderSoil"
{
    Properties
    {   
        //: { "type": "group","label":"基础"}
        //: { "type": "texture2","prop":"_BaseMap","label":"基础纹理"}
        _BaseMap("基础纹理", 2D) = "white" {}
        //: { "type": "float","prop":"_OutlineParams", "label":"整体明度","vectorComponent1":"y","min":"0","max":"1"} 
        _OutlineParams("",vector) = (1,1,0,0)
         //: { "type": "float","prop":"_BuildFlashLighting", "label":"程序加的明度","min":"0","max":"1"}
        _BuildFlashLighting("",Float) = 0
        
         //: { "type": "groupVariant","label":"渐显渐隐","keyword":"_TRANSPARENTLY_ON"}
        //: { "type": "float","prop":"_Clip", "label":"渐隐度","min":"0","max":"1"}
        _Clip("",float) = 0

         //: { "type": "groupVariant","label":"警告开关","keyword":"_WARNINGS_ON"}
         //: { "type": "color","prop":"_AddColor","label":"叠加颜色"}
        [HDR] _AddColor("_AddColor",color) = (0,0,0,0)
        
        //: { "type": "groupVariant","label":"自发光","keyword":"_EMISSION_ON"}
        //: { "type": "texture2","prop":"_EmissionMap","label":"自发光遮罩"}
        _EmissionMap("基础纹理", 2D) = "white" {}
        //: { "type": "color","prop":"_EmissionColorr","label":"r自发光颜色"}
        //: { "type": "color","prop":"_EmissionColorg","label":"g自发光颜色"}
        //: { "type": "color","prop":"_EmissionColorb","label":"b自发光颜色"}
        //: { "type": "color","prop":"_EmissionColora","label":"a自发光颜色"}
        [HDR] _EmissionColorr("_EmissionColor",color) = (1,1,1,1)
        [HDR] _EmissionColorg("_EmissionColor",color) = (1,1,1,1)
        [HDR] _EmissionColorb("_EmissionColor",color) = (1,1,1,1)
        [HDR] _EmissionColora("_EmissionColor",color) = (1,1,1,1)

        //: { "type": "groupVariant","label":"matcap","keyword":"_MATCAP_ON"}
         //: { "type": "texture2","prop":"_Matcap","label":"matcap纹理"}
         //: { "type": "float","prop":"_OutlineParams", "label":"matcap强度","vectorComponent1":"x","min":"0","max":"2"} 
         _Matcap("_Matcap",2D) = "white" {}
        _OutlineParams("",vector) = (1,0.65,0,0)

        //: { "type": "groupVariant","label":"高光","keyword":"_SPECULAR_ON"}
        //: { "type": "info","label":"高光遮罩在_BaseMap的A通道."}
        //: { "type": "color","prop":"_specularColor","label":"颜色"}
        [HDR] _specularColor("_specularColor",color) = (1,1,1,1)
        //: { "type": "float","prop":"_OutlineParams", "label":"粗糙度","vectorComponent1":"z","min":"0","max":"1"}
        //: { "type": "float","prop":"_OutlineParams", "label":"高光强度","vectorComponent1":"w","min":"0","max":"2"}
        _OutlineParams("",vector) = (0.5,0.65,0,0)
        
       
    }
    SubShader
    {
        Tags{"Queue"="Geometry" "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma shader_feature_local _ _MATCAP_ON
            #pragma shader_feature_local _ _FLASH_LIGHT_ON
            #pragma shader_feature_local _ _SPECULAR_ON
            #pragma shader_feature_local _ _WARNINGS_ON
            #pragma shader_feature_local _ _TRANSPARENTLY_ON
            #pragma shader_feature_local _ _EMISSION_ON


            CBUFFER_START(UnityPerMaterial)
                half4 _BaseMap_ST,_OutlineParams,_Inst,_specularColor,_Noise_ST,_AddColor;
                half _Clip,_BuildFlashLighting;
                half4 _EmissionColorr,_EmissionColorg,_EmissionColorb,_EmissionColora;
            CBUFFER_END
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Noise);SAMPLER(sampler_Noise);
            TEXTURE2D(_Matcap);SAMPLER(sampler_Matcap);
            TEXTURE2D(_EmissionMap);SAMPLER(sampler_EmissionMap);

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half3 normal  : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                half3 normalWS    : TEXCOORD1;
                half3 positionWS   : TEXCOORD2;
                float3 positionRoot : TEXCOORD3;
                float4 positionCS : SV_POSITION;
            };

             half BRDF_DTerm(float NdotH, float i_roughness) {
                //DGGX =  a^2 / π((a^2 – 1) (n · h)^2 + 1)^2
                float a2 = i_roughness * i_roughness;
                float val = ((a2 - 1) * (NdotH * NdotH) + 1);
                return a2 / (PI * (val * val));
            }
            half BRDF_GTerm(float NdotL, float NdotV, float i_roughness) {
                //G(l,v,h)=1/(((n·l)(1-k)+k)*((n·v)(1-k)+k))
                float k = i_roughness * i_roughness / 2;
                return 0.5 / ((NdotL * (1 - k) + k) + (NdotV * (1 - k) + k));
            }

            half Dither4x4(uint2 uv)
            {
                uv %= 4;
                const float A4x4[16] = {
                    0,8,2,10,
                    12,4,14,6,
                    3,11,1,9,
                    15,7,13,5 
                };
                return A4x4[uv.x*4+uv.y]/17;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                // o.fogCoord = ComputeFogFactor(o.positionCS.z);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = normalize(TransformObjectToWorldNormal(v.normal));
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {

                half4 BaseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                // BaseMap.rgb = MixFog(BaseMap.rgb,i.fogCoord);
                half4 c = BaseMap;
                #if _MATCAP_ON
                    
                    half3 normalVS = normalize(TransformWorldToViewDir(i.normalWS));
                    half4 Matcap = SAMPLE_TEXTURE2D(_Matcap, sampler_Matcap, normalVS.xy * 0.5 + 0.5) *_OutlineParams.x;                    
                    c +=  Matcap * BaseMap.a;
                #endif
                
                #if _SPECULAR_ON
                    Light light = GetMainLight();
                    half3 L = normalize(light.direction);
                    half3 V = normalize(_WorldSpaceCameraPos - i.positionWS);
                    half3 H = normalize(L + V);
                    float NdotH = saturate(dot(i.normalWS,H));
                    float NdotL = saturate(dot(i.normalWS,L));
                    float NdotV = saturate(dot(i.normalWS,V));
                    float LdotH = saturate(dot(L,H));
                
                    // half4 Noise = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, i.uv* _Noise_ST.xy + _Noise_ST.zw);
                    float roughness = 1.0 - _OutlineParams.z;
                    roughness = max(roughness, 0.002);
                    float roughness2 = roughness * roughness;
                    //d项
                    half dCol = BRDF_DTerm(NdotH, roughness2);
                    ////G项
                    half gTerm = BRDF_GTerm(NdotL, NdotV, roughness2);
                    ////F项 菲涅尔
                    // half3 frenCol = BRDF_FresnelTerm(_specularColor.rgb, LdotH);
                    float specularPBL = dCol * gTerm * PI ;
                    //不会为负数
                    specularPBL = max(0, specularPBL * NdotL);
                    //any 参数里的任意一个元素不为零
                    // specularPBL *= any(_specularColor.rgb) ? 1.0 : 0.0;
                    c += float4(specularPBL * _specularColor.rgb * _OutlineParams.w,1) * BaseMap.a ;//* Noise
                    // return Noise;
                #endif
                    
                {
                     #if _TRANSPARENTLY_ON
                        uint2 uv1 = (uint2)i.positionCS.xy;
                        half dither2x2_Array = Dither4x4(uv1);
                        clip(dither2x2_Array -_Clip);
                    #endif
                 }
                c.a = 1;
                
                #if _WARNINGS_ON
                    c += _AddColor * saturate(sin(_Time.y*3) *0.4+0.6);
                #endif
                 #if _EMISSION_ON
                    half4 EmissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv);
                    c.rgb += _EmissionColorr.rgb *  EmissionMap.r + _EmissionColorg.rgb *  EmissionMap.g + _EmissionColorb.rgb *  EmissionMap.b +_EmissionColora.rgb *  EmissionMap.a;
                #endif
                #if _FLASH_LIGHT_ON
                    return min(1,c * _OutlineParams.y * _BuildFlashLighting);
                # else
                    return min(1,c * _OutlineParams.y);
                #endif
               
            }
            ENDHLSL
        }
        //        Pass
        // {
        //     Name "DepthOnly"
        //     Tags
        //     {
        //         "LightMode" = "DepthOnly"
        //     }

        //     // -------------------------------------
        //     // Render State Commands
        //     ZWrite On
        //     ColorMask R
        //     Cull[_Cull]

        //     HLSLPROGRAM
        //     #pragma target 2.0

        //     // -------------------------------------
        //     // Shader Stages
        //     #pragma vertex DepthOnlyVertex
        //     #pragma fragment DepthOnlyFragment

        //     // -------------------------------------
        //     // Material Keywords
        //     #pragma shader_feature_local _ALPHATEST_ON
        //     #pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

        //     // -------------------------------------
        //     // Unity defined keywords
        //     #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

        //     //--------------------------------------
        //     // GPU Instancing
        //     #pragma multi_compile_instancing
        //     #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

        //     // -------------------------------------
        //     // Includes
        //     #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
        //     #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
        //     ENDHLSL
        // }
        //   Pass
        // {
        //     Name "DepthNormals"
        //     Tags
        //     {
        //         "LightMode" = "DepthNormals"
        //     }

        //     ZWrite On
        //     Cull[_Cull]

        //     HLSLPROGRAM
        //     #pragma target 2.0

        //     // -------------------------------------
        //     // Shader Stages
        //     #pragma vertex DepthNormalsVertex
        //     #pragma fragment DepthNormalsFragment

        //     #pragma multi_compile_instancing
        //     #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"  
        //     #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        //     struct Attributes
        //     {
        //         float4 positionOS   : POSITION;
        //         float2 texcoord     : TEXCOORD0;
        //         float3 normal       : NORMAL;
        //         UNITY_VERTEX_INPUT_INSTANCE_ID
        //     };

        //     struct Varyings
        //     {
        //         float4 positionCS      : SV_POSITION;
        //         half3 normalWS    : TEXCOORD2;
        //         half3 viewDir     : TEXCOORD3;
        //         UNITY_VERTEX_INPUT_INSTANCE_ID
        //         UNITY_VERTEX_OUTPUT_STEREO
        //     };
        //     Varyings DepthNormalsVertex(Attributes v)
        //     {
        //         Varyings o = (Varyings)0;
        //         UNITY_SETUP_INSTANCE_ID(v);
        //         UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);
        //         o.positionCS = TransformObjectToHClip(v.positionOS.xyz);

        //         VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
        //         VertexNormalInputs normalInput = GetVertexNormalInputs(v.normal);
        //         o.normalWS = half3(NormalizeNormalPerVertex(normalInput.normalWS));

        //         return o;
        //     }
        //     void DepthNormalsFragment(Varyings input, out half4 outNormalWS : SV_Target0)
        //     {
        //         UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        //         half3 normalWS = input.normalWS;

        //         normalWS = NormalizeNormalPerPixel(normalWS);
        //         outNormalWS = half4(normalWS, 0.0);
               
        //     }
        //     ENDHLSL
        // }
    }
    CustomEditor "taecg.tools.CustomShaderGUI"

}
