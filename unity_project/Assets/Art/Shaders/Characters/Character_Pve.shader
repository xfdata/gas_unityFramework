//角色低模风格化
//by taecg
Shader "ONEMT/Character/Character_Pve"
{
    Properties
    {
        //: { "type": "group","label":"基础"}
        //: { "type": "texture2","prop":"_BaseMap","prop2":"_BaseColor", "label":"基础纹理"}
        _BaseColor("Color", Color) = (1,1,1,1)
        _BaseMap("Albedo", 2D) = "white" {}
        //: { "type": "ztest","prop":"_ZTest"}
		[Enum(UnityEngine.Rendering.CompareFunction)]_ZTest("ZTest",int) = 4
        
        //: { "type": "group","label":"受击"}
        //: { "type": "color","prop":"_HitColor","label":"受击颜色"}
        _HitColor("ShadowColor", Color) = (1,1,1,1)
        //: { "type": "float","prop":"_HitParams","label":"受击程度"，"min":"0","max":"1"}
        _HitParams("",Range(0,1)) = 0
        
        //: { "type": "group","label":"暗面"}
        //: { "type": "color","prop":"_ShadowColor","label":"颜色"}
        _ShadowColor("ShadowColor", Color) = (0.5,0.6,0.8,1)
        //: { "type": "minmax","prop":"_ShadowParams", "label":"范围","vectorComponent1":"x","vectorComponent2":"y","min":"0","max":"1"}
        //: { "type": "float","prop":"_ShadowParams","label":"亮暗差异"，"vectorComponent1":"z","min":"0","max":"1"}
        _ShadowParams("",Vector) = (0.43,0.48,0.4,0)

        //: { "type": "groupVariant","label":"法线","keyword":"_BUMP_ON"}
        //: { "type": "texture2","prop":"_BumpMap", "label":"法线纹理"}
        _BumpMap("_BumpMap", 2D) = "bump" {}
        
        //: { "type": "groupVariant","label":"冰冻","keyword":"_ICE_ON"}
         //: { "type": "texture2","prop":"_IceMatcap", "label":"冰冻纹理"}
        _IceMatcap("_BumpMap", 2D) = "white" {}
        //: { "type": "color","prop":"_IceColor","label":"冰冻颜色"}
        [HDR]_IceColor("", Color) = (1,1,1,1)
        //: { "type": "minmax","prop":"_Params03", "label":"冰锥范围","vectorComponent1":"x","vectorComponent2":"y","min":"0","max":"1"}
        //: { "type": "float","prop":"_Params03", "label":"冰锥强度","vectorComponent1":"z","min":"0","max":"1"}
        //: { "type": "float","prop":"_Params03", "label":"冰冻范围","vectorComponent1":"w","min":"0","max":"5"}
        _Params03("",vector) = (0.02,0.3,0,0)
       
        
        
        //: { "type": "group","label":"高光"}
        //: { "type": "texture2","prop":"_SpecularMaskMap", "label":"高光遮罩(R)"}
        _SpecularMaskMap("",2D) = "white"{}
        //: { "type": "float","prop":"_SpecularParams", "label":"强度","vectorComponent1":"x"}
        //: { "type": "float","prop":"_SpecularParams", "label":"扩散","vectorComponent1":"y"}
        _SpecularParams("",vector) = (1,5,0,0)

        //: { "type": "group","label":"外发光"}
        //: { "type": "color","prop":"_RimColor","label":"颜色"}
        _RimColor("",color) = (1,1,1,1)
        //: { "type": "minmax","prop":"_RimParams", "label":"范围","vectorComponent1":"x","vectorComponent2":"y","min":"0","max":"1"}
        //: { "type": "float","prop":"_RimParams", "label":"强度","vectorComponent1":"z"}
        //: { "type": "float","prop":"_RimParams", "label":"衰减","vectorComponent1":"w"}
        _RimParams("",vector) = (0,1,1,1)

        //: { "type": "groupVariant","label":"自发光","keyword":"_EMISSIVE_ON"}
         //: { "type": "toggle","prop":"_EmissiveParams", "label":"是否用flowmap","vectorComponent1":"z"}
        _EmissiveParams("",vector) = (1,0,0,0)
        //: { "type": "texture1","prop":"_EmissiveMap", "label":"自发光颜色(RGB)"."st":"true"}
        _EmissiveMap("", 2D) = "black"{}
        //: { "type": "texture1","prop":"_FlowMap", "label":"_FlowMap"}
        _FlowMap("", 2D) = "black"{}
        //: { "type": "float","prop":"_EmissiveParams", "label":"强度","vectorComponent1":"x"}
        //: { "type": "float","prop":"_EmissiveParams", "label":"flowmap流动速度","vectorComponent1":"y","min":"0","max":"30"}
        _EmissiveParams("",vector) = (1,0,0,0)

        //: { "type": "group","label":"自定义光照"}
        //: { "type": "color","prop":"_CustomLightColor","label":"颜色"}
        [HDR]_CustomLightColor("",Color) = (0,0,0,0)
        //: { "type": "vector3","prop":"_LightCenter", "label":"光照中心","vectorComponent1":"x","vectorComponent2":"y","vectorComponent3":"z"}
        //: { "type": "float","prop":"_LightCenter", "label":"半径","vectorComponent1":"w","min":"0","max":"10"}
        _LightCenter("",vector) = (0.5,0.65,0,0)

        //: { "type": "groupVariant","label":"渐显渐隐","keyword":"_TRANSPARENTLY_ON"}
        //: { "type": "float","prop":"_Clip", "label":"透明度","vectorComponent1":"x","min":"0","max":"1"}
        _Clip("",vector) = (0,0,0,0)

        
        //: { "type": "group","label":"渲染状态"}
        //: { "type": "toggle","prop":"_Clip", "label":"GPUSKINNING","vectorComponent1":"y"}
        _Clip("",vector) = (0,0,0,0)
        //: { "type": "queue"}
        //: { "type": "gpuInstancing"}
        
    }
    SubShader
    {
        Tags
        {
            "Queue"="Geometry" "RenderPipeline" = "UniversalPipeline"
        }
        ZTest [_ZTest]

        Pass
        {
            Name "ForwardLit"
//            Tags
//            {
//                "LightMode" = "NPC"
//            }
            
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag 
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _EMISSIVE_ON
            #pragma shader_feature_local _ _TRANSPARENTLY_ON
            #pragma shader_feature_local _ _BUMP_ON
            #pragma shader_feature_local _ _GPUSKINBLEND
            #pragma shader_feature _ _GPUSKIN_BLEND_ON
            #pragma multi_compile _ _ICE_ON
            #define GPUSKIN_BLEND_ON
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            #include "Assets/ThirdParty/GPUSkinning/Shader/GPUSkinningInclude.hlsl"
           

            #define STEP 2  //色阶层数

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor,_EmissiveMap_ST,_Params03;
                half4 _ShadowColor, _ShadowParams;
                half4 _SpecularParams;
                half4 _RimColor, _RimParams;
                half4 _CustomLightColor, _LightCenter,_EmissiveParams,_Clip,_IceColor;
                half _NdL;
            CBUFFER_END
              UNITY_INSTANCING_BUFFER_START(PerInstance)
              
                UNITY_DEFINE_INSTANCED_PROP(float4, _HitColor)
                UNITY_DEFINE_INSTANCED_PROP(float,  _HitParams)
           
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_SpecularMaskMap);
            SAMPLER(sampler_SpecularMaskMap);
            TEXTURE2D(_EmissiveMap);
            SAMPLER(sampler_EmissiveMap);
            TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
            TEXTURE2D(_FlowMap);SAMPLER(sampler_FlowMap);
            TEXTURE2D(_IceMatcap);SAMPLER(sampler_IceMatcap);
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord0 : TEXCOORD0;
                half4 normalOS : NORMAL;
                float4 uv2 : TEXCOORD1;
                float4 uv3 : TEXCOORD2;
                half4 tangentOS : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 viewWS:TEXCOORD2;
                half3 normalWS : TEXCOORD3;
                half fogCoord : TEXCOORD4;
                half4 tangentWS     : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
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

            half3 shpereMask(half3 pos,half radius,half3 center,half Hardness)
            {
                half dis = distance(pos,center);
                return smoothstep(radius + Hardness,radius,dis); 
            }

            Varyings vert(Attributes v)
            {   
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o = (Varyings)0;
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float4 pos = lerp(v.positionOS,skin2(v.positionOS, v.uv2, v.uv3),_Clip.y);
                o.uv = v.texcoord0;
                o.positionWS = TransformObjectToWorld(pos.xyz);
                o.normalWS = TransformObjectToWorldNormal(lerp(v.normalOS,skin2(v.normalOS, v.uv2, v.uv3),_Clip.y));

                // #if _ICE_ON
                //     half freezeRange = smoothstep(_Params03.y, _Params03.x, o.normalWS.y * 0.5 + 0.5);
                //     o.positionWS.y = max(o.positionWS.y - freezeRange * _Params03.z, 0);
                // #endif
                o.positionCS = TransformWorldToHClip(o.positionWS.xyz);
                o.viewWS = normalize(_WorldSpaceCameraPos.xyz - o.positionWS);
                // o.normalWS = TransformObjectToWorldNormal(v.normalOS.xyz);
                half sign = v.tangentOS.w * GetOddNegativeScale();
                // o.normalWS = TransformObjectToWorldNormal(v.normal);
                o.tangentWS = half4(TransformObjectToWorldDir(v.tangentOS.xyz),sign);

                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float4 frag(Varyings i) : COLOR
            {   
                UNITY_SETUP_INSTANCE_ID(i);
                
                half4 c = 1;
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv.xy);
                c = baseMap * _BaseColor;
                half3 N = normalize(i.normalWS);
                #if _BUMP_ON
                    half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv));
                    float3 bitangent = i.tangentWS.w * cross(i.normalWS.xyz, i.tangentWS.xyz);
                    N = normalize(mul(normalTS, half3x3(i.tangentWS.xyz, bitangent.xyz, i.normalWS.xyz)));
                #endif
                
                Light light = GetMainLight();
                half3 L = light.direction;
                
                half3 V = normalize(i.viewWS);
                half3 H = normalize(L + V);
                half NoL = dot(N, L) * (1-_ShadowParams.z) + _ShadowParams.z;
                // return NoL;
                half NoH = saturate(dot(N, H));
                half NoV = dot(N, V) * 0.5 + 0.5;
                c = (c + c) * NoL;

                //明暗色阶
                {
                    half3 shadow = smoothstep(_ShadowParams.x, _ShadowParams.y, NoL);
                    shadow = saturate(shadow + _ShadowColor.rgb);
                    c.rgb *= shadow;
                }
               

                // 高光
                {
                    half3 specular = _SpecularParams.x * pow(NoH, _SpecularParams.y);
                    half4 specularMaskMap = SAMPLE_TEXTURE2D(_SpecularMaskMap, sampler_SpecularMaskMap, i.uv);
                    specular *= specularMaskMap.rgb;
                    c.rgb += specular;
                }

                //外发光
                {
                    half rim = smoothstep(_RimParams.y, _RimParams.x, NoV);
                    rim = saturate(_RimParams.z * pow(rim, _RimParams.w));
                    c.rgb += rim * _RimColor.rgb;
                }

                #if _ICE_ON
                    half3 normalVS = mul((float3x3)UNITY_MATRIX_V, N);
                    float2 normalUV = normalVS.xy * 0.5 + 0.5;
                    half3 IceMatcap = SAMPLE_TEXTURE2D(_IceMatcap, sampler_IceMatcap, normalUV).rgb;
                    c.rgb = lerp(c.rgb,IceMatcap * _IceColor, pow(1-NoV, _Params03.w));
                #endif
                
 
                {
                    //自发光
                    #if _EMISSIVE_ON
                        half4 emissiveMap = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_EmissiveMap, i.uv);
                        if (_EmissiveParams.z )
                        {
                             half4 FlowMap = SAMPLE_TEXTURE2D(_FlowMap,sampler_FlowMap,i.uv)*2-1;
                            float phase0 = frac(_Time.x*0.1 * _EmissiveParams.y);
                            float phase1 = frac(_Time.x*0.1 * _EmissiveParams.y+0.5);
                            half4 emissiveMap0 = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_EmissiveMap, (i.uv * _EmissiveMap_ST.xy + _EmissiveMap_ST.zw) - FlowMap.xy * phase0);
                            half4 emissiveMap1 = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_EmissiveMap, (i.uv * _EmissiveMap_ST.xy + _EmissiveMap_ST.zw) - FlowMap.xy * phase1);
                            float flowLerp = abs((0.5-phase0)/0.5);
                            emissiveMap = lerp(emissiveMap0,emissiveMap1,flowLerp);
                            // return  0;
                        }
                    
                    // return emissiveMap;

                    c.rgb += emissiveMap.rgb * baseMap.a * _EmissiveParams.x ;
                    clip(emissiveMap.a - 0.1);
                    #endif
                }
                {
                    //自定义关照
                    half3 CoustomL = normalize(_LightCenter.xyz - i.positionWS);
                    half CoustomNoL = saturate(dot(N,CoustomL)) * 0.5+0.5;
                    half3 CustomColor = c.rgb * shpereMask(i.positionWS,_LightCenter.w,half3(_LightCenter.xyz),1) * _CustomLightColor.rgb * CoustomNoL;
                    c.rgb += CustomColor;
                    // return CoustomNoL;
                }
                #if _TRANSPARENTLY_ON
                uint2 uv1 = (uint2)i.positionCS.xy;
                half dither2x2_Array = Dither4x4(uv1);
                clip(dither2x2_Array -_Clip.x);
                #endif
                half4 inst_HitColor = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _HitColor);
                half inst_HitParams = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _HitParams);
                c.rgb = lerp(c.rgb,inst_HitColor.rgb,inst_HitParams);
                c.rgb = MixFog(c.rgb, i.fogCoord);
                c.a = 1;
                return c;
            }
            ENDHLSL
        }

        // Pass
        // {
        //     Name "ShadowCaster"
        //     Tags
        //     {
        //         "LightMode" = "ShadowCaster"
        //     }

        //     // -------------------------------------
        //     // Render State Commands
        //     ZWrite On
        //     ZTest LEqual
        //     ColorMask 0
        //     Cull[_Cull]

        //     HLSLPROGRAM
        //     #pragma target 2.0

        //     // -------------------------------------
        //     // Shader Stages
        //     #pragma vertex ShadowPassVertex
        //     #pragma fragment ShadowPassFragment

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

        //     // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
        //     #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

        //     // -------------------------------------
        //     // Includes
        //     #include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
        //     #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
        //     ENDHLSL
        // }
        pass {
			Tags{ "LightMode" = "ShadowCaster" }
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
            #pragma multi_compile_instancing
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
             #include "Assets/ThirdParty/GPUSkinning/Shader/GPUSkinningInclude.hlsl"
			
            CBUFFER_START(UnityPerMaterial)
                half4 _Clip;
            CBUFFER_END
			struct appdata
			{
				float4 vertex : POSITION;
                float2 texcoord0 : TEXCOORD0;
                float4 uv2 : TEXCOORD1;
                float4 uv3 : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};
 
			struct v2f
			{
				float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};
 
			v2f vert(appdata v)
			{   
                UNITY_SETUP_INSTANCE_ID(v);
				v2f o;
                UNITY_TRANSFER_INSTANCE_ID(v, o);
				// o.pos = mul(UNITY_MATRIX_MVP,v.vertex);
                float4 pos = lerp(v.vertex,skin2(v.vertex, v.uv2, v.uv3),_Clip.y);
                o.pos = TransformObjectToHClip(pos);
				return o;
			}
			float4 frag(v2f i) : SV_Target
			{   
                UNITY_SETUP_INSTANCE_ID(i);
				float4 color;
				color.xyz = float3(0.0, 0.0, 0.0);
				return color;
			}
			ENDHLSL
		}
