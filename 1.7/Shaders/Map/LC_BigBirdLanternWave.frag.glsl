#version 450

// Expanding hypnotic golden-amber lantern pulse wave (for Big Bird Attract/MindAttack & E.G.O. Lamp 2-cell smash).
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(1.0, 0.78, 0.18, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(1.0, 0.96, 0.78, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Intensity; // @default = 1.4
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
    float waveR = mix(0.06, 0.94, prog);

    // Subtle organic flame turbulence around the ring perimeter
    float turb = (valueNoise(vec2(ang * 3.5, r * 6.0 - u._AgeSecs * 4.5)) - 0.5) * 0.06;
    float dr = r - (waveR + turb);

    // Primary sharp golden crest + secondary trailing hypnotic concentric rings
    float mainRing = exp(-dr * dr * 95.0);
    float innerTrail = exp(-pow(max(0.0, waveR - r) * 5.2, 2.0)) * step(r, waveR) * 0.55;
    float hypnoticRipples = (0.5 + 0.5 * sin((waveR - r) * 34.0 - u._AgeSecs * 9.0)) * innerTrail;

    // Subtle radial light rays radiating from the lantern core
    float rays = pow(0.5 + 0.5 * sin(ang * 12.0 + u._AgeSecs * 2.4), 3.0) * smoothstep(waveR, 0.0, r) * (1.0 - prog * 0.65) * 0.45;
    // Warm core flash early in the wave
    float coreFlash = exp(-r * r * 14.0) * pow(1.0 - prog, 1.6) * 1.15;

    float outerSoft = smoothstep(1.0, 0.86, r);
    float energy = (mainRing * 1.35 + hypnoticRipples * 0.72 + rays + coreFlash) * outerSoft;

    vec3 col = u._Color.rgb * (mainRing * 1.1 + hypnoticRipples * 0.95 + rays * 0.85);
    col += u._CoreColor.rgb * (mainRing * 0.75 + coreFlash * 1.25);
    col *= u._Intensity * u._Alpha * vColor.rgb;

    float alpha = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
