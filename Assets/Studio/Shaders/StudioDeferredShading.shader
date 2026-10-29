// Ersatz für Unitys Deferred-Lighting-Shader (Built-in) mit physikalischem Lichtabfall:
// Punkt- und Spotlichter fallen mit 1/r² ab (Inverse-Square-Law), am Ende der Reichweite weich ausgeblendet.
// Cookies (Torblende), Schatten und das Standard-BRDF bleiben unverändert.
// Aktiviert über GraphicsSettings (BuildScript) — funktioniert auch in WebGL.
Shader "Hidden/Studio-DeferredShading" {
Properties {
    _LightTexture0 ("", any) = "" {}
    _LightTextureB0 ("", 2D) = "" {}
    _ShadowMapTexture ("", any) = "" {}
    _SrcBlend ("", Float) = 1
    _DstBlend ("", Float) = 1
}
SubShader {

// Pass 1: Licht in den Lichtpuffer akkumulieren
Pass {
    ZWrite Off
    Blend [_SrcBlend] [_DstBlend]

CGPROGRAM
#pragma target 3.0
#pragma vertex vert_deferred
#pragma fragment frag
#pragma multi_compile_lightpass
#pragma multi_compile ___ UNITY_HDR_ON

#pragma exclude_renderers nomrt

#include "UnityCG.cginc"
#include "UnityDeferredLibrary.cginc"
#include "UnityPBSLighting.cginc"
#include "UnityStandardUtils.cginc"
#include "UnityGBuffer.cginc"
#include "UnityStandardBRDF.cginc"

sampler2D _CameraGBufferTexture0;
sampler2D _CameraGBufferTexture1;
sampler2D _CameraGBufferTexture2;

// 1 = physikalischer Abfall an, 0 = Unity-Standardkurve; Skala in „Lichteinheiten pro m²“
float _StudioPhysicalFalloff;
float _StudioFalloffScale;

// Ersetzt Unitys Abfall-Lookup (_LightTextureB0) durch 1/r² mit weichem Fenster an der Reichweite.
float StudioFalloffCorrection (float3 wpos)
{
#if defined (POINT) || defined (POINT_COOKIE) || defined (SPOT)
    if (_StudioPhysicalFalloff < 0.5) return 1.0;
    float3 tolight = wpos - _LightPos.xyz;
    float d2 = dot(tolight, tolight);
    float att = d2 * _LightPos.w;               // _LightPos.w = 1 / Reichweite²
    float unityFalloff = tex2D (_LightTextureB0, att.rr).r;
    if (unityFalloff < 1e-3) return 0.0;
    float window = saturate(1.0 - att * att);   // (1 - (d/r)^4)
    window *= window;
    float physical = _StudioFalloffScale / max(d2, 0.09) * window;
    return physical / unityFalloff;
#else
    return 1.0;
#endif
}

half4 CalculateLight (unity_v2f_deferred i)
{
    float3 wpos;
    float2 uv;
    float atten, fadeDist;
    UnityLight light;
    UNITY_INITIALIZE_OUTPUT(UnityLight, light);
    UnityDeferredCalculateLightParams (i, wpos, uv, light.dir, atten, fadeDist);

    atten *= StudioFalloffCorrection(wpos);
    light.color = _LightColor.rgb * atten;

    half4 gbuffer0 = tex2D (_CameraGBufferTexture0, uv);
    half4 gbuffer1 = tex2D (_CameraGBufferTexture1, uv);
    half4 gbuffer2 = tex2D (_CameraGBufferTexture2, uv);
    UnityStandardData data = UnityStandardDataFromGbuffer(gbuffer0, gbuffer1, gbuffer2);

    float3 eyeVec = normalize(wpos-_WorldSpaceCameraPos);
    half oneMinusReflectivity = 1 - SpecularStrength(data.specularColor.rgb);

    UnityIndirect ind;
    UNITY_INITIALIZE_OUTPUT(UnityIndirect, ind);
    ind.diffuse = 0;
    ind.specular = 0;

    half4 res = UNITY_BRDF_PBS (data.diffuseColor, data.specularColor, oneMinusReflectivity, data.smoothness, data.normalWorld, -eyeVec, light, ind);
    return res;
}

#ifdef UNITY_HDR_ON
half4
#else
fixed4
#endif
frag (unity_v2f_deferred i) : SV_Target
{
    half4 c = CalculateLight(i);
    #ifdef UNITY_HDR_ON
    return c;
    #else
    return exp2(-c);
    #endif
}

ENDCG
}

// Pass 2: LDR-Dekodierung des Lichtpuffers (wie im Original)
Pass {
    ZTest Always Cull Off ZWrite Off
    Stencil {
        ref [_StencilNonBackground]
        readmask [_StencilNonBackground]
        compback equal
        compfront equal
    }

CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#pragma exclude_renderers nomrt

#include "UnityCG.cginc"

sampler2D _LightBuffer;
struct v2f {
    float4 vertex : SV_POSITION;
    float2 texcoord : TEXCOORD0;
};

v2f vert (float4 vertex : POSITION, float2 texcoord : TEXCOORD0)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(vertex);
    o.texcoord = texcoord.xy;
#ifdef UNITY_SINGLE_PASS_STEREO
    o.texcoord = TransformStereoScreenSpaceTex(o.texcoord, 1.0f);
#endif
    return o;
}

fixed4 frag (v2f i) : SV_Target
{
    return -log2(tex2D(_LightBuffer, i.texcoord));
}
ENDCG
}

}
Fallback Off
}
