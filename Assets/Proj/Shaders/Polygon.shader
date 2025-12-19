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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
                        
            float noise(float2 uv)
            {
                return dot(uv, normalize(float2(1000.0f, 2000.0f)));
            }

            float2 rotation(float2 p, float theta)
            {
	            return float2((p.x) * cos(theta) - p.y * sin(theta), p.x * sin(theta) +  p.y * cos(theta));
            }

            float polygon(float2 p, int n, float size)
            {
                float PI = 3.14159265359f;
	            float a = atan2(p.x, p.y) + PI;
	            float r = 2 * PI / n;
	            return smoothstep(size, size - 0.025f, cos(floor(0.5 + a / r) * r - a) * length(p)); 
            }

            
            float circle(float2 p, float radius)
            {
	            return smoothstep(radius, radius - 0.025f, length(p));
            }

            int _NCount;
            float _Size;
            float _Theta;
            float _Open;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 result = (fixed4)0;

                float2 pos = i.uv * 2.0f - 1.0f;
                pos = rotation(pos, _Theta);

                float size = _Size;

                float p = lerp(polygon(pos, _NCount, size), circle(pos, size), _Open);

                result = p.rrrr;


                return result;
            }
            ENDCG
        }
    }
}
