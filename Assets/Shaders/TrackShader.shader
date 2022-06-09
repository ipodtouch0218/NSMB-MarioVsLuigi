Shader "Unlit/TrackShader"
{
    Properties
    {
        [ToggleUI] Star("Star", Float) = 1
        [NoScaleOffset]_MainTex("MainTex", 2D) = "white" {}
        OverlayColor("Color", Color) = (1, 1, 1, 0)
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }
        SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "UniversalMaterialType" = "Unlit"
            "Queue" = "Transparent"
            "ShaderGraphShader" = "true"
            "ShaderGraphTargetId" = ""
        }
        Pass
        {
            Name "Sprite Unlit"
            Tags
            {
                "LightMode" = "Universal2D"
            }

        // Render State
        Cull Off
    Blend SrcAlpha OneMinusSrcAlpha
    ZTest LEqual
    ZWrite Off

        // Debug
        // <None>

        // --------------------------------------------------
        // Pass

        HLSLPROGRAM

        // Pragmas
        #pragma target 2.0
    #pragma exclude_renderers d3d11_9x
    #pragma vertex vert
    #pragma fragment frag

        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>

        // Keywords
        #pragma multi_compile_fragment _ DEBUG_DISPLAY
        // GraphKeywords: <None>

        // Defines
        #define _SURFACE_TYPE_TRANSPARENT 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_COLOR
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_SPRITEUNLIT
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */

        // Includes
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreInclude' */

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

        // --------------------------------------------------
        // Structs and Packing

        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

        struct Attributes
    {
         float3 positionOS : POSITION;
         float3 normalOS : NORMAL;
         float4 tangentOS : TANGENT;
         float4 uv0 : TEXCOORD0;
         float4 color : COLOR;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : INSTANCEID_SEMANTIC;
        #endif
    };
    struct Varyings
    {
         float4 positionCS : SV_POSITION;
         float3 positionWS;
         float4 texCoord0;
         float4 color;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : CUSTOM_INSTANCE_ID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
         uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
         uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
         FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
        #endif
    };
    struct SurfaceDescriptionInputs
    {
         float4 uv0;
         float3 TimeParameters;
    };
    struct VertexDescriptionInputs
    {
         float3 ObjectSpaceNormal;
         float3 ObjectSpaceTangent;
         float3 ObjectSpacePosition;
    };
    struct PackedVaryings
    {
         float4 positionCS : SV_POSITION;
         float3 interp0 : INTERP0;
         float4 interp1 : INTERP1;
         float4 interp2 : INTERP2;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : CUSTOM_INSTANCE_ID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
         uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
         uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
         FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
        #endif
    };

        PackedVaryings PackVaryings(Varyings input)
    {
        PackedVaryings output;
        ZERO_INITIALIZE(PackedVaryings, output);
        output.positionCS = input.positionCS;
        output.interp0.xyz = input.positionWS;
        output.interp1.xyzw = input.texCoord0;
        output.interp2.xyzw = input.color;
        #if UNITY_ANY_INSTANCING_ENABLED
        output.instanceID = input.instanceID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
        output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
        output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        output.cullFace = input.cullFace;
        #endif
        return output;
    }

    Varyings UnpackVaryings(PackedVaryings input)
    {
        Varyings output;
        output.positionCS = input.positionCS;
        output.positionWS = input.interp0.xyz;
        output.texCoord0 = input.interp1.xyzw;
        output.color = input.interp2.xyzw;
        #if UNITY_ANY_INSTANCING_ENABLED
        output.instanceID = input.instanceID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
        output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
        output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        output.cullFace = input.cullFace;
        #endif
        return output;
    }


    // --------------------------------------------------
    // Graph

    // Graph Properties
    CBUFFER_START(UnityPerMaterial)
float Star;
float4 _MainTex_TexelSize;
float4 OverlayColor;
CBUFFER_END

// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
Gradient _Gradient_Definition()
{
    Gradient g;
    g.type = 0;
    g.colorsLength = 6;
    g.alphasLength = 2;
    g.colors[0] = float4(1, 0, 0, 0);
    g.colors[1] = float4(1, 0.3422316, 0, 0.1294118);
    g.colors[2] = float4(1, 0.8700229, 0, 0.3411765);
    g.colors[3] = float4(0.09411765, 1, 0, 0.5676509);
    g.colors[4] = float4(1, 0, 0.7869606, 0.802945);
    g.colors[5] = float4(1, 0, 0, 1);
    g.colors[6] = float4(0, 0, 0, 0);
    g.colors[7] = float4(0, 0, 0, 0);
    g.alphas[0] = float2(1, 0);
    g.alphas[1] = float2(1, 1);
    g.alphas[2] = float2(0, 0);
    g.alphas[3] = float2(0, 0);
    g.alphas[4] = float2(0, 0);
    g.alphas[5] = float2(0, 0);
    g.alphas[6] = float2(0, 0);
    g.alphas[7] = float2(0, 0);
    return g;
}
#define _Gradient _Gradient_Definition()

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Multiply_float_float(float A, float B, out float Out)
{
    Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Rotate_Degrees_float(float2 UV, float2 Center, float Rotation, out float2 Out)
{
    //rotation matrix
    Rotation = Rotation * (3.1415926f / 180.0f);
    UV -= Center;
    float s = sin(Rotation);
    float c = cos(Rotation);

    //center rotation matrix
    float2x2 rMatrix = float2x2(c, -s, s, c);
    rMatrix *= 0.5;
    rMatrix += 0.5;
    rMatrix = rMatrix * 2 - 1;

    //multiply the UVs by the rotation matrix
    UV.xy = mul(UV.xy, rMatrix);
    UV += Center;

    Out = UV;
}

void Unity_Modulo_float2(float2 A, float2 B, out float2 Out)
{
    Out = fmod(A, B);
}

void Unity_SampleGradientV1_float(Gradient Gradient, float Time, out float4 Out)
{
    float3 color = Gradient.colors[0].rgb;
    [unroll]
    for (int c = 1; c < Gradient.colorsLength; c++)
    {
        float colorPos = saturate((Time - Gradient.colors[c - 1].w) / (Gradient.colors[c].w - Gradient.colors[c - 1].w)) * step(c, Gradient.colorsLength - 1);
        color = lerp(color, Gradient.colors[c].rgb, lerp(colorPos, step(0.01, colorPos), Gradient.type));
    }
#ifdef UNITY_COLORSPACE_GAMMA
    color = LinearToSRGB(color);
#endif
    float alpha = Gradient.alphas[0].x;
    [unroll]
    for (int a = 1; a < Gradient.alphasLength; a++)
    {
        float alphaPos = saturate((Time - Gradient.alphas[a - 1].y) / (Gradient.alphas[a].y - Gradient.alphas[a - 1].y)) * step(a, Gradient.alphasLength - 1);
        alpha = lerp(alpha, Gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), Gradient.type));
    }
    Out = float4(color, alpha);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
    Out = A * B;
}

