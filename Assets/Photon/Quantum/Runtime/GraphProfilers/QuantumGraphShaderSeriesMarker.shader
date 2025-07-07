Shader "Quantum/Marker Series Graph"
{
	Properties
	{
		_Marker1Color("Marker 1 Color", Color) = (1.0, 0.0, 0.0, 1.0)
		_Marker2Color("Marker 2 Color", Color) = (0.0, 1.0, 0.0, 1.0)
		_Marker3Color("Marker 3 Color", Color) = (0.0, 0.0, 1.0, 1.0)
		_Marker4Color("Marker 4 Color", Color) = (0.0, 0.0, 0.0, 1.0)
		_Marker5Color("Marker 5 Color", Color) = (0.0, 0.0, 0.0, 1.0)
		_Marker6Color("Marker 6 Color", Color) = (0.0, 0.0, 0.0, 1.0)
		_Marker7Color("Marker 7 Color", Color) = (0.0, 0.0, 0.0, 1.0)
		_Marker8Color("Marker 8 Color", Color) = (0.0, 0.0, 0.0, 1.0)

		_Falloff("Falloff", Float) = 0.0
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
				fixed4 color     : COLOR;
				float2 texcoord  : TEXCOORD0;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			fixed4 _Marker1Color;
			fixed4 _Marker2Color;
			fixed4 _Marker3Color;
			fixed4 _Marker4Color;
			fixed4 _Marker5Color;
			fixed4 _Marker6Color;
			fixed4 _Marker7Color;
			fixed4 _Marker8Color;
			fixed  _Falloff;

			uniform float _Values[512];
			uniform float _Samples;

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

				color.a = 0.0;

				fixed x = input.texcoord.x;
				fixed y = input.texcoord.y;

				int value = int(_Values[floor(x * _Samples)]);

				float falloff = 0.0625 * _Falloff;

				if (y < 0.125)
				{
					if (value & 1)
					{
						color = _Marker1Color;

						if (y < 0.0 + falloff)
						{
							color.a = 1.0 - (0.0 + falloff - y) / falloff;
						}
						else if (y > 0.125 - falloff)
						{
							color.a = 1.0 - (y - 0.125 + falloff) / falloff;
						}
					}
				}
				else if (y < 0.25)
				{
					if (value & 2)
					{
						color = _Marker2Color;

						if (y < 0.125 + falloff)
						{
							color.a = 1.0 - (0.125 + falloff - y) / falloff;
						}
						else if (y > 0.25 - falloff)
						{
							color.a = 1.0 - (y - 0.25 + falloff) / falloff;
						}
					}
				}
				else if (y < 0.375)
				{
					if (value & 4)
					{
						color = _Marker3Color;

						if (y < 0.25 + falloff)
						{
							color.a = 1.0 - (0.25 + falloff - y) / falloff;
						}
						else if (y > 0.375 - falloff)
						{
							color.a = 1.0 - (y - 0.375 + falloff) / falloff;
						}
					}
				}
				else if (y < 0.5)
				{
					if (value & 8)
					{
						color = _Marker4Color;

						if (y < 0.375 + falloff)
						{
							color.a = 1.0 - (0.375 + falloff - y) / falloff;
						}
						else if (y > 0.5 - falloff)
						{
							color.a = 1.0 - (y - 0.5 + falloff) / falloff;
						}
					}
				}
				else if (y < 0.625)
				{
					if (value & 16)
					{
						color = _Marker5Color;

						if (y < 0.5 + falloff)
						{
							color.a = 1.0 - (0.5 + falloff - y) / falloff;
						}
						else if (y > 0.625 - falloff)
						{
							color.a = 1.0 - (y - 0.625 + falloff) / falloff;
						}
					}
				}
				else if (y < 0.75)
				{
					if (value & 32)
					{
						color = _Marker6Color;

						if (y < 0.625 + falloff)
						{
							color.a = 1.0 - (0.625 + falloff - y) / falloff;
						}
						else if (y > 0.75 - falloff)
						{
							color.a = 1.0 - (y - 0.75 + falloff) / falloff;
						}
					}
				}
				else if (y < 0.875)
				{
					if (value & 64)
					{
						color = _Marker7Color;

						if (y < 0.75 + falloff)
						{
							color.a = 1.0 - (0.75 + falloff - y) / falloff;
						}
						else if (y > 0.875 - falloff)
						{
							color.a = 1.0 - (y - 0.875 + falloff) / falloff;
						}
					}
				}
				else
				{
					if (value & 128)
					{
						color = _Marker8Color;

						if (y < 0.875 + falloff)
						{
							color.a = 1.0 - (0.875 + falloff - y) / falloff;
						}
						else if (y > 1.0 - falloff)
						{
							color.a = 1.0 - (y - 1.0 + falloff) / falloff;
						}
					}
				}

				color.rgb *= color.a;

				return color;
			}

			ENDCG
		}
	}
}
