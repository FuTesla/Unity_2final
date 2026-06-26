Shader "Custom/ToonRampOutline"
{
    Properties
    {
        _MainTex ("Base Map", 2D) = "white" {}
        _Color ("Base Color", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Color", Color) = (0.35,0.35,0.4,1)
        _HighlightColor ("Highlight Color", Color) = (1,1,1,1)
        _RampSteps ("Ramp Steps", Range(2,5)) = 3
        _SpecularSize ("Specular Size", Range(0.01,0.98)) = 0.65
        _SpecularStrength ("Specular Strength", Range(0,1)) = 0.35
        _OutlineColor ("Outline Color", Color) = (0.02,0.018,0.02,1)
        _OutlineWidth ("Outline Width", Range(0.001,0.03)) = 0.006
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On
            ColorMask RGB

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 normal = normalize(v.normal);
                float4 expandedVertex = v.vertex;
                expandedVertex.xyz += normal * _OutlineWidth;
                o.pos = UnityObjectToClipPos(expandedVertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        Pass
        {
            Name "TOON"
            Tags { "LightMode"="ForwardBase" }
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _ShadowColor;
            fixed4 _HighlightColor;
            float _RampSteps;
            float _SpecularSize;
            float _SpecularStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 halfDir = normalize(lightDir + viewDir);

                float ndotl = saturate(dot(normal, lightDir) * 0.5 + 0.5);
                float stepped = floor(ndotl * _RampSteps) / max(1.0, _RampSteps - 1.0);
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                fixed3 shadow = albedo.rgb * _ShadowColor.rgb;
                fixed3 ramp = lerp(shadow, albedo.rgb, stepped);

                float spec = step(_SpecularSize, saturate(dot(normal, halfDir))) * _SpecularStrength;
                fixed3 color = lerp(ramp, _HighlightColor.rgb, spec);
                return fixed4(color * _LightColor0.rgb, albedo.a);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