void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
{
    Out = Predicate ? True : False;
}

/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
    float3 Position;
    float3 Normal;
    float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
    VertexDescription description = (VertexDescription)0;
    description.Position = IN.ObjectSpacePosition;
    description.Normal = IN.ObjectSpaceNormal;
    description.Tangent = IN.ObjectSpaceTangent;
    return description;
}

    #ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
    float3 BaseColor;
    float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
    SurfaceDescription surface = (SurfaceDescription)0;
    float _Property_f2e7dd640b4a4407a67113633596b29d_Out_0 = Star;
    Gradient _Property_1f81d39187904db3ba08daa22ba5dcd4_Out_0 = _Gradient;
    float _Multiply_f4a9569317f24954a124e4d934643f3c_Out_2;
    Unity_Multiply_float_float(IN.TimeParameters.x, 0.8, _Multiply_f4a9569317f24954a124e4d934643f3c_Out_2);
    float2 _TilingAndOffset_dae510dea34648e2be902c3e71a11e12_Out_3;
    Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), (_Multiply_f4a9569317f24954a124e4d934643f3c_Out_2.xx), _TilingAndOffset_dae510dea34648e2be902c3e71a11e12_Out_3);
    float2 _Rotate_9252c3a5b4c04dd7ae004d2593b9049b_Out_3;
    Unity_Rotate_Degrees_float(_TilingAndOffset_dae510dea34648e2be902c3e71a11e12_Out_3, float2 (0.5, 0.5), 60, _Rotate_9252c3a5b4c04dd7ae004d2593b9049b_Out_3);
    float2 _Modulo_aff0c4cc872e4853a06d2381a28d64f9_Out_2;
    Unity_Modulo_float2(_Rotate_9252c3a5b4c04dd7ae004d2593b9049b_Out_3, float2(1, 1), _Modulo_aff0c4cc872e4853a06d2381a28d64f9_Out_2);
    float4 _SampleGradient_a90bb498abf94c74a1661f3963fc3e8a_Out_2;
    Unity_SampleGradientV1_float(_Property_1f81d39187904db3ba08daa22ba5dcd4_Out_0, (_Modulo_aff0c4cc872e4853a06d2381a28d64f9_Out_2).x, _SampleGradient_a90bb498abf94c74a1661f3963fc3e8a_Out_2);
    UnityTexture2D _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0 = UnityBuildTexture2DStructNoScale(_MainTex);
    float4 _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0 = SAMPLE_TEXTURE2D(_Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.tex, _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.samplerstate, _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.GetTransformedUV(IN.uv0.xy));
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_R_4 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.r;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_G_5 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.g;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_B_6 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.b;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_A_7 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.a;
    float4 _Multiply_5622f8830d30452680e5b41b4f9a948e_Out_2;
    Unity_Multiply_float4_float4(_SampleGradient_a90bb498abf94c74a1661f3963fc3e8a_Out_2, _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0, _Multiply_5622f8830d30452680e5b41b4f9a948e_Out_2);
    float4 _Property_83c9e8f19f814b6688529e15ef8b3b55_Out_0 = OverlayColor;
    float4 _Multiply_48142ba2f2aa42a6a1efe79a3bf1fc24_Out_2;
    Unity_Multiply_float4_float4(_Property_83c9e8f19f814b6688529e15ef8b3b55_Out_0, _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0, _Multiply_48142ba2f2aa42a6a1efe79a3bf1fc24_Out_2);
    float4 _Branch_f66b419c05e541cbbd365f8d3238b0af_Out_3;
    Unity_Branch_float4(_Property_f2e7dd640b4a4407a67113633596b29d_Out_0, _Multiply_5622f8830d30452680e5b41b4f9a948e_Out_2, _Multiply_48142ba2f2aa42a6a1efe79a3bf1fc24_Out_2, _Branch_f66b419c05e541cbbd365f8d3238b0af_Out_3);
    surface.BaseColor = (_Branch_f66b419c05e541cbbd365f8d3238b0af_Out_3.xyz);
    surface.Alpha = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_A_7;
    return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal = input.normalOS;
    output.ObjectSpaceTangent = input.tangentOS.xyz;
    output.ObjectSpacePosition = input.positionOS;

    return output;
}
    SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);







    output.uv0 = input.texCoord0;
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

    return output;
}

    // --------------------------------------------------
    // Main

    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/2D/ShaderGraph/Includes/SpriteUnlitPass.hlsl"

    ENDHLSL
}
Pass
{
    Name "SceneSelectionPass"
    Tags
    {
        "LightMode" = "SceneSelectionPass"
    }

        // Render State
        Cull Off

        // Debug
        // <None>

        // --------------------------------------------------
        // Pass

        HLSLPROGRAM

        // Pragmas
        #pragma target 2.0
    #pragma exclude_renderers d3d11_9x
    #pragma vertex vert
    #pragma fragment frag

        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>

        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>

        // Defines
        #define _SURFACE_TYPE_TRANSPARENT 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define VARYINGS_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
    #define SCENESELECTIONPASS 1

        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */

        // Includes
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreInclude' */

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

        // --------------------------------------------------
        // Structs and Packing

        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

        struct Attributes
    {
         float3 positionOS : POSITION;
         float3 normalOS : NORMAL;
         float4 tangentOS : TANGENT;
         float4 uv0 : TEXCOORD0;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : INSTANCEID_SEMANTIC;
        #endif
    };
    struct Varyings
    {
         float4 positionCS : SV_POSITION;
         float4 texCoord0;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : CUSTOM_INSTANCE_ID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
         uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
         uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
         FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
        #endif
    };
    struct SurfaceDescriptionInputs
    {
         float4 uv0;
    };
    struct VertexDescriptionInputs
    {
         float3 ObjectSpaceNormal;
         float3 ObjectSpaceTangent;
         float3 ObjectSpacePosition;
    };
    struct PackedVaryings
    {
         float4 positionCS : SV_POSITION;
         float4 interp0 : INTERP0;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : CUSTOM_INSTANCE_ID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
         uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
         uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
         FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
        #endif
    };

        PackedVaryings PackVaryings(Varyings input)
    {
        PackedVaryings output;
        ZERO_INITIALIZE(PackedVaryings, output);
        output.positionCS = input.positionCS;
        output.interp0.xyzw = input.texCoord0;
        #if UNITY_ANY_INSTANCING_ENABLED
        output.instanceID = input.instanceID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
        output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
        output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        output.cullFace = input.cullFace;
        #endif
        return output;
    }

    Varyings UnpackVaryings(PackedVaryings input)
    {
        Varyings output;
        output.positionCS = input.positionCS;
        output.texCoord0 = input.interp0.xyzw;
        #if UNITY_ANY_INSTANCING_ENABLED
        output.instanceID = input.instanceID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
        output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
        output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        output.cullFace = input.cullFace;
        #endif
        return output;
    }


    // --------------------------------------------------
    // Graph

    // Graph Properties
    CBUFFER_START(UnityPerMaterial)
float Star;
float4 _MainTex_TexelSize;
float4 OverlayColor;
CBUFFER_END

// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
Gradient _Gradient_Definition()
{
    Gradient g;
    g.type = 0;
    g.colorsLength = 6;
    g.alphasLength = 2;
    g.colors[0] = float4(1, 0, 0, 0);
    g.colors[1] = float4(1, 0.3422316, 0, 0.1294118);
    g.colors[2] = float4(1, 0.8700229, 0, 0.3411765);
    g.colors[3] = float4(0.09411765, 1, 0, 0.5676509);
    g.colors[4] = float4(1, 0, 0.7869606, 0.802945);
    g.colors[5] = float4(1, 0, 0, 1);
    g.colors[6] = float4(0, 0, 0, 0);
    g.colors[7] = float4(0, 0, 0, 0);
    g.alphas[0] = float2(1, 0);
    g.alphas[1] = float2(1, 1);
    g.alphas[2] = float2(0, 0);
    g.alphas[3] = float2(0, 0);
    g.alphas[4] = float2(0, 0);
    g.alphas[5] = float2(0, 0);
    g.alphas[6] = float2(0, 0);
    g.alphas[7] = float2(0, 0);
    return g;
}
#define _Gradient _Gradient_Definition()

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions
// GraphFunctions: <None>

/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
    float3 Position;
    float3 Normal;
    float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
    VertexDescription description = (VertexDescription)0;
    description.Position = IN.ObjectSpacePosition;
    description.Normal = IN.ObjectSpaceNormal;
    description.Tangent = IN.ObjectSpaceTangent;
    return description;
}

    #ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
    float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
    SurfaceDescription surface = (SurfaceDescription)0;
    UnityTexture2D _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0 = UnityBuildTexture2DStructNoScale(_MainTex);
    float4 _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0 = SAMPLE_TEXTURE2D(_Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.tex, _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.samplerstate, _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.GetTransformedUV(IN.uv0.xy));
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_R_4 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.r;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_G_5 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.g;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_B_6 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.b;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_A_7 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.a;
    surface.Alpha = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_A_7;
    return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal = input.normalOS;
    output.ObjectSpaceTangent = input.tangentOS.xyz;
    output.ObjectSpacePosition = input.positionOS;

    return output;
}
    SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);







    output.uv0 = input.texCoord0;
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

    return output;
}

    // --------------------------------------------------
    // Main

    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

    ENDHLSL
}
Pass
{
    Name "ScenePickingPass"
    Tags
    {
        "LightMode" = "Picking"
    }

        // Render State
        Cull Back

        // Debug
        // <None>

        // --------------------------------------------------
        // Pass

        HLSLPROGRAM

        // Pragmas
        #pragma target 2.0
    #pragma exclude_renderers d3d11_9x
    #pragma vertex vert
    #pragma fragment frag

        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>

        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>

        // Defines
        #define _SURFACE_TYPE_TRANSPARENT 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define VARYINGS_NEED_TEXCOORD0
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
    #define SCENEPICKINGPASS 1

        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */

        // Includes
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreInclude' */

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

        // --------------------------------------------------
        // Structs and Packing

        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

        struct Attributes
    {
         float3 positionOS : POSITION;
         float3 normalOS : NORMAL;
         float4 tangentOS : TANGENT;
         float4 uv0 : TEXCOORD0;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : INSTANCEID_SEMANTIC;
        #endif
    };
    struct Varyings
    {
         float4 positionCS : SV_POSITION;
         float4 texCoord0;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : CUSTOM_INSTANCE_ID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
         uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
         uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
         FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
        #endif
    };
    struct SurfaceDescriptionInputs
    {
         float4 uv0;
    };
    struct VertexDescriptionInputs
    {
         float3 ObjectSpaceNormal;
         float3 ObjectSpaceTangent;
         float3 ObjectSpacePosition;
    };
    struct PackedVaryings
    {
         float4 positionCS : SV_POSITION;
         float4 interp0 : INTERP0;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : CUSTOM_INSTANCE_ID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
         uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
         uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
         FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
        #endif
    };

        PackedVaryings PackVaryings(Varyings input)
    {
        PackedVaryings output;
        ZERO_INITIALIZE(PackedVaryings, output);
        output.positionCS = input.positionCS;
        output.interp0.xyzw = input.texCoord0;
        #if UNITY_ANY_INSTANCING_ENABLED
        output.instanceID = input.instanceID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
        output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
        output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        output.cullFace = input.cullFace;
        #endif
        return output;
    }

    Varyings UnpackVaryings(PackedVaryings input)
    {
        Varyings output;
        output.positionCS = input.positionCS;
        output.texCoord0 = input.interp0.xyzw;
        #if UNITY_ANY_INSTANCING_ENABLED
        output.instanceID = input.instanceID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
        output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
        output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        output.cullFace = input.cullFace;
        #endif
        return output;
    }


    // --------------------------------------------------
    // Graph

    // Graph Properties
    CBUFFER_START(UnityPerMaterial)
float Star;
float4 _MainTex_TexelSize;
float4 OverlayColor;
CBUFFER_END

// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
Gradient _Gradient_Definition()
{
    Gradient g;
    g.type = 0;
    g.colorsLength = 6;
    g.alphasLength = 2;
    g.colors[0] = float4(1, 0, 0, 0);
    g.colors[1] = float4(1, 0.3422316, 0, 0.1294118);
    g.colors[2] = float4(1, 0.8700229, 0, 0.3411765);
    g.colors[3] = float4(0.09411765, 1, 0, 0.5676509);
    g.colors[4] = float4(1, 0, 0.7869606, 0.802945);
    g.colors[5] = float4(1, 0, 0, 1);
    g.colors[6] = float4(0, 0, 0, 0);
    g.colors[7] = float4(0, 0, 0, 0);
    g.alphas[0] = float2(1, 0);
    g.alphas[1] = float2(1, 1);
    g.alphas[2] = float2(0, 0);
    g.alphas[3] = float2(0, 0);
    g.alphas[4] = float2(0, 0);
    g.alphas[5] = float2(0, 0);
    g.alphas[6] = float2(0, 0);
    g.alphas[7] = float2(0, 0);
    return g;
}
#define _Gradient _Gradient_Definition()

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions
// GraphFunctions: <None>

/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
    float3 Position;
    float3 Normal;
    float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
    VertexDescription description = (VertexDescription)0;
    description.Position = IN.ObjectSpacePosition;
    description.Normal = IN.ObjectSpaceNormal;
    description.Tangent = IN.ObjectSpaceTangent;
    return description;
}

    #ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
    float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
    SurfaceDescription surface = (SurfaceDescription)0;
    UnityTexture2D _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0 = UnityBuildTexture2DStructNoScale(_MainTex);
    float4 _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0 = SAMPLE_TEXTURE2D(_Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.tex, _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.samplerstate, _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.GetTransformedUV(IN.uv0.xy));
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_R_4 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.r;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_G_5 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.g;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_B_6 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.b;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_A_7 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.a;
    surface.Alpha = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_A_7;
    return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal = input.normalOS;
    output.ObjectSpaceTangent = input.tangentOS.xyz;
    output.ObjectSpacePosition = input.positionOS;

    return output;
}
    SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);







    output.uv0 = input.texCoord0;
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

    return output;
}

    // --------------------------------------------------
    // Main

    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/SelectionPickingPass.hlsl"

    ENDHLSL
}
Pass
{
    Name "Sprite Unlit"
    Tags
    {
        "LightMode" = "UniversalForward"
    }

        // Render State
        Cull Off
    Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
    ZTest LEqual
    ZWrite Off

        // Debug
        // <None>

        // --------------------------------------------------
        // Pass

        HLSLPROGRAM

        // Pragmas
        #pragma target 2.0
    #pragma exclude_renderers d3d11_9x
    #pragma vertex vert
    #pragma fragment frag

        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>

        // Keywords
        #pragma multi_compile_fragment _ DEBUG_DISPLAY
        // GraphKeywords: <None>

        // Defines
        #define _SURFACE_TYPE_TRANSPARENT 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_COLOR
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_COLOR
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_SPRITEFORWARD
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */

        // Includes
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreInclude' */

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

        // --------------------------------------------------
        // Structs and Packing

        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */

        struct Attributes
    {
         float3 positionOS : POSITION;
         float3 normalOS : NORMAL;
         float4 tangentOS : TANGENT;
         float4 uv0 : TEXCOORD0;
         float4 color : COLOR;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : INSTANCEID_SEMANTIC;
        #endif
    };
    struct Varyings
    {
         float4 positionCS : SV_POSITION;
         float3 positionWS;
         float4 texCoord0;
         float4 color;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : CUSTOM_INSTANCE_ID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
         uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
         uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
         FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
        #endif
    };
    struct SurfaceDescriptionInputs
    {
         float4 uv0;
         float3 TimeParameters;
    };
    struct VertexDescriptionInputs
    {
         float3 ObjectSpaceNormal;
         float3 ObjectSpaceTangent;
         float3 ObjectSpacePosition;
    };
    struct PackedVaryings
    {
         float4 positionCS : SV_POSITION;
         float3 interp0 : INTERP0;
         float4 interp1 : INTERP1;
         float4 interp2 : INTERP2;
        #if UNITY_ANY_INSTANCING_ENABLED
         uint instanceID : CUSTOM_INSTANCE_ID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
         uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
         uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
         FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
        #endif
    };

        PackedVaryings PackVaryings(Varyings input)
    {
        PackedVaryings output;
        ZERO_INITIALIZE(PackedVaryings, output);
        output.positionCS = input.positionCS;
        output.interp0.xyz = input.positionWS;
        output.interp1.xyzw = input.texCoord0;
        output.interp2.xyzw = input.color;
        #if UNITY_ANY_INSTANCING_ENABLED
        output.instanceID = input.instanceID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
        output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
        output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        output.cullFace = input.cullFace;
        #endif
        return output;
    }

    Varyings UnpackVaryings(PackedVaryings input)
    {
        Varyings output;
        output.positionCS = input.positionCS;
        output.positionWS = input.interp0.xyz;
        output.texCoord0 = input.interp1.xyzw;
        output.color = input.interp2.xyzw;
        #if UNITY_ANY_INSTANCING_ENABLED
        output.instanceID = input.instanceID;
        #endif
        #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
        output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
        #endif
        #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
        output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
        #endif
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        output.cullFace = input.cullFace;
        #endif
        return output;
    }


    // --------------------------------------------------
    // Graph

    // Graph Properties
    CBUFFER_START(UnityPerMaterial)
float Star;
float4 _MainTex_TexelSize;
float4 OverlayColor;
CBUFFER_END

// Object and Global properties
SAMPLER(SamplerState_Linear_Repeat);
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
Gradient _Gradient_Definition()
{
    Gradient g;
    g.type = 0;
    g.colorsLength = 6;
    g.alphasLength = 2;
    g.colors[0] = float4(1, 0, 0, 0);
    g.colors[1] = float4(1, 0.3422316, 0, 0.1294118);
    g.colors[2] = float4(1, 0.8700229, 0, 0.3411765);
    g.colors[3] = float4(0.09411765, 1, 0, 0.5676509);
    g.colors[4] = float4(1, 0, 0.7869606, 0.802945);
    g.colors[5] = float4(1, 0, 0, 1);
    g.colors[6] = float4(0, 0, 0, 0);
    g.colors[7] = float4(0, 0, 0, 0);
    g.alphas[0] = float2(1, 0);
    g.alphas[1] = float2(1, 1);
    g.alphas[2] = float2(0, 0);
    g.alphas[3] = float2(0, 0);
    g.alphas[4] = float2(0, 0);
    g.alphas[5] = float2(0, 0);
    g.alphas[6] = float2(0, 0);
    g.alphas[7] = float2(0, 0);
    return g;
}
#define _Gradient _Gradient_Definition()

// Graph Includes
// GraphIncludes: <None>

// -- Property used by ScenePickingPass
#ifdef SCENEPICKINGPASS
float4 _SelectionID;
#endif

// -- Properties used by SceneSelectionPass
#ifdef SCENESELECTIONPASS
int _ObjectId;
int _PassValue;
#endif

// Graph Functions

void Unity_Multiply_float_float(float A, float B, out float Out)
{
    Out = A * B;
}

void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
{
    Out = UV * Tiling + Offset;
}

void Unity_Rotate_Degrees_float(float2 UV, float2 Center, float Rotation, out float2 Out)
{
    //rotation matrix
    Rotation = Rotation * (3.1415926f / 180.0f);
    UV -= Center;
    float s = sin(Rotation);
    float c = cos(Rotation);

    //center rotation matrix
    float2x2 rMatrix = float2x2(c, -s, s, c);
    rMatrix *= 0.5;
    rMatrix += 0.5;
    rMatrix = rMatrix * 2 - 1;

    //multiply the UVs by the rotation matrix
    UV.xy = mul(UV.xy, rMatrix);
    UV += Center;

    Out = UV;
}

void Unity_Modulo_float2(float2 A, float2 B, out float2 Out)
{
    Out = fmod(A, B);
}

void Unity_SampleGradientV1_float(Gradient Gradient, float Time, out float4 Out)
{
    float3 color = Gradient.colors[0].rgb;
    [unroll]
    for (int c = 1; c < Gradient.colorsLength; c++)
    {
        float colorPos = saturate((Time - Gradient.colors[c - 1].w) / (Gradient.colors[c].w - Gradient.colors[c - 1].w)) * step(c, Gradient.colorsLength - 1);
        color = lerp(color, Gradient.colors[c].rgb, lerp(colorPos, step(0.01, colorPos), Gradient.type));
    }
#ifdef UNITY_COLORSPACE_GAMMA
    color = LinearToSRGB(color);
#endif
    float alpha = Gradient.alphas[0].x;
    [unroll]
    for (int a = 1; a < Gradient.alphasLength; a++)
    {
        float alphaPos = saturate((Time - Gradient.alphas[a - 1].y) / (Gradient.alphas[a].y - Gradient.alphas[a - 1].y)) * step(a, Gradient.alphasLength - 1);
        alpha = lerp(alpha, Gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), Gradient.type));
    }
    Out = float4(color, alpha);
}

