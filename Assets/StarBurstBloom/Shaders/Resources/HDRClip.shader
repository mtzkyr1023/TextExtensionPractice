Shader "StarBurstBloom/HDRClip"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            
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

            float4 frag (Varyings i) : SV_Target
            {
                // sample the texture
                float4 col = tex2D(_BlitTexture, i.uv);

                float L = dot(col.rgb, float3(0.299f, 0.587f, 0.114f));

                float knee = _Threshold * 0.5f;
                float bloom = smoothstep(_Threshold - knee, _Threshold + knee, L * (max(L - _Threshold, 0.0f)));

                col = col * (bloom / max(L, 1e-4));

                return col;
            }
            ENDHLSL
        }
    }
}
