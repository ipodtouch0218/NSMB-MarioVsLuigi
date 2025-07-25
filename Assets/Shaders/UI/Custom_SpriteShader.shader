Shader "Custom/Sprite"
{
	Properties
	{
        _MainTex            ("Sprite Texture", 2D) = "white" {}
		_Color              ("Tint", Color) = (1,1,1,1)
		_ColorOffset		("Color Offset", vector) = (0,0,0)
		_Overlay			("Overlay", Float) = 0

		_StencilComp        ("Stencil Comparison", Float) = 8
		_Stencil            ("Stencil ID", Float) = 0
		_StencilOp          ("Stencil Operation", Float) = 0
		_StencilWriteMask   ("Stencil Write Mask", Float) = 255
		_StencilReadMask    ("Stencil Read Mask", Float) = 255

		_CullMode           ("Cull Mode", Float) = 0
		_ColorMask          ("Color Mask", Float) = 15
		_ClipRect           ("Clip Rect", vector) = (-32767, -32767, 32767, 32767)

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
		}

		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Cull [_CullMode]
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask [_ColorMask]

		Pass
		{
            Name "Default"
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
            #pragma target 2.0

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex			: SV_POSITION;
				fixed4 color			: COLOR;
                float2 texcoord			: TEXCOORD0;
				float4 worldPosition	: TEXCOORD1;
				float4 mask				: TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
			};

            sampler2D _MainTex;
			fixed4 _Color;
			fixed3 _ColorOffset;
			float _Overlay;
			fixed4 _TextureSampleAdd;
			float4 _ClipRect;
            float4 _MainTex_ST;
		    float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata_t v)
			{
				v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
				float4 vPosition = UnityObjectToClipPos(v.vertex);
            	OUT.worldPosition = v.vertex;
				OUT.vertex = vPosition;

            	float2 pixelSize = vPosition.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

				float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.mask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                {
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                }
                OUT.color = v.color * _Color;
				return OUT;
			}
			
			
			float3 HUEtoRGB(in float H)
			{
				float R = abs(H * 6 - 3) - 1;
				float G = 2 - abs(H * 6 - 2);
				float B = 2 - abs(H * 6 - 4);
				return saturate(float3(R,G,B));
			}

			float3 HSVtoRGB(in float3 HSV)
			{
				float3 RGB = HUEtoRGB(HSV.x);
				return ((RGB - 1) * HSV.y + 1) * HSV.z;
			}
			
			float Epsilon = 1e-10;
 
			float3 RGBtoHCV(in float3 RGB)
			{
				// Based on work by Sam Hocevar and Emil Persson
				float4 P = (RGB.g < RGB.b) ? float4(RGB.bg, -1.0, 2.0/3.0) : float4(RGB.gb, 0.0, -1.0/3.0);
				float4 Q = (RGB.r < P.x) ? float4(P.xyw, RGB.r) : float4(RGB.r, P.yzx);
				float C = Q.x - min(Q.w, Q.y);
				float H = abs((Q.w - Q.y) / (6 * C + Epsilon) + Q.z);
				return float3(H, C, Q.x);
			}
			
			float3 RGBtoHSV(in float3 RGB)
			{
				float3 HCV = RGBtoHCV(RGB);
				float S = HCV.y / (HCV.z + Epsilon);
				return float3(HCV.x, S, HCV.z);
		    }

			float3 SetColorIfBlack(float3 baseColor, float3 targetColor)
			{
				float isBlack = step(length(baseColor), 0.001);
				return lerp(1, targetColor, isBlack);
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #if UNITY_UI_CLIP_RECT
				half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
				color *= m.x * m.y;
				#endif

				#ifdef UNITY_UI_ALPHACLIP
					clip (color.a - 0.001);
				#endif
				
				float3 hsv = RGBtoHSV(float3(color.xyz));
				hsv += _ColorOffset;
				color = fixed4(HSVtoRGB(hsv).xyz, color.w);

				float3 hsvTint = RGBtoHSV(float3(_Color.xyz));
				hsvTint += _ColorOffset;
				float3 colorTint = HSVtoRGB(hsvTint) * _Overlay;
				
				color = clamp(color, 0, 1);
				color.xyz = lerp(color.xyz, SetColorIfBlack(color.xyz, colorTint), _Overlay);
				return color;
			}
		    ENDCG
		}
	}
}
