#version 450

// T-02-99 Void Dream Hypnotic Sonic Crow Wave & E.G.O. "Engulfing Dream" (BakuWeaponEffect) Sonic Ripple Shader.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.66, 0.34, 0.96, 1.0)
    vec4 _AccentCol;  // @color @default = vec4(0.96, 0.28, 0.62, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(0.98, 0.95, 1.0, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Intensity; // @default = 1.45
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
    float waveR = mix(0.05, 0.94, prog);

    // Harmonic acoustic ripple distortion around the sonic shockwave perimeter
    float harmonic = sin(ang * 8.0 + u._AgeSecs * 9.0) * 0.022
                   + (valueNoise(vec2(ang * 4.0, r * 7.0 - u._AgeSecs * 5.0)) - 0.5) * 0.05;
    float dr = r - (waveR + harmonic);

    // Primary sharp sonic crest + nested hypnotic echo rings
    float mainRing = exp(-dr * dr * 105.0);
    float echoRing = exp(-pow((r - max(0.04, waveR - 0.14)) * 15.0, 2.0)) * step(0.16, waveR) * 0.72;
    float innerTrail = exp(-pow(max(0.0, waveR - r) * 4.8, 2.0)) * step(r, waveR) * 0.52;
    float dreamRipples = (0.5 + 0.5 * sin((waveR - r) * 38.0 - u._AgeSecs * 11.0)) * innerTrail;

    // Subtle hypnotic acoustic rays & early center flare
    float rays = pow(0.5 + 0.5 * sin(ang * 10.0 - u._AgeSecs * 4.0), 3.0) * smoothstep(waveR, 0.0, r) * (1.0 - prog * 0.65) * 0.42;
    float coreFlash = exp(-r * r * 14.0) * pow(1.0 - prog, 1.55) * 1.15;

    float outerSoft = smoothstep(1.0, 0.85, r);
    float colorBlend = 0.5 + 0.5 * sin(ang * 3.0 + prog * 6.28318);
    vec3 waveHue = mix(u._Color.rgb, u._AccentCol.rgb, colorBlend * 0.48);

    vec3 col = waveHue * (mainRing * 1.15 + echoRing * 0.95 + dreamRipples * 0.88 + rays * 0.80);
    col += u._CoreColor.rgb * (mainRing * 0.72 + coreFlash * 1.25);
    col *= u._Intensity * u._Alpha * outerSoft * vColor.rgb;

    float energy = (mainRing * 1.35 + echoRing * 0.80 + dreamRipples * 0.70 + rays + coreFlash) * outerSoft;
    float alpha = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
