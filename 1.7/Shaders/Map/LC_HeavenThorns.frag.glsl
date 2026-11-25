#version 450

// F-04-42 The Burrowing Heaven E.G.O. Weapon (Heaven) Crimson Lance & Thorny Root Spire Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.88, 0.12, 0.16, 1.0)
    vec4 _CoreColor; // @color @default = vec4(0.14, 0.02, 0.04, 1.0)
    float _Progress; // @default = 0.45
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _Mode;     // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

float hash21(vec2 p)
{
    p = fract(p * vec2(234.34, 435.345));
    p += dot(p, p + 34.23);
    return fract(p.x * p.y);
}

float valueNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float spikeShape(vec2 p, float halfBase, float height)
{
    if (p.y < 0.0 || p.y > height)
        return 0.0;
    float t = p.y / max(0.001, height);
    float w = halfBase * pow(1.0 - t, 0.72) + 0.015 * sin(t * 18.0);
    return smoothstep(w, max(0.0, w - 0.045), abs(p.x));
}

void main()
{
    vec2 uv = vUv;
    vec2 p = (uv - 0.5) * 2.0; // -1..+1
    float prog = clamp(u._Progress, 0.0, 1.0);
    float fade = pow(1.0 - prog, 0.65) * u._Alpha;
    if (fade <= 0.01)
        discard;

    float veins = valueNoise(uv * 11.0 + vec2(u._AgeSecs * 4.0, -u._AgeSecs * 2.5));

    if (u._Mode < 0.5)
    {
        // Mode 0: Horizontal high-velocity spear thrust lance (+X direction)
        float tip = clamp(prog * 2.2, 0.15, 0.96);
        float lenMask = smoothstep(-0.92, -0.70, p.x) * smoothstep(tip + 0.06, tip - 0.04, p.x);
        float along = clamp((p.x + 0.90) / max(0.2, tip + 0.90), 0.0, 1.0);
        float profile = (1.0 - pow(along, 1.3)) * 0.24 + 0.06 * sin(along * 22.0);
        float shaft = smoothstep(profile + 0.04, max(0.0, profile - 0.03), abs(p.y));
        float razorCore = exp(-pow(abs(p.y) / 0.055, 2.0)) * smoothstep(0.0, 0.6, along);
        float flashTip = exp(-pow(length(p - vec2(tip - 0.08, 0.0)) / 0.22, 2.0)) * (1.0 - prog);

        float alpha = clamp((shaft * 0.88 + razorCore * 0.65 + flashTip * 0.9) * lenMask * fade, 0.0, 1.0);
        if (alpha <= 0.01)
            discard;

        vec3 col = mix(u._CoreColor.rgb, u._Color.rgb, clamp(0.45 + 0.55 * veins, 0.0, 1.0)) * shaft;
        col += vec3(1.0, 0.85, 0.78) * (razorCore * 0.95 + flashTip * 1.2);
        outColor = vec4(clamp(col * vColor.rgb, 0.0, 1.0), alpha * u._Color.a * vColor.a);
        return;
    }

    // Mode 1: Erupting crimson thorny root spires rising from blood pool
    float erupt = smoothstep(0.0, 0.18, prog);
    float sCenter = spikeShape(vec2(p.x, (p.y + 0.85) / max(0.12, erupt * 1.65)), 0.24, 1.0);
    float sLeft = spikeShape(vec2((p.x + 0.30) * 1.15 - p.y * 0.16, (p.y + 0.82) / max(0.12, erupt * 1.25)), 0.18, 1.0);
    float sRight = spikeShape(vec2((p.x - 0.30) * 1.15 + p.y * 0.16, (p.y + 0.82) / max(0.12, erupt * 1.32)), 0.18, 1.0);

    float spires = max(sCenter, max(sLeft, sRight));
    float baseRing = exp(-pow(length(vec2(p.x, (p.y + 0.76) * 2.2)) / mix(0.25, 0.85, prog), 2.0));
    float outline = spires * (1.0 - 0.35 * veins);
    float coreGlow = (spires * 0.65 + baseRing * 0.55) * (0.6 + 0.4 * veins);

    float totalA = clamp((outline * 0.95 + baseRing * 0.60) * fade, 0.0, 1.0);
    if (totalA <= 0.01)
        discard;

    vec3 spireCol = mix(u._CoreColor.rgb, u._Color.rgb, clamp(veins * 0.9 + 0.25, 0.0, 1.0)) * spires;
    spireCol += vec3(1.0, 0.42, 0.38) * (coreGlow * 0.55 + baseRing * 0.55);
    outColor = vec4(clamp(spireCol * vColor.rgb, 0.0, 1.0), totalA * u._Color.a * vColor.a);
}
