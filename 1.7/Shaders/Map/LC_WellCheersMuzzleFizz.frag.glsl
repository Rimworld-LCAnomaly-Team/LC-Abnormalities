#version 450

// F-05-52 Opened Can of WellCheers - E.G.O. Weapon "Soda" Muzzle Fizz Blast Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.68, 0.34, 1.0, 1.0)
    vec4 _AccentCol; // @color @default = vec4(1.0, 0.24, 0.48, 1.0)
    vec4 _CoreColor; // @color @default = vec4(1.0, 0.94, 1.0, 1.0)
    float _Progress; // @default = 0.25
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

float hash21(vec2 p)
{
    p = fract(p * vec2(127.1, 311.7));
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
    // vUv.y = 0 at pistol muzzle, 1 at forward plume tip; vUv.x = 0.5 along shot axis
    float along = clamp(vUv.y, 0.0, 1.0);
    float lateral = (vUv.x - 0.5) * 2.0; // -1..+1
    float prog = clamp(u._Progress, 0.0, 1.0);

    float halfWidth = mix(0.11, 0.62, pow(along, 0.72));
    float normLat = abs(lateral) / max(halfWidth, 0.001);
    if (normLat > 1.35)
        discard;

    float front = clamp(prog * 3.1 + 0.28, 0.0, 1.2);
    float frontMask = smoothstep(front + 0.10, front - 0.16, along);
    float muzzleFade = smoothstep(0.0, 0.04, along);
    float tailFade = smoothstep(1.0, 0.42, along);

    // Carbonated effervescent mist turbulence
    float n1 = valueNoise(vec2(lateral * 6.5, along * 11.0 - u._AgeSecs * 26.0));
    float n2 = valueNoise(vec2(lateral * 14.0 + 3.1, along * 20.0 - u._AgeSecs * 38.0));
    float fizzTurb = 0.58 + 0.42 * (n1 * 0.62 + n2 * 0.38);

    // 1. Crisp white-lavender flash core at barrel mouth
    float muzzleDist = length(vec2(lateral * 2.8, along * 2.5));
    float coreFlash = exp(-muzzleDist * muzzleDist * 8.5) * pow(1.0 - prog, 1.35) * 1.65;

    // 2. Pressurized grape-purple & cherry-red mist plume
    float mistPlume = exp(-pow(normLat * 1.25, 2.0)) * exp(-along * 3.1) * fizzTurb * pow(1.0 - prog, 0.85);

    // 3. Three fine fizzy carbonated spray streaks
    float rayAngle = lateral / max(along + 0.14, 0.14);
    float streaks = 0.0;
    for (int i = -1; i <= 1; i++)
    {
        float fi = float(i);
        float slope = fi * 0.16;
        float dRay = abs(rayAngle - slope) * (along + 0.16);
        float line = exp(-pow(dRay / 0.025, 2.0));
        float pulsePos = clamp(prog * 1.9 + 0.08 + abs(fi) * 0.05, 0.0, 1.1);
        float head = exp(-pow((along - pulsePos) / 0.13, 2.0));
        streaks += line * (0.45 + head * 1.15) * smoothstep(pulsePos + 0.06, pulsePos - 0.12, along);
    }

    // 4. Quick expanding micro-foam pressure ring
    float ringPos = 0.05 + prog * 0.48;
    float foamRing = exp(-pow((along - ringPos) / 0.042, 2.0))
                   * smoothstep(1.05, 0.25, normLat)
                   * pow(1.0 - prog, 1.10) * 0.78;

    float mask = frontMask * muzzleFade * tailFade * pow(1.0 - prog, 0.75) * u._Alpha;
    float accentMix = smoothstep(0.0, 0.65, along) * 0.45 + n2 * 0.25;
    vec3 sodaTint = mix(u._Color.rgb, u._AccentCol.rgb, clamp(accentMix, 0.0, 0.75));

    vec3 col = sodaTint * (mistPlume * 1.15 + streaks * 0.90 + foamRing * 1.05)
             + u._CoreColor.rgb * (coreFlash * 1.35 + streaks * 0.55 + foamRing * 0.45);
    col *= mask * 1.15 * vColor.rgb;

    float outA = clamp((coreFlash + mistPlume * 0.85 + streaks * 0.85 + foamRing * 0.65) * mask * u._Color.a * vColor.a, 0.0, 1.0);
    if (outA < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), outA);
}
