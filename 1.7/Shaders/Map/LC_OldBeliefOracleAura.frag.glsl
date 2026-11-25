#version 450

// Old Belief and Promise (T-09-97) Oracle Aura & Judgment Wave:
// _Mode < 0.5  : Cyan-Blue chanting vortex (3_Doing) with rhythmic oracle shimmer
// _Mode < 1.5  : Radiant Gold blessing burst (4_Sucess) with starburst halo rays
// _Mode >= 1.5 : Pitch-Dark Violet-Black betrayal eclipse shockwave (4_Fail) with jagged cracks
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.22, 0.80, 1.0, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(0.95, 0.98, 1.0, 1.0)
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
        // Mode 0: Active 3_Doing oracle vortex (swirling cyan-blue spiral + flashing inner halo)
        float swirl = sin(ang * 5.0 + r * 9.0 - u._AgeSecs * 7.5) * 0.5 + 0.5;
        float spiralBand = pow(swirl, 2.2) * smoothstep(0.92, 0.18, r) * smoothstep(0.05, 0.30, r);
        float ring1 = exp(-pow((r - (0.58 + 0.06 * sin(u._AgeSecs * 6.0))) * 9.5, 2.0));
        float ring2 = exp(-pow((r - (0.34 + 0.05 * cos(u._AgeSecs * 8.5))) * 12.0, 2.0));
        float strobe = 0.78 + 0.22 * sin(u._AgeSecs * 14.0);
        float coreGlow = exp(-r * r * 5.5) * 0.72 * strobe;

        energy = (spiralBand * 0.75 + ring1 * 0.95 + ring2 * 0.80 + coreGlow) * outerSoft;
        col = u._Color.rgb * (spiralBand * 0.95 + ring1 * 1.10 + coreGlow * 0.85)
            + u._CoreColor.rgb * (ring2 * 0.75 + coreGlow * 0.65);
    }
    else if (u._Mode < 1.5)
    {
        // Mode 1: Radiant Gold blessing burst (expanding golden ring + 8-point starburst rays)
        float waveR = mix(0.08, 0.94, prog);
        float dr = r - waveR;
        float mainRing = exp(-dr * dr * 88.0);
        float innerHalo = exp(-pow(max(0.0, waveR - r) * 4.2, 2.0)) * step(r, waveR) * 0.55;
        float starRays = pow(0.5 + 0.5 * cos(ang * 8.0 + u._AgeSecs * 1.6), 4.0)
            * smoothstep(0.95, 0.0, r) * (1.0 - prog * 0.55) * 0.88;
        float fineRays = pow(0.5 + 0.5 * sin(ang * 16.0 - u._AgeSecs * 3.0), 3.0)
            * smoothstep(0.75, 0.0, r) * (1.0 - prog * 0.65) * 0.42;
        float coreFlash = exp(-r * r * 11.0) * pow(1.0 - prog, 1.35) * 1.35;

        energy = (mainRing * 1.35 + innerHalo + starRays + fineRays + coreFlash) * outerSoft;
        col = u._Color.rgb * (mainRing * 1.15 + innerHalo * 0.95 + starRays * 1.10 + fineRays * 0.85)
            + u._CoreColor.rgb * (mainRing * 0.65 + coreFlash * 1.35 + starRays * 0.40);
    }
    else
    {
        // Mode 2: Pitch-Dark Betrayal Eclipse (shattering violet-indigo fracture wave + dark distortion)
        float waveR = mix(0.10, 0.93, prog);
        float jagged = (valueNoise(vec2(ang * 6.5 + u._AgeSecs * 5.0, r * 7.0)) - 0.5) * 0.12;
        float dr = r - (waveR + jagged);
        float crackRing = exp(-dr * dr * 110.0);
        float shatterSpokes = pow(valueNoise(vec2(ang * 10.0, u._AgeSecs * 2.5)), 3.2)
            * smoothstep(waveR + 0.08, 0.05, r) * (1.0 - prog * 0.45) * 1.15;
        float eclipseRim = exp(-pow((r - 0.36 * (1.0 - prog * 0.4)) * 11.0, 2.0)) * (1.0 - prog * 0.5);

        energy = (crackRing * 1.40 + shatterSpokes * 0.85 + eclipseRim * 0.95) * outerSoft;
        col = u._Color.rgb * (crackRing * 1.25 + shatterSpokes * 1.05 + eclipseRim * 0.90)
            + u._CoreColor.rgb * (crackRing * 0.55 + shatterSpokes * 0.45);
    }

    col *= u._Intensity * u._Alpha * vColor.rgb;
    float alpha = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
