// Bikes and riders (race view and the livery showcase): vertex colour, lit per pixel with a fixed directional light,
// a sky/ground hemisphere ambient, a Blinn highlight and a fresnel rim. The vertex colour's alpha is the gloss
// (255 = lacquered fairing, ~90 = leathers, ~30 = rubber), so one material draws every part in one pass.
// No textures, no real-time lights. Lives in Resources so it is always included in builds.
Shader "MotoManager/BikeLit"
{
    Properties
    {
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
        LOD 150
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

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
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                half3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half3 n = normalize(i.normal);
                half3 l = normalize(_LightDir.xyz);
                half3 v = normalize(_WorldSpaceCameraPos - i.world);
                half gloss = i.color.a;
                half diff = saturate(dot(n, l) * 0.8 + 0.2);
                half3 hemi = lerp(_GroundColor.rgb, _SkyColor.rgb, n.y * 0.5 + 0.5);
                half3 col = i.color.rgb * (hemi * _Ambient + (1 - _Ambient) * diff);
                half3 h = normalize(l + v);
                col += pow(saturate(dot(n, h)), lerp(8, 64, gloss)) * _Spec * gloss;
                half fres = 1 - saturate(dot(n, v));
                col += fres * fres * fres * _Rim * (0.35 + 0.65 * gloss) * _SkyColor.rgb;
                return fixed4(col * _Tint.rgb, 1);
            }
            ENDCG
        }
    }
    Fallback "MotoManager/VertexColorLit"
}
