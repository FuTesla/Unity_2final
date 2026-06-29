Shader "Hidden/Toon2D/Pixelate"
{
    Properties
    {
        _VirtualHeight ("Virtual Height", Float) = 320
        _ColorSteps ("Color Steps", Float) = 0
        _EdgeStrength ("Edge Strength", Float) = 0
        _Saturation ("Saturation", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "Pixelate"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _VirtualHeight;
            float _ColorSteps;
            float _EdgeStrength;
            float _Saturation;

            float Luma(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 sourceSize = max(_BlitTextureSize.xy, 1.0);
                float virtualHeight = max(_VirtualHeight, 1.0);
                float virtualWidth = max(round(virtualHeight * sourceSize.x / sourceSize.y), 1.0);
                float2 pixelSize = rcp(float2(virtualWidth, virtualHeight));
                float2 uv = (floor(input.texcoord / pixelSize) + 0.5) * pixelSize;

                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                half3 right = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(pixelSize.x, 0.0)).rgb;
                half3 up = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + float2(0.0, pixelSize.y)).rgb;

                float edge = abs(Luma(color.rgb) - Luma(right)) + abs(Luma(color.rgb) - Luma(up));
                edge = saturate(edge * _EdgeStrength);

                if (abs(_Saturation - 1.0) > 0.001)
                {
                    float gray = Luma(color.rgb);
                    color.rgb = lerp(gray.xxx, color.rgb, _Saturation);
                }

                if (_ColorSteps > 1.0)
                {
                    color.rgb = floor(saturate(color.rgb) * _ColorSteps + 0.5) / _ColorSteps;
                }

                if (_EdgeStrength > 0.001)
                {
                    color.rgb *= 1.0 - edge * 0.35;
                }

                return color;
            }
            ENDHLSL
        }
    }
}
