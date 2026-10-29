// Durchscheinendes Papier (Shoji): Licht von hinten scheint durch (diffuse Transmission),
// die Bildseite zeigt den Lichtfleck weich durch das Papier. Schatten nur teilweise (Rasterung → weich gefiltert).
// Forward-Renderpfad (auch in WebGL), Lichtabfall 1/r² passend zum Studio-Deferred-Shader.
Shader "Studio/TranslucentPaper" {
Properties {
    _Color ("Farbe", Color) = (1,1,1,1)
    _MainTex ("Papier", 2D) = "white" {}
    _Transmission ("Durchlässigkeit", Range(0,1)) = 0.6
    _Glossiness ("Glätte", Range(0,1)) = 0.05
}
SubShader {
    Tags { "RenderType"="Opaque" "Queue"="Geometry" }
    Cull Off

    CGPROGRAM
    #pragma surface surf Paper fullforwardshadows
    #pragma target 3.0

    sampler2D _MainTex;
    fixed4 _Color;
    half _Transmission;
    float _StudioPhysicalFalloff;
    float _StudioFalloffScale;

    struct Input {
        float2 uv_MainTex;
        float3 worldPos;
        float facing : VFACE;
    };

    struct SurfaceOutputPaper {
        fixed3 Albedo;
        fixed3 Normal;
        fixed3 Emission;
        half Specular;
        fixed Gloss;
        fixed Alpha;
        float3 WorldPos;
    };

    half4 LightingPaper (SurfaceOutputPaper s, half3 lightDir, half atten)
    {
        float corr = 1.0;
    #if defined (POINT) || defined (POINT_COOKIE) || defined (SPOT)
        if (_StudioPhysicalFalloff > 0.5)
        {
            // gleiche Korrektur wie im Studio-Deferred-Shader: Unity-Abfallkurve → 1/r²
            float3 tl = s.WorldPos - _WorldSpaceLightPos0.xyz;
            float d2 = dot(tl, tl);
            float4 lc = mul(unity_WorldToLight, float4(s.WorldPos, 1));
            float idx = dot(lc.xyz, lc.xyz);
        #if defined (POINT)
            float uf = tex2D(_LightTexture0, idx.rr).r;     // Punktlicht: Abfall-LUT liegt in _LightTexture0
        #else
            float uf = tex2D(_LightTextureB0, idx.rr).r;    // Spot / Punkt mit Cookie
        #endif
            float w = saturate(1.0 - idx * idx);
            w *= w;
            corr = uf > 1e-3 ? (_StudioFalloffScale / max(d2, 0.09) * w) / uf : 0.0;
        }
    #endif
        half ndl = dot(s.Normal, lightDir);
        half front = max(0, ndl);                       // Licht auf dieser Seite
        half back = max(0, -ndl) * _Transmission;       // Licht von hinten, durchs Papier gestreut
        half4 c;
        c.rgb = s.Albedo * _LightColor0.rgb * (front + back) * atten * corr;
        c.a = s.Alpha;
        return c;
    }

    void surf (Input IN, inout SurfaceOutputPaper o)
    {
        fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
        o.Albedo = c.rgb;
        o.Alpha = 1;
        // Rückseite: Normale umdrehen (beidseitig beleuchtet)
        o.Normal = float3(0, 0, IN.facing > 0 ? 1 : -1);
        o.WorldPos = IN.worldPos;
    }
    ENDCG

    // Schattenwurf: gerastert nach Durchlässigkeit → weicher Halbschatten statt voller Abdeckung
    Pass {
        Name "ShadowCaster"
        Tags { "LightMode" = "ShadowCaster" }
        Cull Off
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.0
        #pragma multi_compile_shadowcaster
        #include "UnityCG.cginc"
        half _Transmission;
        struct v2f {
            V2F_SHADOW_CASTER;
        };
        v2f vert (appdata_base v)
        {
            v2f o;
            TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
            return o;
        }
        float4 frag (v2f i) : SV_Target
        {
            // 4×4-Bayer-Raster: Anteil (1 - Transmission) der Texel wirft Schatten
            float2 p = floor(fmod(i.pos.xy, 4.0));
            float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            float t = (bayer[(int)(p.y * 4 + p.x)] + 0.5) / 16.0;
            clip((1.0 - _Transmission) - t);
            SHADOW_CASTER_FRAGMENT(i)
        }
        ENDCG
    }
}
Fallback Off
}
