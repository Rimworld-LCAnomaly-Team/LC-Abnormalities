#version 450

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST;     // @default = vec4(0.0, 0.0, 1.0, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _CrimsonAmount; // @default = 0.0
    float _RevealAmount;  // @default = 1.0
} uMaterial;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

float hash12(vec2 p)
{
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

float valueNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    float a = hash12(i);
    float b = hash12(i + vec2(1.0, 0.0));
    float c = hash12(i + vec2(0.0, 1.0));
    float d = hash12(i + vec2(1.0, 1.0));
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float luminance(vec3 c)
{
    return dot(c, vec3(0.2126, 0.7152, 0.0722));
}

vec4 samplePortrait(vec2 canvasUv)
{
    vec2 clamped = clamp(canvasUv, vec2(0.0), vec2(1.0));
    vec2 stUv = clamped * uMaterial._MainTex_ST.zw + uMaterial._MainTex_ST.xy;
    return texture(_MainTex, stUv);
}

void main()
{
    float age = uMaterial._AgeSecs;
    float crimson = clamp(uMaterial._CrimsonAmount, 0.0, 1.0);
    float reveal = clamp(uMaterial._RevealAmount, 0.0, 1.0);
    if (reveal <= 0.001)
        discard;

    // 1. Subtle eerie oil-impasto micro-warp and crimson downward smear in clean canvas UV space.
    float brushNoise = valueNoise(vec2(vUv.x * 26.0, vUv.y * 10.0));
    float sway = sin(vUv.x * 18.0 + age * 0.9) * 0.0022 + sin(vUv.y * 22.0 - age * 0.65) * 0.0014;
    float impastoWarp = (brushNoise - 0.5) * 0.0055;
    float dripPull = crimson * (valueNoise(vec2(vUv.x * 32.0, age * 0.15)) * 0.018) * smoothstep(0.75, 0.10, vUv.y);
    vec2 baseUv = vUv + vec2(sway + impastoWarp * 0.5, impastoWarp + dripPull);

    // 2. Vertical-biased 9-tap painterly oil-smudge blur & charcoal contour detection.
    vec2 texSize = max(vec2(textureSize(_MainTex, 0)), vec2(1.0));
    vec2 texel = 1.0 / texSize;
    vec2 smudgeStep = texel * vec2(1.35, 2.35);

    vec4 accum = vec4(0.0);
    float wSum = 0.0;
    for (int ix = -1; ix <= 1; ++ix)
    {
        for (int iy = -1; iy <= 1; ++iy)
        {
            float w = (ix == 0 && iy == 0) ? 3.0 : (ix == 0 ? 1.8 : 1.0);
            vec4 s = samplePortrait(baseUv + vec2(float(ix), float(iy)) * smudgeStep);
            accum += vec4(s.rgb * s.a, s.a) * w;
            wSum += w;
        }
    }

    float blurredAlpha = accum.a / max(wSum, 0.0001);
    vec3 blurredRgb = accum.rgb / max(accum.a, 0.0001);

    vec4 cL = samplePortrait(baseUv - vec2(texel.x * 1.6, 0.0));
    vec4 cR = samplePortrait(baseUv + vec2(texel.x * 1.6, 0.0));
    vec4 cD = samplePortrait(baseUv - vec2(0.0, texel.y * 1.6));
    vec4 cU = samplePortrait(baseUv + vec2(0.0, texel.y * 1.6));

    float lumL = luminance(cL.rgb) * cL.a;
    float lumR = luminance(cR.rgb) * cR.a;
    float lumD = luminance(cD.rgb) * cD.a;
    float lumU = luminance(cU.rgb) * cU.a;
    float edge = clamp(length(vec2(lumR - lumL, lumU - lumD)) * 2.8, 0.0, 1.0);

    // 3. Tonal posterization + aged ochre oil-paint / crimson-stained color grading.
    float lum = clamp(luminance(blurredRgb), 0.0, 1.0);
    float posterLum = mix(lum, floor(lum * 5.0 + 0.5) / 5.0, 0.42);
    float contrastLum = clamp((posterLum - 0.5) * 1.25 + 0.5, 0.0, 1.0);

    vec3 normalShadow = vec3(0.10, 0.07, 0.05);
    vec3 normalMid = vec3(0.55, 0.43, 0.26);
    vec3 normalLight = vec3(0.84, 0.72, 0.49);
    vec3 normalRamp = contrastLum < 0.5
        ? mix(normalShadow, normalMid, contrastLum * 2.0)
        : mix(normalMid, normalLight, (contrastLum - 0.5) * 2.0);

    vec3 crimsonShadow = vec3(0.08, 0.01, 0.01);
    vec3 crimsonMid = vec3(0.54, 0.11, 0.09);
    vec3 crimsonLight = vec3(0.80, 0.46, 0.40);
    vec3 crimsonRamp = contrastLum < 0.5
        ? mix(crimsonShadow, crimsonMid, contrastLum * 2.0)
        : mix(crimsonMid, crimsonLight, (contrastLum - 0.5) * 2.0);

    vec3 mutedHue = mix(vec3(contrastLum), blurredRgb, 0.25);
    vec3 normalOil = mix(normalRamp, normalRamp * (0.65 + 0.70 * mutedHue), 0.45);
    vec3 crimsonOil = mix(crimsonRamp, crimsonRamp * (0.62 + 0.55 * mutedHue), 0.35);
    vec3 paintedRgb = mix(normalOil, crimsonOil, crimson);

    // Deepen charcoal-ink / clotted-blood facial & hair contours.
    vec3 contourInk = mix(vec3(0.06, 0.04, 0.03), vec3(0.05, 0.005, 0.005), crimson);
    paintedRgb = mix(paintedRgb, contourInk, edge * mix(0.68, 0.82, crimson));

    // Subtle procedural woven oil-canvas linen texture.
    float weaveX = sin(vUv.x * 165.0 + vUv.y * 12.0) * 0.5 + 0.5;
    float weaveY = sin(vUv.y * 235.0 - vUv.x * 8.0) * 0.5 + 0.5;
    float linen = (weaveX * 0.5 + weaveY * 0.5 - 0.5) * 0.065;
    paintedRgb += linen * mix(0.8, 1.0, contrastLum);

    // 4. Painterly soft vignette inside the easel canvas bounds + crimson weeping glaze.
    float roughMargin = (brushNoise - 0.5) * 0.04;
    float maskX = smoothstep(0.04, 0.18, vUv.x + roughMargin) * smoothstep(0.04, 0.18, 1.0 - vUv.x - roughMargin);
    float maskY = smoothstep(0.02, 0.15, vUv.y + roughMargin) * smoothstep(0.05, 0.19, 1.0 - vUv.y + roughMargin);
    float canvasMask = maskX * maskY;

    float radialDist = length((vUv - vec2(0.50, 0.53)) * vec2(1.25, 1.0));
    float vignette = smoothstep(0.75, 0.22, radialDist + (brushNoise - 0.5) * 0.10);

    // Procedural weeping blood streaks falling from eye-level when crimsonStained > 0.
    float streakBand = smoothstep(0.66, 0.44, vUv.y) * smoothstep(0.04, 0.25, vUv.y);
    float streakPattern = smoothstep(0.62, 0.88, valueNoise(vec2(vUv.x * 28.0 + 3.1, vUv.y * 3.5 + age * 0.18)));
    float eyeAlign = exp(-pow((abs(vUv.x - 0.50) - 0.12) * 8.5, 2.0));
    float dripMask = crimson * streakBand * (0.45 * streakPattern + 0.55 * streakPattern * eyeAlign);

    float figureAlpha = smoothstep(0.08, 0.82, blurredAlpha) * (0.35 + 0.65 * vignette);
    float glazeAlpha = crimson * 0.22 * (0.72 + 0.28 * sin(age * 1.9)) * (1.0 - vignette * 0.4);

    vec3 crimsonGlazeColor = mix(vec3(0.28, 0.02, 0.02), vec3(0.42, 0.03, 0.03), brushNoise);
    vec3 finalRgb = mix(crimsonGlazeColor, paintedRgb, clamp(figureAlpha * 1.25, 0.0, 1.0));
    finalRgb = mix(finalRgb, vec3(0.22, 0.01, 0.01), clamp(dripMask * 0.85, 0.0, 0.85));

    float finalAlpha = clamp(max(figureAlpha * 0.90, max(glazeAlpha, dripMask * 0.78)), 0.0, 1.0) * canvasMask * reveal;
    if (finalAlpha < 0.01)
        discard;

    outColor = vec4(finalRgb, finalAlpha) * uMaterial._Color * vColor;
}
