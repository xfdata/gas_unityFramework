//特效通用Shader
//因为世界地图保护盾，所以需关闭SRPBatcher
//by taecg
Shader "ONEMT/Effect/CommonEffect (SoftMaskable)"
{
	Properties
	{
		//: { "type": "group","label":"主控制"}
		//: { "type": "default","prop":"_SrcBlend", "label":"源颜色(混合)"}
		[Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", float) = 5
		//: { "type": "default","prop":"_DstBlend", "label":"目标颜色(混合)"}
		[Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend",float) = 10
		//: { "type": "cull","prop":"_CullMode"}
		[Enum(UnityEngine.Rendering.CullMode)]_CullMode("Cull Mode",int) = 2
		//: { "type": "zwrite","prop":"_ZWrite"}
		[Enum(Off,0,On,1)]_ZWrite("_ZWrite",int) = 0
		//: { "type": "ztest","prop":"_ZTest"}
		//: { "type": "int","prop":"_ColorMask", "label":"_ColorMask"}
		_ColorMask ("StencilID", int) = 15
		[Enum(UnityEngine.Rendering.CompareFunction)]_ZTest("ZTest",int) = 4
		//: { "type": "default","prop":"_SrcBlend1", "label":"源Alpha"}
		[Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend1("Src Blend", float) = 1
		//: { "type": "default","prop":"_DstBlend1", "label":"目标Alpha"}
		[Enum(UnityEngine.Rendering.BlendMode)]_DstBlend1("Dst Blend",float) = 1
		
		//: { "type": "toggle","prop":"_scale", "label":"是否行军队列","vectorComponent1":"w"}
		_scale("_scale", Vector) = (1,1,0,0)
		
		//: { "type": "color","prop":"_BaseColor", "label":"主颜色"}
		[HDR]_BaseColor("Tint Color", Color) = (1,1,1,1)
		//: { "type": "float","prop":"_Intensity_Opacity_DistortionIntensity_AlphaChannel", "label":"总亮度","vectorComponent1":"x"}
		//: { "type": "float","prop":"_Intensity_Opacity_DistortionIntensity_AlphaChannel", "label":"总透明度","vectorComponent1":"y"}
		//: { "type": "float","prop":"_Intensity_Opacity_DistortionIntensity_AlphaChannel", "label":"总扭曲度","vectorComponent1":"z","min":"0","max":"1"}
		//: { "type": "toggle","prop":"_Intensity_Opacity_DistortionIntensity_AlphaChannel", "label":"是否启用Alpha","vectorComponent1":"w"}
		//: { "type": "info", "label":"是否采用主纹理的A通道做为透明?如不勾选,则会用主纹理的R通道用做透明;仅影响AlphaBlend模式."}
		_Intensity_Opacity_DistortionIntensity_AlphaChannel("Intensity_Opacity_DistortionIntensity", Vector) = (1,1,0,0)
		//: { "type": "float","prop":"_scale", "label":"总速度","vectorComponent1":"x","min":"0","max":"2"}
		_scale("_scale", Vector) = (1,1,0,0)
		//: { "type": "group","label":"模版测试"}
		//: { "type": "int","prop":"_Stencil", "label":"StencilID"}
		_Stencil ("StencilID", int) = 0
		//: { "type": "default","prop":"_StencilComp", "label":"StencilComp"}
		[Enum(UnityEngine.Rendering.CompareFunction)]_StencilComp ("StencilComp", int) = 8
		//: { "type": "default","prop":"_StencilOp", "label":"Pass"}
		[Enum(UnityEngine.Rendering.StencilOp)]_StencilOp ("StencilPassOP", int) = 0
		//: { "type": "default","prop":"_StencilFailOp", "label":"Fail"}
		[Enum(UnityEngine.Rendering.StencilOp)]_StencilFailOp ("StencilFailOP", int) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		
		//: { "type": "group","label":"基础纹理"}
		//: { "type": "texture1","prop":"_MainTex", "label":"基础纹理","st":"true"}
		_MainTex("MainTex", 2D) = "black" {}
		//: { "type": "toggle","prop":"_scale", "label":"置灰开关","vectorComponent1":"z"}
		_scale("_scale", Vector) = (1,1,0,0)
		//: { "type": "int","prop":"_Params01", "label":"Mip级别","vectorComponent1":"x","min":"-2","max":"0"}
		//: { "type": "enum","prop":"_UseMapUV1", "label":"使用UV","enums":"UV0|UV1","vectorComponent1":"x"}
		_UseMapUV1("Use Map UV1",Vector) = (0,0,0,0)
		//: { "type": "vector2","prop":"_MainTex_DistortionTex_Speed", "label":"UV速度","vectorComponent1":"x","vectorComponent2":"y"}
		_MainTex_DistortionTex_Speed("MainTex_DistortionTex_Speed", Vector) = (0,0,0,0)
		//: { "type": "info", "label":"如果Repeat无效,请在Inspector中将纹理的WrapMode修改为默认的Repeat."}
		//: { "type": "enum","prop":"_WrapMode_Rotation", "label":"重复模式","enums":"Repeat|Clamp","vectorComponent1":"x"}
		//: { "type": "float","prop":"_WrapMode_Rotation", "label":"主贴图旋转","vectorComponent1":"y","min":"0","max":"6.28"}
		_WrapMode_Rotation("",Vector) = (0,0,0,0) 

		//: { "type": "groupVariant","label":"极坐标","keyword":"_POLAR_ON"}
		//: { "type": "vector2","prop":"_PolarParams", "label":"UV中心","vectorComponent1":"x","vectorComponent2":"y"}
		//: { "type": "toggle","prop":"_PolarParams", "label":"反向","vectorComponent1":"z"}
		_PolarParams("",Vector) = (0.5,0.5,0,0) 

		//: { "type": "groupVariant","label":"顶点变化","keyword":"_VERTEXTRANS_ON"}
		//: { "type": "texture1","prop":"_HightMap", "label":"顶点位移高度图","st":"true"}
		//: { "type": "enum","prop":"_SeqVector", "label":"使用UV","enums":"UV0|UV1","vectorComponent1":"w"}
		//: { "type": "vector2","prop":"_HightMap_Speed", "label":"UV速度","vectorComponent1":"x","vectorComponent2":"y"}
		_HightMap("", 2D) = "black" {}
		_SeqVector("",vector) = (1,1,1,0)
		//: { "type": "toggle","prop":"_HightMap_Speed", "label":"是否粒子系统控制位置强度","vectorComponent1":"z"}
		//: { "type": "info", "label":"例子系统Custom2.y生效,Render中需先添加"}
		_HightMap_Speed("",vector) = (0,0,0,0)
		//: { "type": "float","prop":"_scale", "label":"位移强度","vectorComponent1":"y","min":"0","max":"30"}
		_scale("",vector) = (1,1,0,0)

		//: { "type": "groupVariant","label":"扭曲","keyword":"_DISTORTION_ON"}
		//: { "type": "texture1","prop":"_DistortionTex", "label":"扭曲纹理","st":"true"}
		//: { "type": "int","prop":"_Params01", "label":"Mip级别","vectorComponent1":"y","min":"-2","max":"0"}
		//: { "type": "enum","prop":"_UseMapUV1", "label":"使用UV","enums":"UV0|UV1","vectorComponent1":"y"}
		//: { "type": "vector2","prop":"_MainTex_DistortionTex_Speed", "label":"UV速度","vectorComponent1":"z","vectorComponent2":"w"}

		_DistortionTex("DistortionTex", 2D) = "white" {}

		//: { "type": "groupVariant","label":"遮罩","keyword":"_MASKTEXUV_ON"}
		//: { "type": "texture1","prop":"_Mask1Tex", "label":"遮罩01","st":"true"}
		//: { "type": "int","prop":"_Params01", "label":"Mip级别","vectorComponent1":"z","min":"-2","max":"0"}
		//: { "type": "enum","prop":"_UseMapUV1", "label":"使用UV","enums":"UV0|UV1","vectorComponent1":"z"}
		//: { "type": "vector2","prop":"_Mask1_Mask2_Speed", "label":"UV速度","vectorComponent1":"x","vectorComponent2":"y"}
		_Mask1Tex("Mask1Tex", 2D) = "white" {}
		//: { "type": "texture1","prop":"_Mask2Tex", "label":"遮罩02","st":"true"}
		//: { "type": "int","prop":"_Params01", "label":"Mip级别","vectorComponent1":"w","min":"-2","max":"0"}
		//: { "type": "enum","prop":"_UseMapUV1", "label":"使用UV","enums":"UV0|UV1","vectorComponent1":"w"}
		//: { "type": "vector2","prop":"_Mask1_Mask2_Speed", "label":"UV速度","vectorComponent1":"z","vectorComponent2":"w"}
		_Mask2Tex("Mask2Tex", 2D) = "white" {}
		_Mask1_Mask2_Speed("Mask1_Mask2_Speed", Vector) = (0,0,0,0)

		//: { "type": "groupVariant","label":"溶解","keyword":"_CUTOFF_ON"}
		//: { "type": "texture1","prop":"_CutoffTex", "label":"溶解纹理","st":"true"}
		//: { "type": "toggle","prop":"_DisColorOn", "label":"溶解色开关","vectorComponent1":"x"}
		//: { "type": "color","prop":"_CutoffColor", "label":"溶解颜色"}
		[HDR]_CutoffColor("",color) = (0,0,0,0)
		_CutoffTex("CutoffTex", 2D) = "white" {}
		//: { "type": "int","prop":"_Params02", "label":"Mip级别","vectorComponent1":"x","min":"-2","max":"0"}
		
		//: { "type": "texture1","prop":"_RampTex", "label":"溶解色","st":"true"}
		//: { "type": "int","prop":"_Params02", "label":"Mip级别","vectorComponent1":"y","min":"-2","max":"0"}
		_RampTex("RampTex", 2D) = "white" {}
		//: { "type": "enum","prop":"_CutoffUV2_Distortion_VertexColor_CustomData", "label":"使用UV","enums":"UV0|UV1","vectorComponent1":"x"}
		//: { "type": "vector2","prop":"_CutoffSpeed_Intensity_Soft", "label":"UV速度","vectorComponent1":"x","vectorComponent2":"y"}
		//: { "type": "float","prop":"_CutoffSpeed_Intensity_Soft", "label":"强度","vectorComponent1":"z","min":"0","max":"1"}
		//: { "type": "float","prop":"_CutoffSpeed_Intensity_Soft", "label":"柔化","vectorComponent1":"w"}
		//: { "type": "float","prop":"_Intensity_Color", "label":"溶解色强度","floatComponent1":"x"}
		//: { "type": "toggle","prop":"_CutoffUV2_Distortion_VertexColor_CustomData", "label":"CutOffToVertexColor","vectorComponent1":"z"}
		//: { "type": "toggle","prop":"_CutoffUV2_Distortion_VertexColor_CustomData", "label":"CutOffToCustomData","vectorComponent1":"w"}
		//: { "type": "info", "label":"CutOffTo开启时,强度应设置为1."}
		
		_CutoffSpeed_Intensity_Soft("CutoffSpeed_Intensity_Soft", Vector) = (0,0,0.5,0.5)
		_CutoffUV2_Distortion_VertexColor_CustomData("_CutoffTo",Vector)=(0,0,0,0)
		_Intensity_Color("Intensity_Color",float) = 0
		_DisColorOn("",vector) = (0,1,0,0)

		//: { "type": "groupVariant","label":"外发光","keyword":"_RIM_ON"}
		//: { "type": "color","prop":"_RimColor", "label":"颜色"}
		_RimColor("RimColor",color) = (1,1,1,1)
		//: { "type": "toggle","prop":"_Rim", "label":"是否反转","vectorComponent1":"x"}
		//: { "type": "toggle","prop":"_Rim", "label":"使用乘法(默认加法)","vectorComponent1":"w"}
		//: { "type": "float","prop":"_Rim", "label":"衰减","vectorComponent1":"y"}
		//: { "type": "float","prop":"_Rim", "label":"强度","vectorComponent1":"z","min":"0","max":"10"}
		_Rim("Rim",Vector) = (0,0,0,0)
		//: { "type": "info", "label":"例子系统Custom2.x生效,Render中需先添加"}
		//: { "type": "toggle","prop":"_CustomData", "label":"粒子系统控制强度","vectorComponent1":"x"}
		_CustomData("_CustomData",Vector) = (0,0,0,0)

		//: { "type": "groupVariant","label":"序列帧动画","keyword":"_SEQUENC"}
		//: { "type": "vector2","prop":"_SeqVectorAni", "label":"序列图行列数","vectorComponent1":"x","vectorComponent2":"y"}
		//: { "type": "float","prop":"_SeqVectorAni", "label":"序列图播放速度","vectorComponent1":"z"}
		_SeqVectorAni("_SeqVectorAni",vector) = (1,1,1,0)

		_Params01("",vector) = (0,0,0,0)
		_Params02("",vector) = (0,0,0,0)

		//: { "type": "float////////////","prop":"_AlphaClipThreshold", "label":"."}
		_Dark("",float) = 1
		
		//: { "type": "groupVariant","label":"软边","keyword":"_SOFTINSIDE"}
		//: { "type": "info", "label":"生效位置世界空间y值0点"}
		//: { "type": "float","prop":"_SoftSide", "label":"软边高度","vectorComponent1":"x","min":"0","max":"50"}
		//: { "type": "float","prop":"_SoftSide", "label":"软边平滑过渡","vectorComponent1":"y","min":"0","max":"10"}
		_SoftSide("",vector) = (1,1,0,0)

		//: { "type": "group","label":"渲染状态"}
		//: { "type": "queue"}
		//: { "type": "gpuInstancing"}
	}

	SubShader
	{
		Tags { "Queue"="Transparent" "RenderPipeline" = "UniversalPipeline"}
		Cull [_CullMode]
		ZWrite [_ZWrite]
		ZTest [_ZTest]
		Blend [_SrcBlend] [_DstBlend],[_SrcBlend1] [_DstBlend1]
		Offset -0.1,-0.1
		ColorMask [_ColorMask]
		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Pass 
		{
			Tags {"LightMode" = "UniversalForward"}
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma prefer_hlslcc gles
			#pragma target 3.5
			#pragma multi_compile_instancing
			#pragma shader_feature_local _ _DISTORTION_ON
			#pragma shader_feature_local _ _MASKTEXUV_ON
			#pragma shader_feature_local _ _CUTOFF_ON
			#pragma shader_feature_local _ _RIM_ON
			#pragma shader_feature_local _ _SEQUENC
			#pragma shader_feature_local _ _VertAn
			#pragma shader_feature_local _ _SOFTINSIDE
			#pragma shader_feature_local _ _VERTEXTRANS_ON
			#pragma shader_feature_local _ _OPAQUEDISTORTION_ON
			#pragma shader_feature_local _ _POLAR_ON
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			

			#ifdef UNITY_INSTANCING_ENABLED
				UNITY_INSTANCING_BUFFER_START(prop)
				UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
				UNITY_INSTANCING_BUFFER_END(prop)
			#endif
			
			// CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST,_NoiseMap_ST,_HightMap_ST,_SoftSide;
				float4 _MainTex_DistortionTex_Speed;
				float4 _DistortionTex_ST;
				float4 _Mask1Tex_ST;
				float4 _Mask1_Mask2_Speed;
				float4 _Mask2Tex_ST;
				float4 _CutoffSpeed_Intensity_Soft,_PolarParams;
				float4 _CutoffTex_ST;
				float4 _SeqVector,_HightMap_Speed;
				half4 _BaseColor,_DisColorOn;
				half4 _UseMapUV1;
				half4 _WrapMode_Rotation;
				half4 _Intensity_Opacity_DistortionIntensity_AlphaChannel;
				half4 _CutoffUV2_Distortion_VertexColor_CustomData;
				half4 _RimColor,_Decal;
				half4 _Rim,_CutoffColor;
				float4 _SeqVectorAni;
				half4 _Params01,_Params02,_Clip,_CustomData;
				half _SrcBlend,_DstBlend,_Intensity_Color,_Dark;
				float4 _scale;//程序控制速度
				// half _AlphaClipThreshold;
				float4 _ClipRect,_ParticlePosition;
	            float _UIMaskSoftnessX;
	            float _UIMaskSoftnessY;
			// CBUFFER_END

			// 以下这种写法很多移动平台不支持
			// #define smp_clamp _linear_clamp
			// SAMPLER(smp_clamp);
			// #define smp_repeat _linear_repeat
			// SAMPLER(smp_repeat);
			TEXTURE2D (_MainTex);	SAMPLER(sampler_MainTex);//主纹理一定要设置为Repeat,在shader中再实现clamp
			TEXTURE2D(_DistortionTex);	SAMPLER(sampler_DistortionTex);
			TEXTURE2D(_Mask1Tex);	SAMPLER(sampler_Mask1Tex);
			TEXTURE2D(_Mask2Tex);	SAMPLER(sampler_Mask2Tex);
			TEXTURE2D(_CutoffTex);	SAMPLER(sampler_CutoffTex);
			TEXTURE2D(_RampTex);	SAMPLER(sampler_RampTex);
			TEXTURE2D(_CameraDepthTexture);      SAMPLER(sampler_CameraDepthTexture);
			TEXTURE2D(_CameraOpaqueTexture);      SAMPLER(sampler_CameraOpaqueTexture);
			TEXTURE2D(_HightMap);      SAMPLER(sampler_HightMap);
			

			struct appdata
			{
				float4 positionOS 	: POSITION;
				float4 texcoord0 	: TEXCOORD0;
				float4 texcoord1 	: TEXCOORD1;
				half4 color 		: COLOR;
				half3 normalOS		: NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f 
			{
				float4 positionCS	: SV_POSITION;
				float4 uv0 			: TEXCOORD0;
				float4 uv1			: TEXCOORD1;
				half4 color 		: TEXCOORD2;
				half3 normalWS		: TEXCOORD3;
				half3 viewWS		: TEXCOORD4;
				half3 positionOS 	: TEXCOORD5;
                float4  mask		: TEXCOORD6;
				float2 texcoord		: TEXCOORD7;
                float4 worldPosition : TEXCOORD8;
				float3 positionVS	: TEXCOORD9;
				float3 positionWS	: TEXCOORD10;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			v2f vert (appdata v) 
			{
				v2f o =(v2f)0;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				o.uv0 = v.texcoord0;
				o.uv1 = v.texcoord1;
				o.color = v.color;
				o.positionOS = v.positionOS.xyz;

				o.normalWS = TransformObjectToWorldNormal(v.normalOS);
				float3 positionWS = TransformObjectToWorld(v.positionOS.xyz );
				o.positionWS = positionWS;
				#if _VERTEXTRANS_ON
					float2 HightMapUV = lerp(o.uv0.xy,o.uv1.xy,_SeqVector.w) * _HightMap_ST.xy + _HightMap_ST.zw ;
					HightMapUV += frac(float2(_HightMap_Speed.xy * _Time.y * _scale.x));
					half HightMap = _HightMap.SampleLevel(sampler_HightMap,HightMapUV,0);
					if(_HightMap_Speed.z == 0)
						positionWS.xyz += HightMap * o.normalWS.xyz * _scale.y;
					else
						positionWS.xyz += HightMap * o.normalWS.xyz * o.uv1.w;
					
				#endif
				o.viewWS = normalize(_WorldSpaceCameraPos - positionWS);				
				o.positionCS = TransformWorldToHClip(positionWS.xyz);
				o.positionVS = TransformWorldToView(positionWS);
                float4 vPosition = o.positionCS;
                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScaledScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (v.positionOS.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                // float2 uu = TRANSFORM_TEX(v.texcoord0.xy, _MainTex);
				// o.uv0 = float4(uu, o.uv0.zw);
				const half2 maskSoftness = half2(max(_UIMaskSoftnessX, _UIMaskSoftnessX), max(_UIMaskSoftnessY, _UIMaskSoftnessY));
                o.mask = float4(v.positionOS.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + pixelSize.xy));

				o.worldPosition = v.positionOS;
				
				return o;
			}
			

			inline float UnityGet2DClipping (in float2 position, in float4 clipRect)
			{
			    float2 inside = step(clipRect.xy, position.xy) * step(position.xy, clipRect.zw);
			    return inside.x * inside.y;
			}

			float2 RectToPolar(float2 uv, float2 centerUV) {
                uv = uv - centerUV;                 //改变中心 将中心从UV左下角移到UV中心
                float theta = atan2(uv.y, uv.x);    // atan()值域[-π/2, π/2]一般不用; atan2()值域[-π, π]确定一个完整的圆
				// theta = step(360,theta);
                float r = length(uv);               //UV上的某一点到我们确定的中心得距离
                return float2(theta, r);
            }


			half4 frag (v2f i,half _vface:VFACE,out float outDepth : SV_Depth) : SV_Target 
			{
				
				UNITY_SETUP_INSTANCE_ID(i);
					outDepth = i.positionCS.z;
				 if (_scale.w == 1)//匹配行军队列的深度修改
				 {
				 	
				 	#if defined(UNITY_REVERSED_Z)
						outDepth += 0.1;
					#else
						outDepth -= 0.1;
					#endif
				 }
				

				half4 c = 1;
				float2 customData;

				customData.x = i.uv0.z;	// 粒子中的CustomData存放位置
				customData.y = i.uv0.w;//粒子中的CustomData 控制UV.x 流动一次     UV需要改成Clamp
				// customData.z
				// return half4(i.uv0.w,i.uv0.w,i.uv0.w,1);

				half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(prop, _BaseColor);
				
				half4 tintColor = half4(baseColor.rgb,1);
				float2 screenUV = i.positionCS.xy/_ScreenParams.xy;
				// #if _DEPTHDECAL_ON
				// 	//深度贴花
				// 	// half3 positionWS = TransformObjectToWorld(i.positionOS.xyz - _ParticlePosition.xyz);
				// 	// half3 positionVS = TransformWorldToView(i.positionWS );
				// 	half Depth = SAMPLE_TEXTURE2D(_CameraDepthTexture,sampler_CameraDepthTexture,screenUV);
    //             	half DepthZ = LinearEyeDepth(Depth,_ZBufferParams);
	   //
				// 	float4 DepthVS = 1;
				// 	DepthVS.xy = i.positionVS.xy*DepthZ/-i.positionVS.z;
				// 	DepthVS.z = DepthZ;
				// 	float3 DepthWS = mul(unity_CameraToWorld,DepthVS) - _ParticlePosition.xyz;
				// 	float3 DepthOS = mul(unity_WorldToObject,float4(DepthWS ,1));   
				// 	// return  _ParticlePosition.z;
				// #endif
				float2 uv0_MainTex = i.uv0.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float2 uv1_MainTex = i.uv1.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				float2 mainTex_uv = lerp(uv0_MainTex,uv1_MainTex,_UseMapUV1.x);
				//主纹理旋转    //因为精度问题  旋转不支持流UV   
				float cosA = cos(_WrapMode_Rotation.y);
				float sinA = sin(_WrapMode_Rotation.y);
				float2x2 M_rotationZ = float2x2(
				cosA,sinA,
				-sinA,cosA);
				mainTex_uv = mul(M_rotationZ,mainTex_uv*2-1)*0.5+0.5;
				mainTex_uv = lerp(mainTex_uv,saturate(float2(mainTex_uv.x+customData.y-1,mainTex_uv.y)),_WrapMode_Rotation.x);

				//主纹理
				//由于两套UV需要分别针对多张纹理做Tiling和Offset，所以就不再放在vert中执行
				
				float2 mainTex_speed = frac(float2(_MainTex_DistortionTex_Speed.xy * _Time.y * _scale.x));
				// float2 mainTex_speed = _MainTex_DistortionTex_Speed.xy * _GlobalTime;
				

				#if _POLAR_ON
					half boolean = 1;
					if(!_PolarParams.z)
					{
						boolean = -1;
					}
					float2 thetaR = RectToPolar(mainTex_uv,half2(_PolarParams.x,_PolarParams.y));
					mainTex_uv = float2(
                    thetaR.x / 3.141593 * 0.5 + 0.5,    // θ映射到[0, 1]
                    thetaR.y + frac(_Time.x * _scale.x)  * boolean    // r随时间流动
                );
				#endif

				// #if _DEPTHDECAL_ON
				// 	mainTex_uv =  DepthOS.xz/_Decal.x + half2(_Decal.y,_Decal.z);
				// #endif
				// half4 mainTex = ;
				// return half4(i.positionOS ,1);
				mainTex_uv += mainTex_speed;

				#if _SEQUENC
					float time = floor(_Time.y * _SeqVectorAni.z);//控制时间
					float row = floor(time / _SeqVectorAni.y);//行索引
					float col = time - row * _SeqVectorAni.y;//列索引
					float2 cellSize = float2(mainTex_uv.x/_SeqVectorAni.y,mainTex_uv.y/_SeqVectorAni.x);//小图大小
					mainTex_uv.x = cellSize.x + col / _SeqVectorAni.y;
					mainTex_uv.y = cellSize.y - row / _SeqVectorAni.x;
				#endif

			
				//纹理扭曲
				half4 mainTex = 0;
				float2 distortionUV = mainTex_uv;
				float2 distortTex = 1;
				#if _DISTORTION_ON
					float2 uv0_DistortionTex = i.uv0.xy * _DistortionTex_ST.xy + _DistortionTex_ST.zw;
					float2 uv1_DistortionTex = i.uv1.xy * _DistortionTex_ST.xy + _DistortionTex_ST.zw;
					float2 distortionTex_speed = frac(float2(_MainTex_DistortionTex_Speed.zw * _Time.y * _scale.x));
					// float2 distortionTex_speed = _MainTex_DistortionTex_Speed.zw * _GlobalTime;
					float2 distortionTex_uv = lerp(uv0_DistortionTex,uv1_DistortionTex,_UseMapUV1.y);
					distortionTex_uv += distortionTex_speed;
					distortTex = SAMPLE_TEXTURE2D_BIAS( _DistortionTex,sampler_DistortionTex, distortionTex_uv,_Params01.y).rg;				
					distortionUV = (lerp( mainTex_uv, distortTex.xy , _Intensity_Opacity_DistortionIntensity_AlphaChannel.z));
					distortionUV = mainTex_uv + distortTex.xy * _Intensity_Opacity_DistortionIntensity_AlphaChannel.z;//结果与lerp一样
					
				#endif
					
					// float2 uv0_DistortionTex = screenUV * _DistortionTex_ST.xy + _DistortionTex_ST.zw;
					// uv0_DistortionTex += distortionTex_speed;
					// half4 Opaque = SAMPLE_TEXTURE2D_BIAS(_CameraOpaqueTexture,sampler_CameraOpaqueTexture,uv0_DistortionTex,_Params01.y);


				
				mainTex = SAMPLE_TEXTURE2D_BIAS(_MainTex,sampler_MainTex,distortionUV,_Params01.x);

				half gray = 0.2125 * mainTex.r + 0.7154 * mainTex.g + 0.0721*mainTex.b;
				// return mainTex;
				mainTex.rgb = lerp(mainTex.rgb,gray.xxx,_scale.z);

				// float2 screenUV = i.positionCS.xy/_ScreenParams.xy;
					

				//利用采样器来实现UV的重复模式，可以解决边上有条线及重复度下扭曲不对的问题
				// if (_WrapMode_Rotation.x==0)
				// mainTex = SAMPLE_TEXTURE2D(_MainTex, smp_repeat, distortionUV);
				// else
				// mainTex = SAMPLE_TEXTURE2D(_MainTex, smp_clamp, distortionUV);

				//两个遮罩
				float3 maskTex01 = 1,maskTex02 = 1;
				#if _MASKTEXUV_ON
					float2 uv0_Mask1Tex = i.uv0.xy * _Mask1Tex_ST.xy + _Mask1Tex_ST.zw;
					float2 uv1_Mask1Tex = i.uv1.xy * _Mask1Tex_ST.xy + _Mask1Tex_ST.zw;
					float2 maskTex01_uv = lerp(uv0_Mask1Tex,uv1_Mask1Tex,_UseMapUV1.z);
					maskTex01_uv += frac(float2(_Mask1_Mask2_Speed.xy * _Time.y * _scale.x));
					// maskTex01_uv += _Mask1_Mask2_Speed.xy *_GlobalTime;
					maskTex01_uv=(maskTex01_uv);
					maskTex01 = SAMPLE_TEXTURE2D_BIAS( _Mask1Tex,sampler_Mask1Tex, maskTex01_uv,_Params01.z).rgb;
					float2 uv0_Mask2Tex = i.uv0.xy * _Mask2Tex_ST.xy + _Mask2Tex_ST.zw;
					float2 uv1_Mask2Tex = i.uv1.xy * _Mask2Tex_ST.xy + _Mask2Tex_ST.zw;
					float2 maskTex02_uv = lerp(uv0_Mask2Tex,uv1_Mask2Tex,_UseMapUV1.w);
					maskTex02_uv += frac(float2(_Mask1_Mask2_Speed.zw * _Time.y * _scale.x));
					// maskTex02_uv += fmod(_Mask1_Mask2_Speed.zw * _Time.y,1);
					maskTex02_uv=(maskTex02_uv);
					maskTex02 = SAMPLE_TEXTURE2D_BIAS( _Mask2Tex,sampler_Mask2Tex, maskTex02_uv,_Params01.w).rgb;
				#endif

				c = tintColor * mainTex * float4( i.color.rgb , 1 ) * maskTex01.r * maskTex02.r;
				//外发光
				#if _RIM_ON
					
					half4 rim = _RimColor ;
					half NdotV = saturate(dot(i.normalWS*_vface,i.viewWS));
					NdotV = lerp(1-NdotV,NdotV,_Rim.x);

					if(_CustomData.x == 0)
					NdotV = PositivePow(NdotV,_Rim.y) * _Rim.z;
					else
					NdotV = PositivePow(NdotV,_Rim.y) * max(0,i.uv1.z);

					rim *= NdotV;
					if(_Rim.w==0)	c.rgb += rim;
					else	c.rgb *= rim;
					// return rim;
				#endif
				// return  i.color.a;
				//总体控制
				c.rgb *= _Intensity_Opacity_DistortionIntensity_AlphaChannel.x;
				float alpha = _BaseColor.a  * i.color.a * maskTex01.r * maskTex02.r;
				alpha *= lerp(mainTex.r,mainTex.a,_Intensity_Opacity_DistortionIntensity_AlphaChannel.w);

				// 溶解
				#if _CUTOFF_ON
					float2 uv0_CutoffTex = i.uv0.xy * _CutoffTex_ST.xy + _CutoffTex_ST.zw;
					float2 uv1_CutoffTex = i.uv1.xy * _CutoffTex_ST.xy + _CutoffTex_ST.zw;
					float2 cutoffTex_uv = lerp(uv0_CutoffTex,uv1_CutoffTex,_CutoffUV2_Distortion_VertexColor_CustomData.x);
					cutoffTex_uv += frac(float2(_CutoffSpeed_Intensity_Soft.xy * _Time.y * _scale.x));
					// cutoffTex_uv += _CutoffSpeed_Intensity_Soft.xy * _GlobalTime;
					float2 distortionIntensity = lerp( cutoffTex_uv , distortTex.xy , _Intensity_Opacity_DistortionIntensity_AlphaChannel.z);
					float2 cutoff2Distortion = lerp(cutoffTex_uv,distortionIntensity,_CutoffUV2_Distortion_VertexColor_CustomData.y);
					cutoff2Distortion=frac(cutoff2Distortion);
					cutoff2Distortion = lerp(cutoff2Distortion,distortionUV,_Intensity_Opacity_DistortionIntensity_AlphaChannel.z);

					float cutoffTex = SAMPLE_TEXTURE2D_BIAS( _CutoffTex,sampler_CutoffTex, cutoff2Distortion,_Params02.x).r;
					//return cutoffTex;
					float cutoff2VertexColor = lerp(1,i.color.a,_CutoffUV2_Distortion_VertexColor_CustomData.z);
					float cutoff2CustomData = lerp(1,customData.x,_CutoffUV2_Distortion_VertexColor_CustomData.w);
					// float cutoffClamp = saturate( cutoffTex * cutoff2VertexColor * cutoff2CustomData + 1.0 +  _CutoffSpeed_Intensity_Soft.z * -2.0 );
					float cutoffClamp = saturate( cutoffTex + 1.0 +  _CutoffSpeed_Intensity_Soft.z * cutoff2VertexColor * cutoff2CustomData * -2.0 );
					float cutoff = smoothstep( 1.0 - _CutoffSpeed_Intensity_Soft.w, _CutoffSpeed_Intensity_Soft.w , cutoffClamp);
					// float alpha = _BaseColor.a * ( mainTex_Desaturate * mainTex.a ) * i.color.a * maskTex01.r * maskTex02.r;
					// half mainTexGrey = Luminance(mainTex.rgb);
					half4 RampTex = SAMPLE_TEXTURE2D_BIAS(_RampTex,sampler_RampTex, cutoff,_Params02.y);
					half a = smoothstep(_CutoffSpeed_Intensity_Soft.w,_CutoffSpeed_Intensity_Soft.w+0.2,cutoff);

					alpha *= cutoff;
					c.rgb = lerp(c.rgb,lerp(c.rgb,_CutoffColor.rgb,(1-a)),_DisColorOn.x);
					// c.rgb = lerp(c.rgb,lerp(c.rgb,_CutoffColor.rgb,saturate(pow(1-a,_SeqVector.w))),_DisColorOn.x);
					// return pow(1-a,5);

					c.rgb += RampTex.rgb * _Intensity_Color;
				#endif

				c.a *= alpha * _Intensity_Opacity_DistortionIntensity_AlphaChannel.y;

				if(_SrcBlend==1&&_DstBlend==1)//Blend One One
				{
					c.rgb *= _Intensity_Opacity_DistortionIntensity_AlphaChannel.y * i.color.a * alpha;
				}
				
				#if _SOFTINSIDE
					half depth = smoothstep(_SoftSide.x,_SoftSide+_SoftSide.y,i.positionWS.y);
					c.a *= saturate(depth);
				
				#endif
				// return saturate(depth);
				return c * saturate(_Dark);
			}
						
			ENDHLSL
		}
	}

	// CustomEditor "CommonEffectShaderGUI"
	CustomEditor "taecg.tools.CustomShaderGUI"
}
