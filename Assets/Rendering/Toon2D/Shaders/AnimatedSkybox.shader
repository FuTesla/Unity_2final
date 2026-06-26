Shader "Custom/AnimatedSkybox"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.18, 0.5, 0.95, 1)
        _HorizonColor ("Horizon Color", Color) = (0.48, 0.75, 1, 1)
        _GroundColor ("Ground Color", Color) = (0.38, 0.64, 0.9, 1)
        _BandStrength ("Band Strength", Range(0, 1)) = 0.02
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            fixed4 _GroundColor;
            float _BandStrength;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(v.vertex.xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float h = i.dir.y * 0.5 + 0.5;
                fixed3 sky = lerp(_HorizonColor.rgb, _TopColor.rgb, saturate(h));
                sky = lerp(_GroundColor.rgb, sky, step(0.48, h));

                float bands = sin((h * 28.0) + (_Time.y * 0.45)) * 0.5 + 0.5;
                sky += bands * _BandStrength * smoothstep(0.35, 0.85, h);

                return fixed4(saturate(sky), 1);
            }
            ENDCG
        }
    }

    FallBack Off
}
