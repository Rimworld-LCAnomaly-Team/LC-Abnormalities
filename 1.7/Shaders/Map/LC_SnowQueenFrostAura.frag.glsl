#version 450

// The Snow Queen (F-01-37) Frost Sigil, Kiss Burst, Spring Rose Thaw & Ice Shatter Shader:
// _Mode < 0.5 : Continuous crystalline frost sigil ring (containment / duel arena)
// _Mode < 1.5 : Expanding frost-kiss / Frost Splinter impact wave
// _Mode < 2.5 : Duel victory "Rose Bloom & Spring Thaw" warm floral burst
// _Mode >= 2.5: Duel defeat / Third Kiss lethal crystalline shatter burst
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.42, 0.88, 1.0, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(0.92, 0.98, 1.0, 1.0)
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
    float outerSoft = smoothstep(1.0, 0.80, r);

    float energy = 0.0;
    vec3 col = vec3(0.0);

    if (u._Mode < 0.5)
    {
        // Mode 0: Rotating 6-fold hexagonal snowflake & frost aura (_Progress carries duel/urgency 0..1)
        float rotAng = ang + u._AgeSecs * (0.45 + 0.75 * u._Progress);
        float hex6 = pow(0.5 + 0.5 * cos(rotAng * 6.0), 4.0);
        float hex12 = pow(0.5 + 0.5 * sin(rotAng * 12.0 - r * 5.0), 3.0);
        float outerRing = exp(-pow((r - 0.78) * 14.0, 2.0));
        float innerRing = exp(-pow((r - 0.46) * 15.0, 2.0)) * 0.80;
        float crystalSpokes = hex6 * smoothstep(0.88, 0.14, r) * (0.38 + 0.45 * u._Progress);
        float shimmer = hex12 * smoothstep(0.65, 0.10, r) * 0.25;
        float softMist = exp(-r * r * 3.8) * 0.30;

        energy = (outerRing + innerRing + crystalSpokes + shimmer + softMist) * outerSoft;
        col = u._Color.rgb * (outerRing * 1.10 + crystalSpokes * 1.25 + softMist)
            + u._CoreColor.rgb * (innerRing * 0.75 + shimmer + crystalSpokes * 0.40);
    }
    else if (u._Mode < 1.5)
    {
        // Mode 1: Expanding glacial kiss / Frost Splinter burst
        float waveR = mix(0.08, 0.94, prog);
        float dr = r - waveR;
        float mainRing = exp(-dr * dr * 92.0);
        float sixRays = pow(0.5 + 0.5 * cos(ang * 6.0 + u._AgeSecs * 1.5), 5.0)
            * smoothstep(waveR + 0.10, 0.02, r) * (1.0 - prog * 0.45);
        float coreFlash = exp(-r * r * 14.0) * pow(1.0 - prog, 1.6) * 0.90;

        energy = (mainRing * 1.35 + sixRays * 1.15 + coreFlash) * outerSoft;
        col = u._Color.rgb * (mainRing * 1.15 + sixRays * 1.20)
            + u._CoreColor.rgb * (mainRing * 0.55 + coreFlash * 1.15 + sixRays * 0.35);
    }
    else if (u._Mode < 2.5)
    {
        // Mode 2: Duel victory "Rose Bloom & Spring Thaw" (crimson rose petals + golden thaw radiance)
        float waveR = mix(0.06, 0.95, prog);
        float petals = pow(0.5 + 0.5 * cos(ang * 5.0 - r * 3.2 + u._AgeSecs * 1.8), 2.5);
        float dr = r - waveR;
        float mainRing = exp(-dr * dr * 76.0);
        float roseBloom = petals * smoothstep(waveR + 0.14, 0.05, r) * (1.0 - prog * 0.35) * 1.20;
        float innerWarm = exp(-r * r * 9.0) * pow(1.0 - prog, 1.35) * 1.15;

        energy = (mainRing * 1.25 + roseBloom + innerWarm) * outerSoft;
        col = u._Color.rgb * (mainRing * 1.10 + roseBloom * 1.25)
            + u._CoreColor.rgb * (innerWarm * 1.25 + mainRing * 0.50);
    }
    else
    {
        // Mode 3: Duel defeat / Third Kiss jagged ice-shatter explosion
        float waveR = mix(0.08, 0.95, prog);
        float jagged = (valueNoise(vec2(ang * 9.0 + u._AgeSecs * 4.0, r * 6.0)) - 0.5) * 0.16;
        float dr = r - (waveR + jagged);
        float mainRing = exp(-dr * dr * 88.0);
        float spikes = pow(0.5 + 0.5 * sin(ang * 12.0), 4.0)
            * smoothstep(waveR + 0.10, 0.03, r) * (1.0 - prog * 0.42) * 1.35;
        float coreFlash = exp(-r * r * 10.0) * pow(1.0 - prog, 1.4) * 1.45;

        energy = (mainRing * 1.45 + spikes + coreFlash) * outerSoft;
        col = u._Color.rgb * (mainRing * 1.25 + spikes * 1.20)
            + u._CoreColor.rgb * (mainRing * 0.65 + coreFlash * 1.30);
    }

    col *= u._Intensity * u._Alpha * vColor.rgb;
    float alpha = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