//                Pass
//        {
//            Name "DepthNormals"
//            Tags
//            {
//                "LightMode" = "DepthNormals"
//            }
//
//            ZWrite On
//            Cull[_Cull]
//
//            HLSLPROGRAM
//            #pragma target 2.0
//
//            // -------------------------------------
//            // Shader Stages
//            #pragma vertex DepthNormalsVertex
//            #pragma fragment DepthNormalsFragment
//
//            #pragma multi_compile_instancing
//            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"  
//            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
//            struct Attributes
//            {
//                float4 positionOS   : POSITION;
//                float2 texcoord     : TEXCOORD0;
//                float3 normal       : NORMAL;
//                UNITY_VERTEX_INPUT_INSTANCE_ID
//            };
//
//            struct Varyings
//            {
//                float4 positionCS      : SV_POSITION;
//                half3 normalWS    : TEXCOORD2;
//                half3 viewDir     : TEXCOORD3;
//                UNITY_VERTEX_INPUT_INSTANCE_ID
//                UNITY_VERTEX_OUTPUT_STEREO
//            };
//            Varyings DepthNormalsVertex(Attributes v)
//            {
//                Varyings o = (Varyings)0;
//                UNITY_SETUP_INSTANCE_ID(v);
//                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);
//                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
//
//                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
//                VertexNormalInputs normalInput = GetVertexNormalInputs(v.normal);
//                o.normalWS = half3(NormalizeNormalPerVertex(normalInput.normalWS));
//
//                return o;
//            }
//            void DepthNormalsFragment(Varyings input, out half4 outNormalWS : SV_Target0)
//            {
//                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
//                half3 normalWS = input.normalWS;
//
//                normalWS = NormalizeNormalPerPixel(normalWS);
//                outNormalWS = half4(normalWS, 0.0);
//               
//            }
//            ENDHLSL
//        }
    }
    CustomEditor "taecg.tools.CustomShaderGUI"
}