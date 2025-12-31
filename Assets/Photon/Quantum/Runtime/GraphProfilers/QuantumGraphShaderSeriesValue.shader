Shader "Quantum/Value Series Graph"
{
	Properties
	{
		_BaseColor            ("Base Color",             Color)    = (0.0, 1.0, 0.0, 1.0)
		_AverageColor         ("Average Color",          Color)    = (1.0, 1.0, 1.0, 0.0)
		_Threshold1Color      ("Threshold 1 Color",      Color)    = (1.0, 1.0, 0.0, 1.0)
		_Threshold2Color      ("Threshold 2 Color",      Color)    = (1.0, 0.5, 0.0, 1.0)
		_Threshold3Color      ("Threshold 3 Color",      Color)    = (1.0, 0.0, 0.0, 1.0)

		_FadeColorIntensity   ("Fade Color Intensity",   Float)    = 1.0
		_PointsThickness      ("Points Thickness",       Float)    = 1.0
		_LinesThickness       ("Lines Thickness",        Float)    = 1.0
		_SideFalloff          ("Side Falloff",           Float)    = 1.0
	}

	SubShader
	{
		Tags
		{
			"Queue"             = "Transparent"
			"RenderType"        = "Transparent"
			"PreviewType"       = "Plane"
			"IgnoreProjector"   = "True"
			"CanUseSpriteAtlas" = "True"
		}

		Cull     Off
		Lighting Off
		ZWrite   Off
		ZTest    Off
		Blend    One OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM

			#pragma vertex   vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex    : SV_POSITION;
				fixed4 color	 : COLOR;
				float2 texcoord  : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			fixed4 _BaseColor;
			fixed4 _AverageColor;
			fixed4 _Threshold1Color;
			fixed4 _Threshold2Color;
			fixed4 _Threshold3Color;

			fixed  _Threshold1;
			fixed  _Threshold2;
			fixed  _Threshold3;
			fixed  _FadeColorIntensity;
			fixed  _PointsThickness;
			fixed  _LinesThickness;
			fixed  _SideFalloff;

			uniform float _Values[512];
			uniform float _Samples;
			uniform float _Average;

			v2f vert(appdata input)
			{
				v2f output;

				UNITY_SETUP_INSTANCE_ID(input); 
				UNITY_INITIALIZE_OUTPUT(v2f, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				output.vertex   = UnityObjectToClipPos(input.vertex);
				output.texcoord = input.texcoord;
				output.color    = input.color;

				return output;
			}

			fixed4 frag(v2f input) : SV_Target
			{
				fixed4 color = input.color;

				fixed x = input.texcoord.x;
				fixed y = input.texcoord.y;

				float value = _Values[floor(x * _Samples)];

				color = _BaseColor;

				if (_Threshold1 > 0.0 && value >= _Threshold1)
				{
					color = _Threshold1Color;
				}
				if (_Threshold2 > 0.0 && value >= _Threshold2)
				{
					color = _Threshold2Color;
				}
				if (_Threshold3 > 0.0 && value >= _Threshold3)
				{
					color = _Threshold3Color;
				}

				if (y > value)
				{
					color.a = 0.0;
				}
				else if (y < value - 0.01 * _PointsThickness)
				{
					color.a = y * _FadeColorIntensity;
				}
				else
				{
					color.a = 1.0;
				}

				if (_LinesThickness > 0.0)
				{
					if (_AverageColor.a > 0.0 && y < _Average && y > _Average - 0.01 * _LinesThickness)
					{
						color = _AverageColor;
					}

					if (_Threshold1Color.a > 0.0 && y < _Threshold1 && y > _Threshold1 - 0.01 * _LinesThickness)
					{
						color = _Threshold1Color;
					}

					if (_Threshold2Color.a > 0.0 && y < _Threshold2 && y > _Threshold2 - 0.01 * _LinesThickness)
					{
						color = _Threshold2Color;
					}

					if (_Threshold3Color.a > 0.0 && y < _Threshold3 && y > _Threshold3 - 0.01 * _LinesThickness)
					{
						color = _Threshold3Color;
					}
				}

				if (_SideFalloff > 0.0)
				{
					float sideFalloff = 0.01 * _SideFalloff;

					if (x < sideFalloff)
					{
						color.a *= 1.0 - (sideFalloff - x) / sideFalloff;
					}
					else if (x > 1.0 - sideFalloff)
					{
						color.a *= (1.0 - x) / sideFalloff;
					}
				}

				color.rgb *= color.a;

				return color;
			}

			ENDCG
		}
	}
}
