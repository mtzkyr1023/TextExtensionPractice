Shader "Unlit/Polygon"
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
                      
            float2 rotation(float2 p, float theta)
            {
	            return float2((p.x) * cos(theta) - p.y * sin(theta), p.x * sin(theta) +  p.y * cos(theta));
            }

            float polygon(float2 p, int n, float size)
            {
	            float a = atan2(p.x, p.y) + PI;
	            float r = 2 * PI / n;
	            return smoothstep(size, size - 0.025f, cos(floor(0.5 + a / r) * r - a) * length(p)); 
            }

            
            float circle(float2 p, float radius)
            {
	            return smoothstep(radius, radius - 0.025f, length(p));
            }

            int _NCount;
            float _Size = 0.5f;
            float _Theta;
            float _Open = 0.0f;

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
                float4 result = (float4)0;

                float2 pos = i.uv * 2.0f - 1.0f;
                pos = rotation(pos, _Theta);

                float size = _Size;

                float p = lerp(polygon(pos, _NCount, size), circle(pos, size), _Open);

                result = p.rrrr;


                return result;
            }
            ENDHLSL
        }
    }
}
