Shader "Hidden/Toon2D/ScreenSpaceOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0.018, 0.017, 0.02, 1)
        _Thickness ("Thickness", Range(0.5, 3)) = 1
        _DepthThreshold ("Depth Threshold", Range(0.001, 0.08)) = 0.012
        _NormalThreshold ("Normal Threshold", Range(0.01, 1)) = 0.18
        _Strength ("Strength", Range(0, 1)) = 0.85
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
            sampler2D _CameraDepthNormalsTexture;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;
            float _Thickness;
            float _DepthThreshold;
            float _NormalThreshold;
            float _Strength;

            void SampleDepthNormal(float2 uv, out float depth, out float3 normal)
            {
                float4 encoded = tex2D(_CameraDepthNormalsTexture, uv);
                DecodeDepthNormal(encoded, depth, normal);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, i.uv);

                float centerDepth;
                float3 centerNormal;
                SampleDepthNormal(i.uv, centerDepth, centerNormal);

                float2 texel = _MainTex_TexelSize.xy * _Thickness;
                float depthRight;
                float depthLeft;
                float depthUp;
                float depthDown;
                float3 normalRight;
                float3 normalLeft;
                float3 normalUp;
                float3 normalDown;

                SampleDepthNormal(i.uv + float2(texel.x, 0), depthRight, normalRight);
                SampleDepthNormal(i.uv - float2(texel.x, 0), depthLeft, normalLeft);
                SampleDepthNormal(i.uv + float2(0, texel.y), depthUp, normalUp);
                SampleDepthNormal(i.uv - float2(0, texel.y), depthDown, normalDown);

                float depthEdge = 0;
                depthEdge = max(depthEdge, abs(centerDepth - depthRight));
                depthEdge = max(depthEdge, abs(centerDepth - depthLeft));
                depthEdge = max(depthEdge, abs(centerDepth - depthUp));
                depthEdge = max(depthEdge, abs(centerDepth - depthDown));
                depthEdge = smoothstep(_DepthThreshold, _DepthThreshold * 2.0, depthEdge);

                float normalEdge = 0;
                normalEdge = max(normalEdge, 1.0 - dot(centerNormal, normalRight));
                normalEdge = max(normalEdge, 1.0 - dot(centerNormal, normalLeft));
                normalEdge = max(normalEdge, 1.0 - dot(centerNormal, normalUp));
                normalEdge = max(normalEdge, 1.0 - dot(centerNormal, normalDown));
                normalEdge = smoothstep(_NormalThreshold, _NormalThreshold * 2.0, normalEdge);

                float edge = saturate(max(depthEdge, normalEdge) * _Strength);
                return lerp(source, _OutlineColor, edge);
            }
            ENDCG
        }
    }
}