void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
{
    Out = A * B;
}

void Unity_Branch_float4(float Predicate, float4 True, float4 False, out float4 Out)
{
    Out = Predicate ? True : False;
}

/* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */

// Graph Vertex
struct VertexDescription
{
    float3 Position;
    float3 Normal;
    float3 Tangent;
};

VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
{
    VertexDescription description = (VertexDescription)0;
    description.Position = IN.ObjectSpacePosition;
    description.Normal = IN.ObjectSpaceNormal;
    description.Tangent = IN.ObjectSpaceTangent;
    return description;
}

    #ifdef FEATURES_GRAPH_VERTEX
Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
{
return output;
}
#define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
#endif

// Graph Pixel
struct SurfaceDescription
{
    float3 BaseColor;
    float Alpha;
};

SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
{
    SurfaceDescription surface = (SurfaceDescription)0;
    float _Property_f2e7dd640b4a4407a67113633596b29d_Out_0 = Star;
    Gradient _Property_1f81d39187904db3ba08daa22ba5dcd4_Out_0 = _Gradient;
    float _Multiply_f4a9569317f24954a124e4d934643f3c_Out_2;
    Unity_Multiply_float_float(IN.TimeParameters.x, 0.8, _Multiply_f4a9569317f24954a124e4d934643f3c_Out_2);
    float2 _TilingAndOffset_dae510dea34648e2be902c3e71a11e12_Out_3;
    Unity_TilingAndOffset_float(IN.uv0.xy, float2 (1, 1), (_Multiply_f4a9569317f24954a124e4d934643f3c_Out_2.xx), _TilingAndOffset_dae510dea34648e2be902c3e71a11e12_Out_3);
    float2 _Rotate_9252c3a5b4c04dd7ae004d2593b9049b_Out_3;
    Unity_Rotate_Degrees_float(_TilingAndOffset_dae510dea34648e2be902c3e71a11e12_Out_3, float2 (0.5, 0.5), 60, _Rotate_9252c3a5b4c04dd7ae004d2593b9049b_Out_3);
    float2 _Modulo_aff0c4cc872e4853a06d2381a28d64f9_Out_2;
    Unity_Modulo_float2(_Rotate_9252c3a5b4c04dd7ae004d2593b9049b_Out_3, float2(1, 1), _Modulo_aff0c4cc872e4853a06d2381a28d64f9_Out_2);
    float4 _SampleGradient_a90bb498abf94c74a1661f3963fc3e8a_Out_2;
    Unity_SampleGradientV1_float(_Property_1f81d39187904db3ba08daa22ba5dcd4_Out_0, (_Modulo_aff0c4cc872e4853a06d2381a28d64f9_Out_2).x, _SampleGradient_a90bb498abf94c74a1661f3963fc3e8a_Out_2);
    UnityTexture2D _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0 = UnityBuildTexture2DStructNoScale(_MainTex);
    float4 _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0 = SAMPLE_TEXTURE2D(_Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.tex, _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.samplerstate, _Property_e627b5a6b9944348822e1ba492a3ab47_Out_0.GetTransformedUV(IN.uv0.xy));
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_R_4 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.r;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_G_5 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.g;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_B_6 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.b;
    float _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_A_7 = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0.a;
    float4 _Multiply_5622f8830d30452680e5b41b4f9a948e_Out_2;
    Unity_Multiply_float4_float4(_SampleGradient_a90bb498abf94c74a1661f3963fc3e8a_Out_2, _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0, _Multiply_5622f8830d30452680e5b41b4f9a948e_Out_2);
    float4 _Property_83c9e8f19f814b6688529e15ef8b3b55_Out_0 = OverlayColor;
    float4 _Multiply_48142ba2f2aa42a6a1efe79a3bf1fc24_Out_2;
    Unity_Multiply_float4_float4(_Property_83c9e8f19f814b6688529e15ef8b3b55_Out_0, _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_RGBA_0, _Multiply_48142ba2f2aa42a6a1efe79a3bf1fc24_Out_2);
    float4 _Branch_f66b419c05e541cbbd365f8d3238b0af_Out_3;
    Unity_Branch_float4(_Property_f2e7dd640b4a4407a67113633596b29d_Out_0, _Multiply_5622f8830d30452680e5b41b4f9a948e_Out_2, _Multiply_48142ba2f2aa42a6a1efe79a3bf1fc24_Out_2, _Branch_f66b419c05e541cbbd365f8d3238b0af_Out_3);
    surface.BaseColor = (_Branch_f66b419c05e541cbbd365f8d3238b0af_Out_3.xyz);
    surface.Alpha = _SampleTexture2D_745b9cf742b74ea1ae6fc4d145cc0556_A_7;
    return surface;
}

// --------------------------------------------------
// Build Graph Inputs

VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
{
    VertexDescriptionInputs output;
    ZERO_INITIALIZE(VertexDescriptionInputs, output);

    output.ObjectSpaceNormal = input.normalOS;
    output.ObjectSpaceTangent = input.tangentOS.xyz;
    output.ObjectSpacePosition = input.positionOS;

    return output;
}
    SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
{
    SurfaceDescriptionInputs output;
    ZERO_INITIALIZE(SurfaceDescriptionInputs, output);







    output.uv0 = input.texCoord0;
    output.TimeParameters = _TimeParameters.xyz; // This is mainly for LW as HD overwrite this value
#if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN                output.FaceSign =                                   IS_FRONT_VFACE(input.cullFace, true, false);
#else
#define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
#endif
#undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN

    return output;
}

    // --------------------------------------------------
    // Main

    #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Editor/2D/ShaderGraph/Includes/SpriteUnlitPass.hlsl"

    ENDHLSL
}
    }
        CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
        FallBack "Hidden/Shader Graph/FallbackError"
}