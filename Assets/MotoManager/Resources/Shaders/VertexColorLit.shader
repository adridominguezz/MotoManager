// Cheap mobile shader for the race view: vertex colour with a fixed directional light and a little ambient.
// No textures, no real-time lights, one pass -> track and bikes render in very few draw calls.
// Lives in Resources so it is always included in builds (loaded with Resources.Load<Shader>).
Shader "MotoManager/VertexColorLit"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _LightDir ("Light direction", Vector) = (0.35, 0.85, 0.4, 0)
        _Ambient ("Ambient", Range(0,1)) = 0.55
        // Depth bias for flat layers drawn on top of each other (track surfaces, paint): 0 for everything else.
        _OffsetFactor ("Offset factor", Float) = 0
        _OffsetUnits ("Offset units", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Pass
        {
            Offset [_OffsetFactor], [_OffsetUnits]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Tint;
            float4 _LightDir;
            half _Ambient;

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
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                half diff = saturate(dot(n, normalize(_LightDir.xyz)));
                o.color = v.color * _Tint * (_Ambient + (1 - _Ambient) * diff);
                o.color.a = 1;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
