Shader "G1/PointCloud"
{
    Properties
    {
        _Tint("Tint", Color) = (1, 1, 1, 1)
        _PointSize("Point Size", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "PointCloud"

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _PointSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR0;
                float pointSize : PSIZE;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Tint;
                output.pointSize = _PointSize;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return input.color;
            }

            ENDHLSL
        }
    }
}