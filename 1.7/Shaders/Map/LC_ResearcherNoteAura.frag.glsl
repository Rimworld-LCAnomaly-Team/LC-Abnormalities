#version 450

// Notes from a Crazed Researcher (T-09-78) Aura, Insight Pulse & Gore Detonation Shader:
// _Mode < 0.5 : Continuous manic aura under bearer/pedestal (_Progress carries dangerAmt 0..1)
// _Mode < 1.5 : Expanding study-insight / safe-return / equip ripple
// _Mode >= 1.5: Swelling gore-flesh explosion shockwave
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.45, 0.95, 0.42, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(0.92, 1.0, 0.85, 1.0)
    float _Progress;  // @default = 0.0
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Intensity; // @default = 1.30
    float _Mode;      // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

float hash21(vec2 p)
{
    p = fract(p * vec2(127.1, 311.7));
    p += dot(p, p + 31.17);
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
    float outerSoft = smoothstep(1.0, 0.82, r);

    float energy = 0.0;
    vec3 col = vec3(0.0);

    if (u._Mode < 0.5)
    {
        // Mode 0: Continuous manic aura (_Progress carries danger 0..1)
        float danger = clamp(u._Progress, 0.0, 1.0);
        float scribble = 0.5 + 0.5 * sin(ang * 9.0 + u._AgeSecs * (2.6 + 6.5 * danger) + r * 5.0);
        float ringJitter = (valueNoise(vec2(ang * 4.0 + u._AgeSecs * 2.5, r * 4.0)) - 0.5) * 0.12 * danger;
        float ringR = mix(0.56, 0.78, 0.5 + 0.5 * sin(u._AgeSecs * (2.5 + 8.0 * danger))) + ringJitter;
        float mainRing = exp(-pow((r - ringR) * 12.0, 2.0));
        float innerRing = exp(-pow((r - 0.36) * 14.0, 2.0)) * 0.65;
        float runes = pow(scribble, 3.0) * smoothstep(0.86, 0.15, r) * (0.28 + 0.45 * danger);
        float core = exp(-r * r * 4.8) * (0.25 + 0.35 * danger);

        energy = (mainRing + innerRing + runes + core) * outerSoft;
        col = u._Color.rgb * (mainRing * 1.10 + runes * 1.15 + core)
            + u._CoreColor.rgb * (innerRing * 0.70 + core * 0.40);
    }
    else if (u._Mode < 1.5)
    {
        // Mode 1: Expanding insight / page-turn / safe-return pulse ring
        float waveR = mix(0.10, 0.94, prog);
        float dr = r - waveR;
        float mainRing = exp(-dr * dr * 92.0);
        float echoRing = exp(-pow((r - max(0.04, waveR - 0.14)) * 110.0, 2.0)) * 0.62 * (1.0 - prog * 0.3);
        float coreFlash = exp(-r * r * 14.0) * pow(1.0 - prog, 1.6) * 0.85;

        energy = (mainRing * 1.30 + echoRing + coreFlash) * outerSoft;
        col = u._Color.rgb * (mainRing * 1.15 + echoRing * 1.05)
            + u._CoreColor.rgb * (mainRing * 0.50 + coreFlash * 1.10);
    }
    else
    {
        // Mode 2: Jagged swelling gore explosion shockwave
        float waveR = mix(0.06, 0.96, prog);
        float jagged = (valueNoise(vec2(ang * 8.5 + u._AgeSecs * 6.0, r * 6.5)) - 0.5) * 0.18;
        float dr = r - (waveR + jagged);
        float mainRing = exp(-dr * dr * 78.0);
        float burstRays = pow(0.5 + 0.5 * sin(ang * 14.0 - u._AgeSecs * 7.0), 3.5)
            * smoothstep(waveR + 0.08, 0.04, r) * (1.0 - prog * 0.45) * 1.35;
        float coreFlash = exp(-r * r * 8.5) * pow(1.0 - prog, 1.3) * 1.55;

        energy = (mainRing * 1.50 + burstRays + coreFlash) * outerSoft;
        col = u._Color.rgb * (mainRing * 1.35 + burstRays * 1.25)
            + u._CoreColor.rgb * (mainRing * 0.65 + coreFlash * 1.35);
    }

    col *= u._Intensity * u._Alpha * vColor.rgb;
    float alpha = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
