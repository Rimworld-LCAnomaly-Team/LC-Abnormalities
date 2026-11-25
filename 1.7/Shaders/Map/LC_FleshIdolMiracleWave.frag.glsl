#version 450

// Flesh Idol (T-09-79) Miracle Wave, Prayer Halo & Catastrophic Detonation Shader:
// _Mode < 0.5  : Expanding Sacred Miracle Heal Pulse (golden-rose wave + cruciform divine rays)
// _Mode < 1.5  : Continuous Prayer / Overload Ritual Halo beneath the cross
// _Mode >= 1.5 : Catastrophic 90s Detonation Shockwave (searing crimson-white-black burst)
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(1.0, 0.86, 0.48, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(1.0, 0.98, 0.90, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Intensity; // @default = 1.4
    float _Mode;      // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

float hash21(vec2 p)
{
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
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

void main()
{
    vec2 centered = (vUv - 0.5) * 2.0;
    float r = length(centered);
    if (r > 1.0)
        discard;

    float ang = atan(centered.y, centered.x);
    float prog = clamp(u._Progress, 0.01, 1.0);
    float outerSoft = smoothstep(1.0, 0.84, r);

    float energy = 0.0;
    vec3 col = vec3(0.0);

    if (u._Mode < 0.5)
    {
        // Mode 0: Sacred Miracle Heal Pulse (golden ring + cruciform cross-beam radiance)
        float waveR = mix(0.06, 0.94, prog);
        float dr = r - waveR;
        float mainRing = exp(-dr * dr * 80.0);
        float softTrail = exp(-pow(max(0.0, waveR - r) * 4.5, 2.0)) * step(r, waveR) * 0.45;
        // Cruciform 4-way divine cross rays aligned to cardinal axes
        float crossRays = (exp(-abs(centered.x) * 14.0) + exp(-abs(centered.y) * 14.0))
            * smoothstep(0.95, 0.0, r) * (1.0 - prog * 0.60) * 0.72;
        float haloRays = pow(0.5 + 0.5 * cos(ang * 12.0 + u._AgeSecs * 1.4), 3.0)
            * smoothstep(waveR, 0.0, r) * (1.0 - prog * 0.55) * 0.36;
        float coreFlash = exp(-r * r * 12.0) * pow(1.0 - prog, 1.4) * 1.15;

        energy = (mainRing * 1.30 + softTrail + crossRays + haloRays + coreFlash) * outerSoft;
        col = u._Color.rgb * (mainRing * 1.10 + softTrail * 0.90 + haloRays + crossRays * 0.70)
            + u._CoreColor.rgb * (mainRing * 0.65 + crossRays * 0.65 + coreFlash * 1.25);
    }
    else if (u._Mode < 1.5)
    {
        // Mode 1: Continuous Prayer Halo (pulsing ritual ring + inward/outward holy shimmer)
        float danger = clamp(u._Progress, 0.0, 1.0);
        float pulse = 0.75 + 0.25 * sin(u._AgeSecs * mix(3.2, 14.0, danger));
        float ringR = 0.68 + 0.05 * sin(u._AgeSecs * mix(2.5, 11.0, danger));
        float ring = exp(-pow((r - ringR) * 10.5, 2.0));
        float innerRing = exp(-pow((r - 0.36) * 12.0, 2.0)) * 0.65;
        float crossGlow = (exp(-abs(centered.x) * 11.0) + exp(-abs(centered.y) * 11.0))
            * smoothstep(0.85, 0.05, r) * 0.48 * pulse;
        float mist = valueNoise(vec2(ang * 4.0 + u._AgeSecs * mix(0.8, 4.5, danger), r * 5.0))
            * smoothstep(0.90, 0.15, r) * 0.42;

        energy = (ring * 1.05 + innerRing + crossGlow + mist) * pulse * outerSoft;
        col = u._Color.rgb * (ring * 1.05 + mist * 0.95 + crossGlow * 0.75)
            + u._CoreColor.rgb * (innerRing * 0.85 + crossGlow * 0.65);
    }
    else
    {
        // Mode 2: Catastrophic 90s Detonation (jagged crimson-white-black shockwave)
        float waveR = mix(0.05, 0.96, prog);
        float turb = (valueNoise(vec2(ang * 6.0, r * 8.0 - u._AgeSecs * 9.0)) - 0.5) * 0.11;
        float dr = r - (waveR + turb);
        float blastRing = exp(-dr * dr * 72.0);
        float innerFire = exp(-pow(max(0.0, waveR - r) * 3.4, 2.0)) * step(r, waveR) * 0.75;
        float tendons = pow(valueNoise(vec2(ang * 9.0, r * 4.0 - prog * 5.0)), 2.8)
            * smoothstep(waveR + 0.05, 0.0, r) * (1.0 - prog * 0.40) * 1.25;
        float coreWhite = exp(-r * r * 7.5) * pow(1.0 - prog, 1.35) * 1.65;

        energy = (blastRing * 1.55 + innerFire + tendons + coreWhite) * outerSoft;
        col = u._Color.rgb * (blastRing * 1.25 + innerFire * 1.15 + tendons * 1.10)
            + u._CoreColor.rgb * (blastRing * 0.70 + coreWhite * 1.35);
    }

    col *= u._Intensity * u._Alpha * vColor.rgb;
    float alpha = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
