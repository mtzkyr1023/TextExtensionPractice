
Shader "TMPCustom/3DTextShader" {

Properties {
	[HDR] _FaceColor          ("Face Color", Color) = (1,1,1,1)
	_FaceDilate			("Face Dilate", Range(-1,1)) = 0

	[HDR] _OutlineColor	    ("Outline Color", Color) = (0,0,0,1)
	_OutlineWidth		("Outline Thickness", Range(0,1)) = 0
	_OutlineSoftness	("Outline Softness", Range(0,1)) = 0

	[HDR] _UnderlayColor	    ("Border Color", Color) = (0,0,0,.5)
	_UnderlayOffsetX 	("Border OffsetX", Range(-1,1)) = 0
	_UnderlayOffsetY 	("Border OffsetY", Range(-1,1)) = 0
	_UnderlayDilate		("Border Dilate", Range(-1,1)) = 0
	_UnderlaySoftness 	("Border Softness", Range(0,1)) = 0

	_WeightNormal		("Weight Normal", float) = 0
	_WeightBold			("Weight Bold", float) = .5

	_ShaderFlags		("Flags", float) = 0
	_ScaleRatioA		("Scale RatioA", float) = 1
	_ScaleRatioB		("Scale RatioB", float) = 1
	_ScaleRatioC		("Scale RatioC", float) = 1

	_MainTex			("Font Atlas", 2D) = "white" {}
	_TextureWidth		("Texture Width", float) = 512
	_TextureHeight		("Texture Height", float) = 512
	_GradientScale		("Gradient Scale", float) = 5
	_ScaleX				("Scale X", float) = 1
	_ScaleY				("Scale Y", float) = 1
	_PerspectiveFilter	("Perspective Correction", Range(0, 1)) = 0.875
	_Sharpness			("Sharpness", Range(-1,1)) = 0

	_VertexOffsetX		("Vertex OffsetX", float) = 0
	_VertexOffsetY		("Vertex OffsetY", float) = 0

	_ClipRect			("Clip Rect", vector) = (-32767, -32767, 32767, 32767)
	_MaskSoftnessX		("Mask SoftnessX", float) = 0
	_MaskSoftnessY		("Mask SoftnessY", float) = 0

	_StencilComp		("Stencil Comparison", Float) = 8
	_Stencil			("Stencil ID", Float) = 0
	_StencilOp			("Stencil Operation", Float) = 0
	_StencilWriteMask	("Stencil Write Mask", Float) = 255
	_StencilReadMask	("Stencil Read Mask", Float) = 255

	_ColorMask			("Color Mask", Float) = 15

	_StepCount			("Step Count", Range(1, 128)) = 32
	
	_Thickness			("Thickness", Range(0, 0.02)) = 0.5
	_PlaceAmount		("PlaceAmount", Range(1, 200)) = 1

	_StepSize			("StepSize", Range(0, 1)) = 1.0

	
	_NoiseTex			("Noise Tex", 2D) = "white" {}
}

SubShader {
	Tags
	{
		"Queue" = "Transparent"
        "RenderType" = "Transparent"
        "RenderPipeline"="UniversalPipeline"      // <-
	}


	Stencil
	{
		Ref [_Stencil]
		Comp [_StencilComp]
		Pass [_StencilOp]
		ReadMask [_StencilReadMask]
		WriteMask [_StencilWriteMask]
	}

	Cull Back
	ZWrite On
	Lighting Off
	Fog { Mode Off }
	ZTest [unity_GUIZTestMode]
	Blend One OneMinusSrcAlpha
	ColorMask [_ColorMask]

	Pass {
		
        Name "ForwardLit"                         // <-
        Tags { "LightMode"="UniversalForward" }   // <-

		HLSLPROGRAM
		#pragma vertex VertShader
        #pragma geometry GeomShader
		#pragma fragment PixShader
		
		#pragma shader_feature __ OUTLINE_ON
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
		
		TEXTURE2D(_MainTex);
		SAMPLER(sampler_MainTex);
		// UI Editable properties
		TEXTURE2D(_FaceTex);					// Alpha : Signed Distance
		SAMPLER(sampler_FaceTex);
		TEXTURE2D(_OutlineTex);
		SAMPLER(sampler_OutlineTex);			// RGBA : Color + Opacity
		TEXTURE2D(_BumpMap);					// Normal map
		SAMPLER(sampler_BumpMap);
		TEXTURECUBE(_Cube);
		SAMPLER(sampler_Cube);						// Cube / sphere map
		TEXTURE2D(_MaskTex);
		SAMPLER(sampler_MasKTex);
		TEXTURE2D(_NoiseTex);
		SAMPLER(sampler_NoiseTex);

		CBUFFER_START(UnityPerMaterial)
		
		float		_FaceUVSpeedX;
		float		_FaceUVSpeedY;
		float4		_FaceColor;					// RGBA : Color + Opacity
		float		_FaceDilate;				// v[ 0, 1]

		float		_OutlineSoftness;			// v[ 0, 1]
		float		_OutlineUVSpeedX;
		float		_OutlineUVSpeedY;
		float4		_OutlineColor;				// RGBA : Color + Opacity
		float		_OutlineWidth;				// v[ 0, 1]

		float		_Bevel;						// v[ 0, 1]
		float		_BevelOffset;				// v[-1, 1]
		float		_BevelWidth;				// v[-1, 1]
		float		_BevelClamp;				// v[ 0, 1]
		float		_BevelRoundness;			// v[ 0, 1]

		float		_BumpOutline;				// v[ 0, 1]
		float		_BumpFace;					// v[ 0, 1]

		float4 		_ReflectFaceColor;			// RGB intensity
		float4		_ReflectOutlineColor;
		//float		_EnvTiltX;					// v[-1, 1]
		//float		_EnvTiltY;					// v[-1, 1]
		float3      _EnvMatrixRotation;
		float4x4	_EnvMatrix;

		float4		_SpecularColor;				// RGB intensity
		float		_LightAngle;				// v[ 0,Tau]
		float		_SpecularPower;				// v[ 0, 1]
		float		_Reflectivity;				// v[ 5, 15]
		float		_Diffuse;					// v[ 0, 1]
		float		_Ambient;					// v[ 0, 1]

		float4		_UnderlayColor;				// RGBA : Color + Opacity
		float		_UnderlayOffsetX;			// v[-1, 1]
		float		_UnderlayOffsetY;			// v[-1, 1]
		float		_UnderlayDilate;			// v[-1, 1]
		float		_UnderlaySoftness;			// v[ 0, 1]

		float4 		_GlowColor;					// RGBA : Color + Intesity
		float 		_GlowOffset;				// v[-1, 1]
		float 		_GlowOuter;					// v[ 0, 1]
		float 		_GlowInner;					// v[ 0, 1]
		float 		_GlowPower;					// v[ 1, 1/(1+4*4)]

		// API Editable properties
		float 		_ShaderFlags;
		float		_WeightNormal;
		float		_WeightBold;

		float		_ScaleRatioA;
		float		_ScaleRatioB;
		float		_ScaleRatioC;

		float		_VertexOffsetX;
		float		_VertexOffsetY;

		//float		_UseClipRect;
		float		_MaskID;
		float4		_MaskCoord;
		float4		_ClipRect;	// bottom left(x,y) : top right(z,w)
		//float		_MaskWipeControl;
		//float		_MaskEdgeSoftness;
		//float4		_MaskEdgeColor;
		//bool		_MaskInverse;

		float		_MaskSoftnessX;
		float		_MaskSoftnessY;

		// Font Atlas properties
		float		_TextureWidth;
		float		_TextureHeight;
		float 		_GradientScale;
		float		_ScaleX;
		float		_ScaleY;
		float		_PerspectiveFilter;
		float		_Sharpness;
		
		int			_StepCount;


		float _UIMaskSoftnessX;
        float _UIMaskSoftnessY;
        int _UIVertexColorAlwaysGammaSpace;

		float		_Thickness;
		float		_PlaceAmount;
		float		_StepSize;
		CBUFFER_END

		struct vertex_t {
			UNITY_VERTEX_INPUT_INSTANCE_ID
			float4	vertex			: POSITION;
			float3	normal			: NORMAL;
			float4	color			: COLOR;
			float4	texcoord0		: TEXCOORD0;
			float2	texcoord1		: TEXCOORD1;
		};

		struct geom_t {
			UNITY_VERTEX_INPUT_INSTANCE_ID
			float4 position			: SV_POSITION;
			float3 normal			: NORMAL0;
			float4 color			: COLOR0;
			float4 texcoord0		: TEXCOORD0;
			float2 texcoord1		: TEXCOORD1;
		};

		struct pixel_t {
			UNITY_VERTEX_INPUT_INSTANCE_ID
			UNITY_VERTEX_OUTPUT_STEREO
			float4	vertex			: SV_POSITION;
			float4	faceColor		: COLOR;
			float4	outlineColor	: COLOR1;
			float4	texcoord0		: TEXCOORD0;			// Texture UV, Mask UV
			half4	param			: TEXCOORD1;			// Scale(x), BiasIn(y), BiasOut(z), Bias(w)
			half4	mask			: TEXCOORD2;			// Position in clip space(xy), Softness(zw)
			#if (UNDERLAY_ON | UNDERLAY_INNER)
			float4	texcoord1		: TEXCOORD3;			// Texture UV, alpha, reserved
			half2	underlayParam	: TEXCOORD4;			// Scale(x), Bias(y)
			#endif
			float3  positionWS		: TEXCOORD5;
			float2   texscale		: TEXCOORD6;
			float3  normalWS			: NORMAL0;
			float2 minuv			: TEXCOORD8;
			float2 maxuv			: TEXCOORD9;
		};

		geom_t VertShader(vertex_t input)
		{
			geom_t output = (geom_t)0;

			output.position = float4(TransformObjectToWorld(input.vertex), 1.0f);
			output.normal = input.normal;
			output.color = input.color;
			output.texcoord0 = input.texcoord0;
			output.texcoord1 = input.texcoord1;

			return output;
		}

		pixel_t calcVert(geom_t input)
		{
			pixel_t output = (pixel_t)0;

			float bold = step(input.texcoord0.w, 0);

			float4 vert = input.position;
			vert.x += _VertexOffsetX;
			vert.y += _VertexOffsetY;
			float4 vPosition = TransformWorldToHClip(vert); 

			float2 pixelSize = vPosition.w;
			pixelSize /= float2(_ScaleX, _ScaleY) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

			float scale = rsqrt(dot(pixelSize, pixelSize));
			scale *= abs(input.texcoord0.w) * _GradientScale * (_Sharpness + 1);
			if(UNITY_MATRIX_P[3][3] == 0) scale = lerp(abs(scale) * (1 - _PerspectiveFilter), scale, abs(dot(TransformObjectToWorld(input.normal.xyz), normalize(TransformObjectToWorldDir(vert)))));

			float weight = lerp(_WeightNormal, _WeightBold, bold) / 4.0;
			weight = (weight + _FaceDilate) * _ScaleRatioA * 0.5;

			float layerScale = scale;

			scale /= 1 + (_OutlineSoftness * _ScaleRatioA * scale);
			float bias = (0.5 - weight) * scale - 0.5;
			float outline = _OutlineWidth * _ScaleRatioA * 0.5 * scale;

            float opacity = input.color.a;
			#if (UNDERLAY_ON | UNDERLAY_INNER)
			opacity = 1.0;
			#endif

			float4 faceColor = float4(input.color.rgb, opacity) * _FaceColor;
			faceColor.rgb *= faceColor.a;

			float4 outlineColor = _OutlineColor;
			outlineColor.a *= opacity;
			outlineColor.rgb *= outlineColor.a;
			outlineColor = lerp(faceColor, outlineColor, sqrt(min(1.0, (outline * 2))));

			#if (UNDERLAY_ON | UNDERLAY_INNER)
			layerScale /= 1 + ((_UnderlaySoftness * _ScaleRatioC) * layerScale);
			float layerBias = (.5 - weight) * layerScale - .5 - ((_UnderlayDilate * _ScaleRatioC) * .5 * layerScale);

			float x = -(_UnderlayOffsetX * _ScaleRatioC) * _GradientScale / _TextureWidth;
			float y = -(_UnderlayOffsetY * _ScaleRatioC) * _GradientScale / _TextureHeight;
			float2 layerOffset = float2(x, y);
			#endif

			// Generate UV for the Masking Texture
			float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
			float2 maskUV = (vert.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);

			// Populate structure for pixel shader
			output.vertex = vPosition;
			output.faceColor = faceColor;
			output.outlineColor = outlineColor;
			output.texcoord0 = float4(input.texcoord0.x, input.texcoord0.y, maskUV.x, maskUV.y);
			output.param = half4(scale, bias - outline, bias + outline, bias);

			const half2 maskSoftness = half2(max(_UIMaskSoftnessX, _MaskSoftnessX), max(_UIMaskSoftnessY, _MaskSoftnessY));
			output.mask = half4(vert.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + pixelSize.xy));
			#if (UNDERLAY_ON || UNDERLAY_INNER)
			output.texcoord1 = float4(input.texcoord0 + layerOffset, input.color.a, 0);
			output.underlayParam = half2(layerScale, layerBias);
			#endif

			output.positionWS = input.position;

			return output;
		}

		[maxvertexcount(24)]
		void GeomShader(triangle geom_t input[3], inout TriangleStream<pixel_t> outStream)
		{
			float3 n = float3(UNITY_MATRIX_M[0][2], UNITY_MATRIX_M[1][2], UNITY_MATRIX_M[2][2]);
			float texscalex = 0.0f;
			float texscaley = 0.0f;
			texscalex = max(texscalex, abs(input[0].texcoord0.x - input[1].texcoord0.x));
			texscalex = max(texscalex, abs(input[1].texcoord0.x - input[2].texcoord0.x));
			texscalex = max(texscalex, abs(input[2].texcoord0.x - input[0].texcoord0.x));
			texscaley = max(texscaley, abs(input[0].texcoord0.y - input[1].texcoord0.y));
			texscaley = max(texscaley, abs(input[1].texcoord0.y - input[2].texcoord0.y));
			texscaley = max(texscaley, abs(input[2].texcoord0.y - input[0].texcoord0.y));

			const float difftex = 1.0f;
			const float PlaceAmount = _PlaceAmount * _Thickness;

			float2 minuv = min(input[0].texcoord0.xy, input[1].texcoord0.xy);
			float2 maxuv = max(input[0].texcoord0.xy, input[1].texcoord0.xy);
			
			minuv = min(minuv, input[2].texcoord0.xy);
			maxuv = max(maxuv, input[2].texcoord0.xy);

			const float scale = 0.5f;

			// ëOñ 
			{
				pixel_t output[3];
				output[0] = calcVert(input[0]);
				output[0].texscale = float2(texscalex, texscaley);
				output[1] = calcVert(input[1]);
				output[1].texscale = float2(texscalex, texscaley);
				output[2] = calcVert(input[2]);
				output[2].texscale = float2(texscalex, texscaley);

				float3 ab = output[1].positionWS - output[0].positionWS;
				float3 bc = output[2].positionWS - output[1].positionWS;

				float3 normal = normalize(cross(ab, bc));

				output[0].normalWS = normal;
				output[1].normalWS = normal;
				output[2].normalWS = normal;

				output[0].minuv = minuv;
				output[0].maxuv = maxuv;
				output[1].minuv = minuv;
				output[1].maxuv = maxuv;
				output[2].minuv = minuv;
				output[2].maxuv = maxuv;

				outStream.Append(output[0]);
				outStream.Append(output[1]);
				outStream.Append(output[2]);
				outStream.RestartStrip();
			}
			{
				pixel_t output[3];
				output[0] = calcVert(input[0]);
				output[0].texscale = float2(texscalex, texscaley);
				output[1] = calcVert(input[1]);
				output[1].texscale = float2(texscalex, texscaley);
				output[2] = calcVert(input[2]);
				output[2].texscale = float2(texscalex, texscaley);

				float3 ab = output[1].positionWS - output[0].positionWS;
				float3 bc = output[2].positionWS - output[1].positionWS;

				float3 normal = normalize(cross(ab, bc));

				output[0].normalWS = normal;
				output[1].normalWS = normal;
				output[2].normalWS = normal;

				output[0].minuv = minuv;
				output[0].maxuv = maxuv;
				output[1].minuv = minuv;
				output[1].maxuv = maxuv;
				output[2].minuv = minuv;
				output[2].maxuv = maxuv;

				outStream.Append(output[0]);
				outStream.Append(output[1]);
				outStream.Append(output[2]);
				outStream.RestartStrip();
			}

			// å„ñ 
			{
				pixel_t output[3];
				geom_t temporary = (geom_t)0;
				
				temporary = input[0];
				temporary.position.xyz += n * PlaceAmount * scale;
				output[0] = calcVert(temporary);
				output[0].texscale = float2(texscalex, texscaley);
				
				temporary = input[2];
				temporary.position.xyz += n * PlaceAmount * scale;
				output[1] = calcVert(temporary);
				output[1].texscale = float2(texscalex, texscaley);
				
				temporary = input[1];
				temporary.position.xyz += n * PlaceAmount * scale;
				output[2] = calcVert(temporary);
				output[2].texscale = float2(texscalex, texscaley);

				float3 ab = output[1].positionWS - output[0].positionWS;
				float3 bc = output[2].positionWS - output[1].positionWS;

				float3 normal = normalize(cross(ab, bc));

				output[0].normalWS = normal;
				output[1].normalWS = normal;
				output[2].normalWS = normal;

				output[0].minuv = minuv;
				output[0].maxuv = maxuv;
				output[1].minuv = minuv;
				output[1].maxuv = maxuv;
				output[2].minuv = minuv;
				output[2].maxuv = maxuv;

				outStream.Append(output[0]);
				outStream.Append(output[1]);
				outStream.Append(output[2]);
				outStream.RestartStrip();
			}
			{
				pixel_t output[3];
				geom_t temporary = (geom_t)0;
				
				temporary = input[0];
				temporary.position.xyz += n * PlaceAmount * scale;
				output[0] = calcVert(temporary);
				output[0].texscale = float2(texscalex, texscaley);
				
				temporary = input[2];
				temporary.position.xyz += n * PlaceAmount * scale;
				output[1] = calcVert(temporary);
				output[1].texscale = float2(texscalex, texscaley);
				
				temporary = input[1];
				temporary.position.xyz += n * PlaceAmount * scale;
				output[2] = calcVert(temporary);
				output[2].texscale = float2(texscalex, texscaley);

				float3 ab = output[1].positionWS - output[0].positionWS;
				float3 bc = output[2].positionWS - output[1].positionWS;

				float3 normal = normalize(cross(ab, bc));

				output[0].normalWS = normal;
				output[1].normalWS = normal;
				output[2].normalWS = normal;

				output[0].minuv = minuv;
				output[0].maxuv = maxuv;
				output[1].minuv = minuv;
				output[1].maxuv = maxuv;
				output[2].minuv = minuv;
				output[2].maxuv = maxuv;

				outStream.Append(output[0]);
				outStream.Append(output[1]);
				outStream.Append(output[2]);
				outStream.RestartStrip();
			}

			// ç∂âEñ 
			{
				pixel_t output[3];
				geom_t temporary = (geom_t)0;

				temporary = input[0];
				temporary.texcoord0.xy = float2(input[0].texcoord0.x, input[0].texcoord0.y);
				output[0] = calcVert(temporary);
				
				temporary = input[2];
				temporary.position.xyz = input[1].position.xyz + n * PlaceAmount * scale;
				temporary.texcoord0.xy = float2(input[0].texcoord0.x, input[1].texcoord0.y);
				output[1] = calcVert(temporary);

				temporary = input[1];
				temporary.texcoord0.xy = float2(input[1].texcoord0.x, input[1].texcoord0.y);
				output[2] = calcVert(temporary);

				float3 ab = output[1].positionWS - output[0].positionWS;
				float3 bc = output[2].positionWS - output[1].positionWS;

				float3 normal = normalize(cross(ab, bc));

				output[0].normalWS = normal;
				output[1].normalWS = normal;
				output[2].normalWS = normal;

				output[0].texscale = float2(texscalex, texscaley) * 1.0f;
				output[1].texscale = float2(texscalex, texscaley) * 1.0f;
				output[2].texscale = float2(texscalex, texscaley) * 1.0f;

				output[0].minuv = minuv;
				output[0].maxuv = maxuv;
				output[1].minuv = minuv;
				output[1].maxuv = maxuv;
				output[2].minuv = minuv;
				output[2].maxuv = maxuv;

				outStream.Append(output[0]);
				outStream.Append(output[1]);
				outStream.Append(output[2]);

				outStream.RestartStrip();
			}
			{
				pixel_t output[3];
				geom_t temporary = (geom_t)0;

				temporary = input[0];
				temporary.texcoord0.xy = float2(input[0].texcoord0.x, input[0].texcoord0.y);
				output[0] = calcVert(temporary);

				temporary = input[1];
				temporary.position.xyz = input[0].position.xyz + n * PlaceAmount * scale;
				temporary.texcoord0.xy = float2(input[0].texcoord0.x, input[0].texcoord0.y);
				output[1] = calcVert(temporary);

				temporary = input[2];
				temporary.position.xyz = input[1].position.xyz + n * PlaceAmount * scale;
				temporary.texcoord0.xy = float2(input[1].texcoord0.x, input[1].texcoord0.y);
				output[2] = calcVert(temporary);

				float3 ab = output[1].positionWS - output[0].positionWS;
				float3 bc = output[2].positionWS - output[1].positionWS;

				float3 normal = normalize(cross(ab, bc));
				
				output[0].normalWS = normal;
				output[1].normalWS = normal;
				output[2].normalWS = normal;

				output[0].texscale = float2(texscalex, texscaley);
				output[1].texscale = float2(texscalex, texscaley);
				output[2].texscale = float2(texscalex, texscaley);

				output[0].minuv = minuv;
				output[0].maxuv = maxuv;
				output[1].minuv = minuv;
				output[1].maxuv = maxuv;
				output[2].minuv = minuv;
				output[2].maxuv = maxuv;

				outStream.Append(output[0]);
				outStream.Append(output[1]);
				outStream.Append(output[2]);

				outStream.RestartStrip();
			}
			
			// è„â∫ñ 
			{
				pixel_t output[3];
				geom_t temporary = (geom_t)0;
				temporary = input[2];
				temporary.texcoord0.xy = float2(input[2].texcoord0.x, input[1].texcoord0.y);
				output[0] = calcVert(temporary);
				
				temporary = input[1];
				temporary.texcoord0.xy = float2(input[0].texcoord0.x, input[1].texcoord0.y);
				output[1] = calcVert(temporary);

				temporary = input[2];
				temporary.position.xyz = input[1].position.xyz + n * PlaceAmount * scale;
				temporary.texcoord0.xy = float2(input[0].texcoord0.x, input[1].texcoord0.y);
				output[2] = calcVert(temporary);

				float3 ab = output[1].positionWS - output[0].positionWS;
				float3 bc = output[2].positionWS - output[1].positionWS;

				float3 normal = normalize(cross(ab, bc));
				
				output[0].normalWS = normal;
				output[1].normalWS = normal;
				output[2].normalWS = normal;

				output[0].texscale = float2(texscalex, texscaley);
				output[1].texscale = float2(texscalex, texscaley);
				output[2].texscale = float2(texscalex, texscaley);

				output[0].minuv = minuv;
				output[0].maxuv = maxuv;
				output[1].minuv = minuv;
				output[1].maxuv = maxuv;
				output[2].minuv = minuv;
				output[2].maxuv = maxuv;

				outStream.Append(output[0]);
				outStream.Append(output[1]);
				outStream.Append(output[2]);

				outStream.RestartStrip();
			}
			{
				pixel_t output[3];
				geom_t temporary = (geom_t)0;
				temporary = input[2];
				temporary.texcoord0.xy = float2(input[2].texcoord0.x, input[1].texcoord0.y);
				output[0] = calcVert(temporary);

				temporary = input[2];
				temporary.position.xyz = input[1].position.xyz + n * PlaceAmount * scale;
				temporary.texcoord0.xy = float2(input[1].texcoord0.x, input[1].texcoord0.y);
				output[1] = calcVert(temporary);
				
				temporary = input[2];
				temporary.position.xyz = input[2].position.xyz + n * PlaceAmount * scale;
				temporary.texcoord0.xy = float2(input[2].texcoord0.x, input[1].texcoord0.y);
				output[2] = calcVert(temporary);
				
				float3 ab = output[1].positionWS - output[0].positionWS;
				float3 bc = output[2].positionWS - output[1].positionWS;

				float3 normal = normalize(cross(ab, bc));
				
				output[0].normalWS = normal;
				output[1].normalWS = normal;
				output[2].normalWS = normal;

				output[0].texscale = float2(texscalex, texscaley);
				output[1].texscale = float2(texscalex, texscaley);
				output[2].texscale = float2(texscalex, texscaley);

				output[0].minuv = minuv;
				output[0].maxuv = maxuv;
				output[1].minuv = minuv;
				output[1].maxuv = maxuv;
				output[2].minuv = minuv;
				output[2].maxuv = maxuv;

				outStream.Append(output[0]);
				outStream.Append(output[1]);
				outStream.Append(output[2]);

				outStream.RestartStrip();
			}
		}

		float rand(float2 texcoords)
		{
			//return SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, texcoords);
			return frac(sin(dot(texcoords, float2(12.9898, 78.233))) * 43758.5453);
		}

		half4 raymarching(pixel_t input, float3 pos, float3 dir)
		{
			const float PlaceAmount = _PlaceAmount * _Thickness;

			float4 samp = mul(UNITY_MATRIX_I_M, float4(pos + dir, 1.0f));

			float3 texcoord = mul((float3x3)UNITY_MATRIX_I_M, dir * 0.5f);
			texcoord.xy *= input.texscale;
			float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
			Light mainLight = GetMainLight(shadowCoord);

			float2 samplePos =  clamp(input.texcoord0.xy + texcoord.xy, input.minuv, input.maxuv);

			half d = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, samplePos).a * input.param.x * (step(_Thickness, 1.0f - abs(samp.z) - PlaceAmount * 0.5f) - step(samp.z, _Thickness));
			half4 c = input.faceColor * saturate(d - input.param.w);

			float3 lightOS = mul((float3x3)UNITY_MATRIX_I_M, normalize(mainLight.direction));
			lightOS.xy *= input.texscale;

			float intensity = 0.0f;
			int lightstep = 8;
			float lightstepscale = 0.1f;
			for (int i = 0; i < lightstep; i++)
			{
				samplePos = clamp(input.texcoord0.xy + texcoord.xy + lightOS.xy * (i + 1) / lightstep * lightstepscale, input.minuv, input.maxuv);
				float z = abs(samp.z + lightOS.z * (i + 1) / lightstep * lightstepscale);
				intensity += (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, samplePos).a * input.param.x * ((step(_Thickness, 1.0f - z) - PlaceAmount * 0.5f) - step(z, _Thickness)) - input.param.w);
			}


			intensity = saturate(intensity);

			#ifdef OUTLINE_ON
			c = lerp(input.outlineColor, input.faceColor, saturate(d - input.param.z));
			c *= saturate(d - input.param.y);
			#endif

			#if UNDERLAY_ON
			d = tex2D(_MainTex, input.texcoord1.xy).a * input.underlayParam.x;
			c += float4(_UnderlayColor.rgb * _UnderlayColor.a, _UnderlayColor.a) * saturate(d - input.underlayParam.y) * (1 - c.a);
			#endif

			#if UNDERLAY_INNER
			half sd = saturate(d - input.param.z);
			d = tex2D(_MainTex, input.texcoord1.xy).a * input.underlayParam.x;
			c += float4(_UnderlayColor.rgb * _UnderlayColor.a, _UnderlayColor.a) * (1 - saturate(d - input.underlayParam.y)) * sd * (1 - c.a);
			#endif

			// Alternative implementation to UnityGet2DClipping with support for softness.
			#if UNITY_UI_CLIP_RECT
			half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
			c *= m.x * m.y;
			#endif

			#if (UNDERLAY_ON | UNDERLAY_INNER)
			c *= input.texcoord1.z;
			#endif
			
			c.rgb *= max(saturate(1.0f - intensity), 0.05f);

			return c;
		}

		// PIXEL SHADER
		float4 PixShader(pixel_t input) : SV_Target
		{
			UNITY_SETUP_INSTANCE_ID(input);
			
			half4 c = (half4)0;
			
            float3 cameraPositionWS = GetCameraPositionWS();
			float3 positionWS = input.positionWS;
			
			float3 dir = normalize(positionWS - cameraPositionWS);

			int stepCount = _StepCount;

			for (int i = 0; i < stepCount; i++)
			{
				c += raymarching(input, positionWS, dir.xyz * ((float)i + rand(input.texcoord0.xy)) * _Thickness * _PlaceAmount * _StepSize) / stepCount * (stepCount - i) * 0.3f;
			} 

			clip(c.a - 0.001);

			return float4(max(c.rgb, float3(0.0f, 0.0f, 0.0f)), saturate(c.a));
		}
		ENDHLSL
	}
}

}
