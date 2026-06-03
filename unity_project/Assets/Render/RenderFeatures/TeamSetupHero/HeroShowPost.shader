
Shader "Hidden/HeroShowPost"
{   
    // ZTest on
        HLSLINCLUDE

    // ZTest on
    #pragma target 3.5
    #pragma multi_compile_local _ _BLOCK_ON
    

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "Assets/Art/Shaders/ShaderLibs/Common.hlsl"

   inline float randomNoise(float2 seed)
{
    return frac(sin(dot(seed * floor(_Time.y * 15.5), float2(17.13, 3.71))) * 43758.5453123);
}

inline float randomNoise(float seed)
{
    return randomNoise(float2(seed, 1.0));
}

    float3 _LightPosition;
    float4 _Lightcolor;
    float4 _BlockColor;
    float4 _BlockColor1;
    float _LightStrength;
    float _DefocusStrength;
    float _MaskRange,_Distance;

    // TEXTURE2D(_PreDistortTexture);
    // SAMPLER(sampler_PreDistortTexture);
    // TEXTURE2D(_CausticTexture);
    // SAMPLER(sampler_CausticTexture);
    // TEXTURE2D(_CausticMaskTexture);
    // SAMPLER(sampler_CausticMaskTexture);
    // TEXTURE2D (_MainTex);SAMPLER(sampler_MainTex);
    TEXTURE2D (_CameraTexture);SAMPLER(sampler_CameraTexture);
    TEXTURE2D (_MaskTexture);SAMPLER(sampler_MaskTexture);


    half4 Frag(Varyings i) : SV_Target
    {
        half4 c = 1;

        float2 uv = i.texcoord;
        
    //     #if UNITY_REVERSED_Z
    //     real depth = SampleSceneDepth(i.texcoord);
    //     #else
    //     real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(i.texcoord));
    //     #endif
    half Mask = SAMPLE_TEXTURE2D(_MaskTexture,sampler_MaskTexture,half2(i.texcoord.x,i.texcoord.y*_MaskRange) + half2(0,_Time.x/3));
    #if _BLOCK_ON
        half2 block = randomNoise(floor(i.texcoord * 22.8));
        float displaceNoise = pow(block.x, 25.0) * pow(block.x, 3.0);
        float splitRGBNoise = pow(randomNoise(7.2341), 37.0);
        float offsetX = 0.05 - splitRGBNoise * 1.5;
        float offsetY = 0.05 - splitRGBNoise * 1.5;
        float noiseX = _Distance * randomNoise(1.0);
        float noiseY = _Distance * randomNoise(2.0);
        float2 offset = float2(offsetX * noiseX, offsetY* noiseY);
        // c = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.texcoord) * _LightStrength;
        c = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.texcoord + offset) * _BlockColor * _LightStrength * Mask;
        // c += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.texcoord - offset) * _BlockColor1  * _LightStrength;
        // c *= f;
    #endif
        c += SAMPLE_TEXTURE2D(_CameraTexture, sampler_CameraTexture, i.texcoord);
       
        // c.rgb = finalColor.rgb;
		

        return c;
        }
    ENDHLSL

SubShader
    {
        Tags
        {
             "RenderPipeline" = "UniversalPipeline"
        }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            // Name "Distort"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            ENDHLSL
        }
    }

    CustomEditor "taecg.tools.CustomShaderGUI"
}
