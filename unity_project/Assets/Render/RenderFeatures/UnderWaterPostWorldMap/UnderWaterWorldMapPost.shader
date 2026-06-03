///水底全屏效果(扭曲+焦散)
///by taecg
Shader "Hidden/UnderWaterPostWorldMap"
{   
    // ZTest on
    HLSLINCLUDE
    // ZTest on
    #pragma target 3.5
    #pragma multi_compile_local _ _DISTORT_ON
    #pragma multi_compile_local _ _CAUSTIC_ON
    #pragma multi_compile_local _ _VIGNET_ON
    //  #pragma multi_compile_local _ _Caustic_ON
    

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "Assets/Art/Shaders/ShaderLibs/Common.hlsl"
    // #pragma multi_compile _ _MAIN_LIGHT_SHADOWS                    //接受阴影
    // #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE            //产生阴影
    // #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
    // #pragma multi_compile _ _SHADOWS_SOFT                         //软阴影 

    half _DistortStrength,_FoamLimit,_itereat;
    float4 _CausticOffsetTiling,_Position,_Limit;
    float3 _CausticFlow01;
    float3 _CausticFlow02;
    float3 _CausticMaskFlow;
    half _CausticStrength,_TimeX;
    float4 _color,_DepthColor;
    float _DefocusStrength;

    TEXTURE2D(_Distort);
    SAMPLER(sampler_Distort);
    TEXTURE2D(_CausticTexture);
    SAMPLER(sampler_CausticTexture);
    TEXTURE2D(_CausticMaskTexture);
    SAMPLER(sampler_CausticMaskTexture);
    TEXTURE2D(_FoamTexture);
    SAMPLER(sampler_FoamTexture);
    TEXTURE2D(_Mask);
    SAMPLER(sampler_Mask);
    // TEXTURE2D(_Cloud);
    // SAMPLER(sampler_Cloud);
    float Random1DTo1D(float value,float a,float b)
    {
	//make value more random by making it bigger
	float random = frac(sin(value+b)*a);
        return random;
    }

    half4 Frag(Varyings i) : SV_Target
    {
        half4 c = 1;
        float fade = 1;
        float2 uv = i.texcoord;

        fade = length(uv.xy-0.5);
        fade = smoothstep(_Limit.x , _Limit.x - _Limit.y,1-fade) * smoothstep(_Limit.z,_Limit.z + _Limit.w,uv.y);

        // return fade;
        //#if _DISTORT_ON
            half4 Distort = SAMPLE_TEXTURE2D(_Distort, sampler_Distort, i.texcoord + _Time.x );
            uv = i.texcoord + Distort.rg * _DistortStrength;
        //#endif
        
        c.rgb = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp,uv);

        #if UNITY_REVERSED_Z
            real depth = SampleSceneDepth(i.texcoord);
        #else
            real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(i.texcoord));
        #endif

            float3 positionWS = ComputeWorldSpacePosition(i.texcoord, depth, UNITY_MATRIX_I_VP);

           
            // Light light = GetMainLight();

         #if _CAUSTIC_ON
        // return half4(positionWS,1);
            float3 t = _Time.y * float3(_CausticFlow01.z, _CausticFlow02.z, _CausticMaskFlow.z);
            float2 causticUV01 = positionWS.xz * _CausticFlow01.xy + positionWS.y * 0.03 + t.x + Distort.rg * sin(0.1);
            half4 causticTex01 = SAMPLE_TEXTURE2D(_CausticTexture, sampler_CausticTexture, causticUV01);
            float2 causticUV02 = positionWS.xz * _CausticFlow02.xy - positionWS.y * 0.01 - t.y - Distort.rg * sin(0.1);
            half4 causticTex02 = SAMPLE_TEXTURE2D(_CausticTexture, sampler_CausticTexture, causticUV02);
            half4 caustic = min(causticTex01, causticTex02);
            float2 causticMaskUV = positionWS.xz/20 + positionWS.y/10 * 0.03  + t.x;
            half causticMask = SAMPLE_TEXTURE2D(_CausticMaskTexture, sampler_CausticMaskTexture, causticMaskUV);
            
            // half lightMask = saturate(dot(light.direction, depthNormal));
            half mask =  causticMask;
            caustic *= _CausticStrength * causticMask * 7;
            c.rgb += caustic;

           
            // c.rgb = lerp(c.rgb,Cloud,0.3);
            // return Cloud;
        #endif

            
         #if _VIGNET_ON
           
            // float tt = _Time.y * float2(_CausticFlow01.z, _CausticFlow02.z);
            half Foam = 0;
            half boolean = 1;
            float2 offset =  half2(_Position.x,-_Position.y)/30;
            float2 offset02 = SAMPLE_TEXTURE2D(_CausticMaskTexture, sampler_CausticMaskTexture, uv/2);
            // return frac((1-dot(uv.x,uv.y)) * 5);
            for(int i = 0;i < _itereat; i++)
            {    
                boolean *= -1;
                float2 Snowuv = half2(uv.x * (1+(0.5*i))- (sin(_Time.y) *0.05) * boolean , uv.y * (1+i) +_Time.x) ; 
                float Foam1 = SAMPLE_TEXTURE2D(_FoamTexture, sampler_FoamTexture,Snowuv - offset ) ;
                Foam += Foam1 * max(0,(uv.y - 0.15 * i));
            }
            
            c += Foam * _color;
            // return Foam += max(0,step(Foam,_FoamLimit ));

         #endif 
            
            
           
            // // return fade;
            float2 Maskuv =  positionWS.xz/35 + _Time.y * 0.01 ;
            float Mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask,Maskuv) ;
            // c = lerp(c,lerp(c,Cloud,0.2),Mask);
            
            // return Mask;
             c.rgb = lerp(c.rgb * _color.rgb ,c.rgb , (1-fade+ Mask ));//lerp(c.rgb, Cloud,0.25)
             c =  lerp(c,lerp(c,_DepthColor,0.5),(fade)*(1-smoothstep(0,1.68,positionWS.y)));//fade*(1-depth* 31.3)
            
            
            
        return c;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "Queue"="Overlay" "RenderPipeline" = "UniversalPipeline"
        }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Distort"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            ENDHLSL
        }
    }

    CustomEditor "taecg.tools.CustomShaderGUI"
}