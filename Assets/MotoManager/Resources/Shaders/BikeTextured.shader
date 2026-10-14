// The detailed bike and rider (Resources/Models/bike_*): the baked base texture repainted in the team's colours through
// a mask (R = main colour areas, G = second colour, B = accent; A = gloss), lit like MotoManager/BikeLit (fixed
// directional light, sky/ground hemisphere ambient, Blinn highlight, fresnel rim). A repainted area keeps the baked
// shading: its brightness is the base texture's luminance relative to the area's reference.
Shader "MotoManager/BikeTextured"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _MaskTex ("Mask (R main, G second, B accent, A gloss)", 2D) = "black" {}
        _Primary ("Main colour", Color) = (1,1,1,1)
        _Secondary ("Second colour", Color) = (0.1,0.1,0.15,1)
        _Tertiary ("Accent", Color) = (1,0.55,0.1,1)
        _Tint ("Tint", Color) = (1,1,1,1)
        _LightDir ("Light direction", Vector) = (0.35, 0.85, 0.4, 0)
        _Ambient ("Ambient", Range(0,1)) = 0.5
        _SkyColor ("Sky", Color) = (0.8, 0.86, 0.98, 1)
        _GroundColor ("Ground", Color) = (0.3, 0.29, 0.3, 1)
        _Spec ("Highlight", Range(0,2)) = 0.55
        _Rim ("Rim", Range(0,1)) = 0.38
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            fixed4 _Primary;
            fixed4 _Secondary;
            fixed4 _Tertiary;
            fixed4 _Tint;
            float4 _LightDir;
            half _Ambient;
            fixed4 _SkyColor;
            fixed4 _GroundColor;
            half _Spec;
            half _Rim;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normal : TEXCOORD1;
                float3 world : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 base = tex2D(_MainTex, i.uv).rgb;
                fixed4 mask = tex2D(_MaskTex, i.uv);
                half lum = dot(base, half3(0.299, 0.587, 0.114));
                // Repaint: each area keeps the baked light and shade (luminance over the area's reference brightness).
                half3 col = base;
                col = lerp(col, saturate(_Primary.rgb * lum / 0.85), mask.r);
                col = lerp(col, saturate(_Secondary.rgb * lum / 0.22), mask.g);
                col = lerp(col, saturate(_Tertiary.rgb * lum / 0.6), mask.b);

                half3 n = normalize(i.normal);
                half3 l = normalize(_LightDir.xyz);
                half3 v = normalize(_WorldSpaceCameraPos - i.world);
                half gloss = mask.a;
                half diff = saturate(dot(n, l) * 0.8 + 0.2);
                half3 hemi = lerp(_GroundColor.rgb, _SkyColor.rgb, n.y * 0.5 + 0.5);
                half3 lit = col * (hemi * _Ambient + (1 - _Ambient) * diff) * 1.15;
                half3 h = normalize(l + v);
                lit += pow(saturate(dot(n, h)), lerp(8, 64, gloss)) * _Spec * gloss;
                half fres = 1 - saturate(dot(n, v));
                lit += fres * fres * fres * _Rim * (0.35 + 0.65 * gloss) * _SkyColor.rgb;
                return fixed4(lit * _Tint.rgb, 1);
            }
            ENDCG
        }
    }
    Fallback "MotoManager/BikeLit"
}
