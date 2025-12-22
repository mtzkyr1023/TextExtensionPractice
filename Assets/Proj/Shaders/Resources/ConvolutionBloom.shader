Shader "Hidden/ConvolutionBloom"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BloomTex ("BloomTex", 2D) = "black" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

       #if SHADER_API_GLES
           struct Attributes
           {
               float4 positionOS : POSITION;
               float2 uv : TEXCOORD0;
           };
       #else
           struct Attributes
           {
               uint vertexID : SV_VertexID;
           };
       #endif


           struct Varyings
           {
               float2 uv : TEXCOORD0;
               float4 positionHCS : SV_POSITION;
           };

            sampler2D _BlitTexture;
            float4 _BlitTexture_ST;

            float _Threshold;

            Varyings vert (Attributes IN)
            {
                Varyings o;
           #if SHADER_API_GLES
               float4 pos = input.positionOS;
               float2 uv  = input.uv;
           #else
               float4 pos = GetFullScreenTriangleVertexPosition(IN.vertexID);
               float2 uv  = GetFullScreenTriangleTexCoord(IN.vertexID);
           #endif
                o.positionHCS = pos;
                o.uv = uv;
                return o;
            }

            sampler2D _BloomTex;
            float _Intensity;

            float4 frag (Varyings i) : SV_Target
            {
                float4 col = tex2D(_BlitTexture, i.uv);
                float4 bloom = tex2D(_BloomTex, float2(i.uv.x, i.uv.y * 0.5f + 0.5f));
                return col + bloom * _Intensity;
            }
            ENDHLSL
        }
    }
}
