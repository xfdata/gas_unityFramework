///角色深度边缘光 
//直接写到角色材质上描边表现慢一帧
Shader "Hidden/DepthRim"
{   
    // ZTest on
    HLSLINCLUDE
    // ZTest on
    #pragma target 3.5
    

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

    half _RimRange;
    float4 _RimColor;

    TEXTURE2D(_DistTexture);
    SAMPLER(sampler_DistTexture);

 
    half4 Frag(Varyings i) : SV_Target
    {
        half4 c = 1;
        c.rgb = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.texcoord);
        half3 depthNormal = SampleSceneNormals(i.texcoord);
        half3 CustomDir = half3(-0.35,0,0.2);
        CustomDir = normalize(CustomDir);
        half2 VL = normalize(TransformWorldToView(CustomDir).xy);
        half2 VN = normalize(TransformWorldToView(depthNormal).xy);
        half VNdL = saturate(dot(VL,VN));
        // half3 maskDir = half3(0,1,0);
        // half Mask = 1-saturate(dot(maskDir,VN));
        half2 ssUV = i.texcoord + VN * VNdL * 0.5 * _RimRange;
 
        

        
        #if UNITY_REVERSED_Z
            real depth1 = SampleSceneDepth(i.texcoord);
            real depth = SampleSceneDepth(ssUV);
            real depth1E = LinearEyeDepth(depth1,_ZBufferParams);
            real depthE = LinearEyeDepth(depth,_ZBufferParams);
        #else
            real depth1 = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(i.texcoord));
            real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(ssUV));
            real depth1E = LinearEyeDepth(depth1,_ZBufferParams);
            real depthE = LinearEyeDepth(depth,_ZBufferParams);
        #endif
         half depthDiff = depthE - depth1E;
         float intensity = smoothstep(0.24 * depth1E, 0.25 * depth1E, depthDiff);
        float3 positionWS = ComputeWorldSpacePosition(i.texcoord, depth1, UNITY_MATRIX_I_VP);
        half3 V = normalize(_WorldSpaceCameraPos - positionWS);
        intensity =  smoothstep(0.55,0.8,pow(1-dot(V,depthNormal),1)) * intensity;

        half3 MaskDir = half3(0,-1,0);
        half Mask = saturate(1-dot(MaskDir,depthNormal));
 // return  Mask;
        c = lerp(c,_RimColor,intensity * Mask);
   
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

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            ENDHLSL
        }
    }

    CustomEditor "taecg.tools.CustomShaderGUI"
}