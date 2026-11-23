#version 450

// Fairy Festival Bioluminescent Care Aura & Expanding Mint-Emerald Healing Pulse Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.32, 0.98, 0.72, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(0.86, 1.0, 0.94, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Mode;      // @default = 0.0 (0 = continuous soft aura, 1 = expanding heal wave)
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
    vec2 p = (vUv - 0.5) * 2.0;
    float r = length(p);
    if (r > 1.0)
        discard;

    float ang = atan(p.y, p.x);
    float prog = clamp(u._Progress, 0.0, 1.0);
    float outerSoft = smoothstep(1.0, 0.80, r);

    // Delicate shimmering fairy-dust pollen sparkles rotating gently
    vec2 rotP = mat2(cos(u._AgeSecs * 0.45), -sin(u._AgeSecs * 0.45),
                     sin(u._AgeSecs * 0.45),  cos(u._AgeSecs * 0.45)) * p;
    float nDust = valueNoise(rotP * 7.5 + vec2(0.0, -u._AgeSecs * 1.4));
    float sparkle = pow(clamp(valueNoise(rotP * 16.0 + vec2(u._AgeSecs * 2.2, -u._AgeSecs * 1.8)), 0.0, 1.0), 5.0);

    // Gossamer wing-vein petal harmonic around perimeter
    float petalWave = 0.5 + 0.5 * sin(ang * 6.0 - u._AgeSecs * 2.6 + nDust * 2.2);

    float energy = 0.0;
    float coreEnergy = 0.0;

    if (u._Mode < 0.5)
    {
        // Mode 0: Continuous ethereal Fairy's Care aura around blessed pawn
        float breath = 0.82 + 0.18 * sin(u._AgeSecs * 3.2);
        float halo = exp(-pow((r - 0.62) / 0.18, 2.0)) * (0.48 + 0.35 * petalWave);
        float mist = exp(-r * r * 3.2) * (0.34 + 0.30 * nDust);
        float dust = sparkle * smoothstep(0.95, 0.15, r) * 1.45;
        energy = (halo + mist + dust) * breath * outerSoft;
        coreEnergy = (dust * 0.95 + exp(-pow((r - 0.62) / 0.07, 2.0)) * 0.45) * breath * outerSoft;
    }
    else
    {
        // Mode 1: Expanding luminous healing / blessing pulse wave
        float waveR = mix(0.08, 0.92, prog);
        float turb = (nDust - 0.5) * 0.05;
        float dr = r - (waveR + turb);
        float mainRing = exp(-dr * dr * 88.0) * (0.85 + 0.30 * petalWave);
        float innerTrail = exp(-pow(max(0.0, waveR - r) * 4.5, 2.0)) * step(r, waveR) * (0.42 + 0.28 * nDust);
        float coreFlash = exp(-r * r * 12.0) * pow(1.0 - prog, 1.5) * 0.95;
        float dustBurst = sparkle * smoothstep(waveR + 0.12, max(0.0, waveR - 0.35), r) * 1.65;
        energy = (mainRing * 1.25 + innerTrail * 0.72 + coreFlash + dustBurst) * outerSoft;
        coreEnergy = (mainRing * 0.85 + coreFlash * 1.15 + dustBurst) * outerSoft;
    }

    vec3 col = u._Color.rgb * energy + u._CoreColor.rgb * coreEnergy;
    col *= u._Alpha * vColor.rgb;
    float alpha = clamp((energy * 0.88 + coreEnergy * 0.55) * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
