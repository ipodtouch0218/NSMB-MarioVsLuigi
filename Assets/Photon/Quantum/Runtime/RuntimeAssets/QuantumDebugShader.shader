Shader "Unlit/Quantum Debug"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _UseShading ("Use Shading", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        ZTest Always
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
            };

            float4 _Color;
            float _UseShading;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                fixed4 color = _Color;

                if (_UseShading > 0.5)
                {
                    float3 lightDir = normalize(float3(1, 1, -1));
                    float3 normalDir = normalize(i.worldNormal);
                    
                    if(!isFrontFace)
                    {
                        discard;
                    }
          
                    float ndotl = dot(normalDir, lightDir);

                    fixed4 color0 = _Color * 0.65;
                    fixed4 color1 = _Color * 0.3;

                    float strength = 1;

                    color0 = lerp(_Color, color0, strength);
                    color1 = lerp(fixed4(0, 0, 0, _Color.a), color1, strength);

                    float term1 = max(0, ndotl);
                    float term2 = 1.0 - abs(ndotl);
                    float term3 = max(0, -ndotl);

                    fixed4 result = _Color * term1 + color0 * term2 + color1 * term3;
                    result.a = _Color.a;

                    color = result;
                }

                return color;
            }
            ENDCG
        }
    }
}