
Shader "Hidden/RadialBlur"
{   
    // ZTest on
    HLSLINCLUDE
    // ZTest on
    #pragma target 3.5
    //  #pragma multi_compile_local _ _Caustic_ON
    half4 _Limit,_Blur;

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "Assets/Art/Shaders/ShaderLibs/Common.hlsl"
  
    half4 Frag(Varyings i) : SV_Target
    {   
        half2 uv = half2(_Blur.z,_Blur.w);
      
        float2 blurVector = (uv - i.texcoord.xy) * _Blur.y;

        half fade = length(uv - i.texcoord.xy);
        fade = smoothstep(_Limit.x,_Limit.x - _Limit.y,fade);
        half4 c = 1;

        half4 acumulateColor = half4(0, 0, 0, 0);

        half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp,i.texcoord);
        for (int j = 0; j < _Blur.x; j ++)
        {
            acumulateColor += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, i.texcoord);
            i.texcoord.xy += blurVector;
        }
        
        c = acumulateColor/_Blur.x;
        c = lerp(c,col,fade);

            
            
            
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
            // Name "Distort"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            ENDHLSL
        }
    }

}