#version 450

// The Heart of Aspiration (T-09-77) Heartbeat Pulse Aura & Frenzy Burst Shader:
// _Mode < 0.5  : Continuous rhythmic heartbeat aura ring under bearer / pedestal
// _Mode < 1.5  : Expanding crimson heartbeat pulse wave (on heartbeat / combat hit)
// _Mode >= 1.5 : Jagged crimson-white frenzy berserk eruption burst
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.96, 0.16, 0.26, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(1.0, 0.84, 0.86, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Intensity; // @default = 1.35
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
        // Mode 0: Continuous bearer/pedestal lub-dub heartbeat aura (_Progress carries beat intensity 0..1)
        float beat = clamp(u._Progress, 0.0, 1.0);
        float ringR = mix(0.46, 0.72, beat);
        float outerRing = exp(-pow((r - ringR) * 11.5, 2.0));
        float echoRing = exp(-pow((r - (ringR * 0.62)) * 13.0, 2.0)) * 0.72;
        float veins = pow(0.5 + 0.5 * sin(ang * 8.0 + u._AgeSecs * 3.8 + r * 6.0), 3.0)
            * smoothstep(0.86, 0.14, r) * (0.28 + 0.52 * beat);
        float softCore = exp(-r * r * 5.2) * (0.28 + 0.42 * beat);

        energy = (outerRing * 1.05 + echoRing + veins + softCore) * outerSoft;
        col = u._Color.rgb * (outerRing * 1.15 + veins * 1.05 + softCore * 0.90)
            + u._CoreColor.rgb * (echoRing * 0.70 + softCore * 0.45);
    }
    else if (u._Mode < 1.5)
    {
        // Mode 1: Expanding crimson heartbeat pulse ring
        float waveR = mix(0.10, 0.94, prog);
        float dr = r - waveR;
        float mainRing = exp(-dr * dr * 95.0);
        // Secondary lub-dub echo ring slightly behind
        float dr2 = r - max(0.04, waveR - 0.15);
        float echoRing = exp(-dr2 * dr2 * 115.0) * 0.65 * (1.0 - prog * 0.3);
        float coreFlash = exp(-r * r * 14.0) * pow(1.0 - prog, 1.7) * 0.85;

        energy = (mainRing * 1.35 + echoRing + coreFlash) * outerSoft;
        col = u._Color.rgb * (mainRing * 1.15 + echoRing * 1.05)
            + u._CoreColor.rgb * (mainRing * 0.55 + coreFlash * 1.15);
    }
    else
    {
        // Mode 2: Jagged Frenzy / Berserk eruption burst
        float waveR = mix(0.08, 0.95, prog);
        float jagged = (valueNoise(vec2(ang * 8.0 + u._AgeSecs * 6.0, r * 6.0)) - 0.5) * 0.14;
        float dr = r - (waveR + jagged);
        float mainRing = exp(-dr * dr * 84.0);
        float spikes = pow(0.5 + 0.5 * sin(ang * 14.0 - u._AgeSecs * 8.0), 4.0)
            * smoothstep(waveR + 0.08, 0.04, r) * (1.0 - prog * 0.50) * 1.25;
        float coreWhite = exp(-r * r * 9.0) * pow(1.0 - prog, 1.35) * 1.50;

        energy = (mainRing * 1.50 + spikes + coreWhite) * outerSoft;
        col = u._Color.rgb * (mainRing * 1.30 + spikes * 1.15)
            + u._CoreColor.rgb * (mainRing * 0.65 + coreWhite * 1.35);
    }

    col *= u._Intensity * u._Alpha * vColor.rgb;
    float alpha = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
