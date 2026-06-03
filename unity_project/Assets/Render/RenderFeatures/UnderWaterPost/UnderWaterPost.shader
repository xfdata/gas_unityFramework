///水底全屏效果(扭曲+焦散)
///by taecg
Shader "Hidden/UnderWaterPost"
{   
    // ZTest on
    HLSLINCLUDE
    // ZTest on
    #pragma target 3.5
    #pragma multi_compile_local _ _DISTORT_ON
    #pragma multi_compile_local _ _CAUSTIC_ON
    #pragma multi_compile_local _ _VIGNET_ON
    #pragma multi_compile_local _ _POLAR_ON
    #pragma multi_compile_local _ _SCAN_ON
    

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "Assets/Art/Shaders/ShaderLibs/Common.hlsl"

    half _DistortStrength,_DUNGEON;
    float4 _CausticOffsetTiling,_Limit,_DistColor;
    float3 _CausticFlow01,_DistTiling,_DistSpeed;
    float3 _CausticFlow02;
    float3 _CausticMaskFlow;
    half _CausticStrength;
    float4 _color,_ScanColor,_CenterParame,_SmoothSpeed,_TextureTil,_CausticColor;

    TEXTURE2D(_PreDistortTexture);
    SAMPLER(sampler_PreDistortTexture);
    TEXTURE2D(_CausticTexture);
    SAMPLER(sampler_CausticTexture);
    TEXTURE2D(_CausticMaskTexture);
    SAMPLER(sampler_CausticMaskTexture);
    TEXTURE2D(_FoamTexture);
    SAMPLER(sampler_FoamTexture);
    TEXTURE2D(_DistTexture);
    SAMPLER(sampler_DistTexture);
    TEXTURE2D(_ScanTexture);
    SAMPLER(sampler_ScanTexture);
    float remap(float value, float fromMin, float fromMax, float toMin, float toMax)
{
    return toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
}
    
    float2 RectToPolar(float2 uv, float2 centerUV) {
                uv = uv - centerUV;                 //改变中心 将中心从UV左下角移到UV中心
                float theta = atan2(uv.y, uv.x);    // atan()值域[-π/2, π/2]一般不用; atan2()值域[-π, π]确定一个完整的圆
				// theta = step(360,theta);
                float r = length(uv);               //UV上的某一点到我们确定的中心得距离
                return float2(theta, r);
            }

    half4 Frag(Varyings i) : SV_Target
    {
        half4 c = 1;

        float2 uv = i.texcoord;
        
        #if _DISTORT_ON
            half4 preDistortTexture = SAMPLE_TEXTURE2D(_PreDistortTexture, sampler_PreDistortTexture, i.texcoord);
            uv = i.texcoord + preDistortTexture.rg * _DistortStrength;
        #endif
            c.rgb = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

        #if UNITY_REVERSED_Z
            real depth = SampleSceneDepth(i.texcoord);
        #else
            real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(i.texcoord));
        #endif
        // return depth;
            float3 positionWS = ComputeWorldSpacePosition(i.texcoord, depth, UNITY_MATRIX_I_VP);
            float4 shadowCoord = TransformWorldToShadowCoord(half4(positionWS,1));
            // return shadowCoord;
            Light light = GetMainLight(shadowCoord);
            half3 depthNormal = SampleSceneNormals(i.texcoord);
            // half3 V = normalize(_WorldSpaceCameraPos - positionWS);
        // return shadow;
        #if _CAUSTIC_ON
                float2 Maskuv0 =  positionWS.xz/300 + _Time.x * 0.05 ;
	            float2 Maskuv1 =  positionWS.xz/330 - _Time.x * 0.032 ;
	            float2 Maskuv2 =  positionWS.xz/180 + float2(_Time.x * 0.059, -_Time.x * 0.027);
	            float Mask0 = SAMPLE_TEXTURE2D(_CausticTexture, sampler_CausticTexture,Maskuv0 * _CausticFlow01.xy ) ;
	            float Mask1 = SAMPLE_TEXTURE2D(_CausticTexture, sampler_CausticTexture,Maskuv1 * _CausticFlow01.xy ) ;
	            float Mask2 = SAMPLE_TEXTURE2D(_CausticTexture, sampler_CausticTexture,Maskuv2 * _CausticFlow01.xy) ;
                half Mask = Mask0 * Mask1 * Mask2 * 2;

            // float3 t = _Time.y * float3(_CausticFlow01.z, _CausticFlow02.z, _CausticMaskFlow.z);
            // float2 causticUV01 = positionWS.xz * _CausticFlow01.xy + positionWS.y * 0.03 + t.x;
            // half4 causticTex01 = SAMPLE_TEXTURE2D(_CausticTexture, sampler_CausticTexture, causticUV01);
            // float2 causticUV02 = positionWS.xz * _CausticFlow02.xy + positionWS.y * 0.01 + t.y;
            // half4 causticTex02 = SAMPLE_TEXTURE2D(_CausticTexture, sampler_CausticTexture, causticUV02);
            // half4 caustic = min(causticTex01, causticTex02);
            // float2 causticMaskUV = positionWS.xz * _CausticMaskFlow.xy + t.z;
            // half causticMask = SAMPLE_TEXTURE2D(_CausticMaskTexture, sampler_CausticMaskTexture, causticMaskUV);
            half groundMask = smoothstepsimple(-0.5, 0, positionWS.y);
            half depthMask = smoothstep(0.12,0, Linear01Depth(depth, _ZBufferParams));
            if (_DUNGEON)
            {
                depthMask = smoothstep(0.025,0.05, Linear01Depth(depth, _ZBufferParams));
            }
            
            
            // return depthMask;
            
            half lightMask = saturate(dot(light.direction, depthNormal));
            half mask = groundMask * depthMask * lightMask ;
                Mask *= mask;
            c.rgb += Mask * _CausticColor;

        #endif

            float fade = 1;
            fade = length(uv.xy-0.5);
            fade = smoothstep(_Limit.x , _Limit.x - _Limit.y,1-fade) * smoothstep(_Limit.z,_Limit.z + _Limit.w, 1- uv.y);
            c.rgb = lerp(c.rgb, c.rgb  * _color,fade);

          #if _POLAR_ON
                    
					float2 thetaR = RectToPolar(i.texcoord,float2(0.5,0.5));
					float2 Distuv = float2(
                    thetaR.x / 3.141593 * 0.5 + 0.5+ frac(_Time.x *_DistSpeed.y),    // θ映射到[0, 1]
                    thetaR.y + frac(_Time.x * _DistSpeed.x)    // r随时间流动
                );
                   // half DisMask =  SAMPLE_TEXTURE2D(_CausticMaskTexture, sampler_CausticMaskTexture, uv * half2(6,2)).r + half2(0.5,0.7);
                c += SAMPLE_TEXTURE2D(_DistTexture, sampler_DistTexture, Distuv * float2(_DistTiling.x,_DistTiling.y)) * smoothstep(_DistTiling.z,0.2,1-length(uv.xy-0.5)) * _DistColor * _DistColor.a;
                // c.a *= _DistColor.a;
               
          #endif

        #if _SCAN_ON
            half Scan = SAMPLE_TEXTURE2D(_ScanTexture, sampler_ScanTexture, positionWS.xz * _TextureTil.xy).r;
            half Distance = distance(positionWS,_CenterParame.xyz);
            half radius = remap(tan(_Time.y/_SmoothSpeed.y)*0.5+0.5,-1,1,0,60) ;
            // half radius1 = remap(tan(_Time.y/_SmoothSpeed.y - 0.5)*0.5+0.5,-1,1,0,60) ;
            float smt = smoothstep(radius,radius + _SmoothSpeed.x,Distance);
            // float smt1 = smoothstep(radius-1,radius +2  -1,Distance);
            if (smt == 1)
            {
                smt = 0;
            }
            // if (smt1 == 1)
            // {
            //     smt1 = 0;
            // }
            half3 addColor = _ScanColor.rgb * smt * Scan * _SmoothSpeed.z;
            // half nDv = dot(depthNormal,V);
            // half3 F = pow(1-nDv,6) * _ScanColor.rgb * smt1 ;
            c.rgb += addColor;
        // return pow(1-nDv,10);
        #endif
        
        return c;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "Queue"="Overlay" "RenderPipeline" = "UniversalPipeline"
        }
        ZTest Always ZWrite Off Cull off

        Pass
        {
            Name "Distort"
//            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            ENDHLSL
        }
    }

    CustomEditor "taecg.tools.CustomShaderGUI"
}