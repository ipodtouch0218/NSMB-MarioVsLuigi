Shader "Fusion/Stats Graph"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
 
        _ColorMask("Color Mask", Float) = 15

        _GoodColor("Good Color", Color) = (0,1,0,1)
        _WarnColor("Warn Color", Color) = (.9,.6,0,1)
        _BadColor("Bad Color", Color) = (1,0,0,1)
        _FlagColor("Flag Color", Color) = (1,1,0,1)
        _InvalidColor("Invalid Color", Color) = (0,1,1,1)
        _NoneColor("Flag Color", Color) = (0,0,0,0)
        _ZWrite("ZWrite", Int) = 0
    }
 
    SubShader
    {
        Tags
        { 
            "Queue"="Geometry+1000" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
        }

        //Cull Off
        Lighting Off
        ZWrite [_ZWrite] 
        ZTest LEqual
        Blend One OneMinusSrcAlpha
        //Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
 
        Pass
        {

        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
             
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };
 
            struct v2f
            {
                float4 vertex   : SV_POSITION;
                half2 texcoord  : TEXCOORD0;
            };
             
            //fixed4 _Color;
            fixed4 _GoodColor;
            fixed4 _WarnColor;
            fixed4 _BadColor;
            fixed4 _FlagColor;
            fixed4 _InvalidColor;
            fixed4 _NoneColor;

            // data for bar graph
            uniform float _Data[1024];
            uniform float _Intensity[1024];
            uniform float _Count;
            uniform float _Height;

            uniform float _ZeroCenter;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
#ifdef UNITY_HALF_TEXEL_OFFSET
                OUT.vertex.xy += (_ScreenParams.zw-1.0)*float2(-1,1);
#endif
                return OUT;
            }
 
            fixed4 frag(v2f IN) : SV_Target
            {
                const float i = floor(IN.texcoord.x * _Count);
                const float fv = _Intensity[i];
                const float v = max(_Data[i], 1 / _Count);
                const float y = IN.texcoord.y;

                float a;
                float dim;

                // All lines draw outward from zero.
                if (_ZeroCenter > 0) {
                    if (v > _ZeroCenter) {
                        dim = 1;

                        if (y > _ZeroCenter) {
                            a = !(y > v);
                        }
                        else {
                            a = 0;
                        }
                    }
                    else {
                        dim = .5;
                        if (y > _ZeroCenter) {
                            a = 0;
                        }
                        else {
                            if (y > v) {
                                a = .5;
                            }
                            else {
                                a = 0;
                            }
                        }
                    }
                }
                else {
                    dim = 1;
                    a = !(y > v);
                }


                // calculate alpha and rgb
                float4 c;
                if (_Count == 0) {
                    c.a = 0.1;
                    c.rgb = _NoneColor.rgb;
                }
                else {
                    if (fv == 0) {
                        c.rgb = _GoodColor.rgb * dim * a;
                    }
                    else if (fv == .5) {
                        c.rgb = _WarnColor.rgb * dim * a;
                    }
                    else if (fv == 1) {
                        c.rgb = _BadColor.rgb * dim * a;
                    }
                    else if (fv == -2) {
                        c.rgb = _NoneColor.rgb * .1;
                    }
                    else if (fv < 0) {
                        c.rgb = _InvalidColor.rgb * dim * a;
                    }
                    else if (fv > 1) {
                        c.rgb = _FlagColor.rgb;
                    }
                    else if (fv < 0.5) {
                        c.rgb = lerp(_GoodColor.rgb, _WarnColor.rgb, max(0, fv * 2)) * dim * a;
                    }
                    else {
                        c.rgb = lerp(_WarnColor.rgb, _BadColor.rgb, min(1, fv - .5f) * 2) * dim * a;
                    }
                    c.a = a;
                }
                return c;
            }
        ENDCG
        }
    }
}
