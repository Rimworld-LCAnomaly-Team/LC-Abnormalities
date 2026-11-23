#version 450

// O-02-62 Judgement Bird Pale Soul Judgement Domain & Verdict Shockwave Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;       // @color @default = vec4(0.60, 0.94, 1.0, 1.0)
    vec4 _AccentColor; // @color @default = vec4(1.0, 0.90, 0.56, 1.0)
    float _Progress;   // @default = 0.5
    float _AgeSecs;    // @default = 0.0
    float _Alpha;      // @default = 1.0
    float _Mode;       // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

float hash21(vec2 p)
{
    p = fract(p * vec2(127.1, 311.7));
    p += dot(p, p + 34.5);
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
    vec2 centered = (vUv - 0.5) * 2.0; // -1..+1
    float r = length(centered);
    if (r > 1.02)
        discard;

    float ang = atan(centered.y, centered.x);
    float prog = clamp(u._Progress, 0.0, 1.0);
    vec3 col = vec3(0.0);
    float alpha = 0.0;

    if (u._Mode < 0.5)
    {
        // MODE 0: Charging Soul Judgement Domain Ring
        // 1. Crisp outer boundary ring marking exact 8.5-cell Pale judgement radius
        float outerRing = exp(-pow((r - 0.955) * 44.0, 2.0));
        float outerSoft = exp(-pow((r - 0.955) * 14.0, 2.0)) * 0.42;

        // 2. Rotating judicial dial tick marks around the perimeter
        float tickWave = smoothstep(0.72, 0.95, abs(cos(ang * 12.0 + u._AgeSecs * 0.45)));
        float tickBand = smoothstep(0.86, 0.90, r) * smoothstep(0.99, 0.95, r) * tickWave;

        // 3. Expanding inner progress ring converging toward the outer rim as judgement approaches climax
        float progRadius = mix(0.14, 0.955, prog);
        float progRing = exp(-pow((r - progRadius) * 32.0, 2.0)) * (0.65 + 0.55 * prog);
        float progFill = smoothstep(progRadius + 0.03, progRadius - 0.16, r) * 0.16 * (0.4 + 0.6 * prog);

        // 4. Eerie inward-drifting Pale soul wisps & swirling mist
        float n1 = valueNoise(vec2(ang * 3.5 + u._AgeSecs * 0.5, r * 6.0 + u._AgeSecs * 1.4));
        float n2 = valueNoise(vec2(ang * 7.0 - u._AgeSecs * 0.8, r * 11.0 + u._AgeSecs * 2.2));
        float mist = (n1 * 0.6 + n2 * 0.4) * smoothstep(1.0, 0.25, r) * smoothstep(0.05, 0.35, r);

        // 5. Central golden-pale radiance beneath Judgement Bird
        float coreGlow = exp(-r * r * 14.0) * (0.45 + 0.55 * prog);

        // Urgent climax pulse in the final 25% of windup
        float urgency = smoothstep(0.70, 1.0, prog);
        float pulse = 0.88 + 0.12 * sin(u._AgeSecs * (6.0 + 16.0 * urgency));

        float paleIntensity = (outerRing * 1.05 + outerSoft + progFill + mist * 0.42) * pulse;
        float goldIntensity = (tickBand * 0.85 + progRing * (0.7 + 0.6 * urgency) + coreGlow * 0.75) * pulse;

        col = u._Color.rgb * paleIntensity + u._AccentColor.rgb * goldIntensity;
        // White-hot glint on climax progress ring
        col += vec3(0.95, 0.99, 1.0) * (progRing * 0.55 * urgency + outerRing * 0.35 * urgency);

        alpha = clamp((paleIntensity * 0.75 + goldIntensity * 0.85) * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    }
    else
    {
        // MODE 1: Verdict Release Pale Shockwave Burst!
        float waveR = mix(0.06, 0.98, pow(prog, 0.62));
        float dist = abs(r - waveR);

        // Jagged soul-rending lightning ripples along the shockwave front
        float nA = valueNoise(vec2(ang * 6.0 + u._AgeSecs * 4.0, r * 9.0 - u._AgeSecs * 8.0));
        float nB = valueNoise(vec2(ang * 13.0 - u._AgeSecs * 5.0, r * 15.0));
        float jitter = (nA - 0.5) * 0.06 + (nB - 0.5) * 0.03;

        float razorFront = exp(-pow((r - (waveR + jitter)) * 28.0, 2.0));
        float wakeTrail = smoothstep(waveR + 0.04, waveR - 0.28, r) * smoothstep(waveR - 0.42, waveR - 0.06, r) * (0.45 + 0.65 * nA);
        float radialRays = pow(max(0.0, sin(ang * 16.0 + nB * 2.5)), 4.0) * smoothstep(1.0, 0.15, r) * (1.0 - prog) * 0.55;
        float centerFlash = exp(-r * r * 7.5) * pow(1.0 - prog, 1.7) * 1.45;

        float fade = pow(1.0 - prog, 1.15);
        col = u._Color.rgb * (razorFront * 1.35 + wakeTrail * 0.85 + radialRays)
            + u._AccentColor.rgb * (centerFlash * 0.65 + razorFront * 0.25)
            + vec3(0.95, 0.99, 1.0) * (razorFront * 1.1 + centerFlash * 0.85);
        col *= fade * u._Alpha * vColor.rgb;

        alpha = clamp((razorFront * 1.15 + wakeTrail * 0.7 + radialRays + centerFlash) * fade * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    }

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
