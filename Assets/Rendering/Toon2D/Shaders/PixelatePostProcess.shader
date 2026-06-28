Shader "Hidden/Toon2D/PixelatePostProcess"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ColorSteps ("Color Steps", Range(8, 96)) = 40
        _DitherStrength ("Dither Strength", Range(0, 0.05)) = 0.012
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _ColorSteps;
            float _DitherStrength;

            float Bayer4(float2 pixel)
            {
                int x = (int)fmod(pixel.x, 4.0);
                int y = (int)fmod(pixel.y, 4.0);
                int index = y * 4 + x;
                if (index == 0) return 0.0 / 16.0;
                if (index == 1) return 8.0 / 16.0;
                if (index == 2) return 2.0 / 16.0;
                if (index == 3) return 10.0 / 16.0;
                if (index == 4) return 12.0 / 16.0;
                if (index == 5) return 4.0 / 16.0;
                if (index == 6) return 14.0 / 16.0;
                if (index == 7) return 6.0 / 16.0;
                if (index == 8) return 3.0 / 16.0;
                if (index == 9) return 11.0 / 16.0;
                if (index == 10) return 1.0 / 16.0;
                if (index == 11) return 9.0 / 16.0;
                if (index == 12) return 15.0 / 16.0;
                if (index == 13) return 7.0 / 16.0;
                if (index == 14) return 13.0 / 16.0;
                return 5.0 / 16.0;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv);
                float dither = Bayer4(i.pos.xy) - 0.5;
                float3 graded = saturate(color.rgb + dither * _DitherStrength);
                graded = floor(graded * _ColorSteps + 0.5) / _ColorSteps;
                return fixed4(graded, color.a);
            }
            ENDCG
        }
    }
}
