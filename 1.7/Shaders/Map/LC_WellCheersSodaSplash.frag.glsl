#version 450

// F-05-52 Opened Can of WellCheers - E.G.O. Weapon "Soda" Impact Splash (Mode 0) & Soothing Heal Mist (Mode 1)
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.68, 0.34, 1.0, 1.0)
    vec4 _AccentCol; // @color @default = vec4(1.0, 0.24, 0.48, 1.0)
    vec4 _CoreColor; // @color @default = vec4(1.0, 0.96, 1.0, 1.0)
    float _Progress; // @default = 0.4
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _Mode;     // @default = 0.0
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
    float isHeal = step(0.5, u._Mode);
    float outerSoft = smoothstep(1.0, 0.80, r);

    if (isHeal < 0.5)
    {
        // Mode 0: Energetic carbonated impact splash + 6 pressurized splash petals
        float waveR = mix(0.08, 0.90, prog);
        float crown = sin(ang * 6.0 + u._AgeSecs * 8.0) * 0.035
                    + (valueNoise(vec2(ang * 3.5, r * 6.0 - u._AgeSecs * 6.0)) - 0.5) * 0.06;
        float dr = r - (waveR + crown);

        float splashRing = exp(-dr * dr * 88.0);
        float innerRing = exp(-pow((r - max(0.04, waveR - 0.18)) * 13.0, 2.0)) * step(0.18, waveR) * 0.65;

        // 6 radiating pressurized soda splash petals
        float petalWave = pow(0.5 + 0.5 * sin(ang * 6.0 + 0.7), 3.0);
        float petalMask = smoothstep(waveR + 0.08, waveR - 0.12, r) * smoothstep(0.05, waveR * 0.65, r);
        float splashPetals = petalWave * petalMask * (1.0 - prog * 0.55) * 0.78;

        // Brief foam core flash on impact
        float coreFlash = exp(-r * r * 12.0) * pow(1.0 - prog, 1.6) * 1.25;

        float blend = 0.5 + 0.5 * sin(ang * 3.0 - prog * 4.5);
        vec3 hue = mix(u._Color.rgb, u._AccentCol.rgb, blend * 0.50);

        vec3 col = hue * (splashRing * 1.15 + innerRing * 0.85 + splashPetals * 1.05)
                 + u._CoreColor.rgb * (splashRing * 0.55 + coreFlash * 1.30);
        col *= u._Alpha * outerSoft * vColor.rgb * 1.25;

        float energy = (splashRing * 1.25 + innerRing * 0.75 + splashPetals + coreFlash) * outerSoft;
        float alpha = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
        if (alpha < 0.01)
            discard;

        outColor = vec4(clamp(col, 0.0, 1.0), alpha);
    }
    else
    {
        // Mode 1: Soothing grape-lavender & refreshing cyan aroma healing ripple
        float wave1 = mix(0.14, 0.88, prog);
        float wave2 = mix(0.06, 0.68, prog);
        float ring1 = exp(-pow((r - wave1) * 9.5, 2.0)) * 0.95;
        float ring2 = exp(-pow((r - wave2) * 8.0, 2.0)) * 0.75;

        float mistNoise = valueNoise(vec2(centered.x * 4.5 + u._AgeSecs * 1.8, centered.y * 4.5 - u._AgeSecs * 2.2));
        float softMist = exp(-r * r * 2.6) * (0.45 + 0.55 * mistNoise) * 0.62;
        float sparkles = pow(0.5 + 0.5 * sin(ang * 8.0 - u._AgeSecs * 5.0), 6.0)
                       * exp(-pow((r - 0.45 - 0.12 * prog) * 6.5, 2.0)) * 0.72;

        float hueMix = 0.5 + 0.5 * sin(ang * 2.0 + u._AgeSecs * 3.5);
        vec3 sootheHue = mix(u._Color.rgb, u._AccentCol.rgb, 0.35 + 0.40 * hueMix);

        vec3 col = sootheHue * (ring1 + ring2 * 0.90 + softMist)
                 + u._CoreColor.rgb * (sparkles * 0.95 + ring1 * 0.35);
        col *= u._Alpha * outerSoft * vColor.rgb * 1.15;

        float energy = (ring1 + ring2 * 0.80 + softMist + sparkles) * outerSoft;
        float alpha = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
        if (alpha < 0.01)
            discard;

        outColor = vec4(clamp(col, 0.0, 1.0), alpha);
    }
}
