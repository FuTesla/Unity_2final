Shader "Hidden/Toon2D/InventoryBlurPostProcess"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
            float4 _BlurOffset;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv) * 0.227027f;
                color += tex2D(_MainTex, i.uv + _BlurOffset.xy * 1.384615f) * 0.316216f;
                color += tex2D(_MainTex, i.uv - _BlurOffset.xy * 1.384615f) * 0.316216f;
                color += tex2D(_MainTex, i.uv + _BlurOffset.xy * 3.230769f) * 0.070270f;
                color += tex2D(_MainTex, i.uv - _BlurOffset.xy * 3.230769f) * 0.070270f;
                return color;
            }
            ENDCG
        }
    }
}
