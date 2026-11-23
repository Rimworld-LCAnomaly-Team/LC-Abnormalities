#version 450

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec2 vScale;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.20, 0.62, 1.0, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(0.88, 0.98, 1.0, 1.0)
    float _Progress;  // @default = 1.0
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Intensity; // @default = 1.6
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

vec2 flipV(vec2 c) { return vec2(c.x, 1.0 - c.y); }

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
    float beamLen = max(vScale.y, 1.0);
    float alongCells = vUv.y * beamLen;
    float headCells = u._Progress * beamLen;
    float distBehindHead = headCells - alongCells;

    // Mask out everything ahead of the supersonic projectile tip (with tiny forward glow bow)
    if (distBehindHead < -1.2)
        discard;

    float headLeadMask = smoothstep(-0.9, 0.15, distBehindHead);
    // Clean fade at origin portal (first 0.35 cells) and gentle tail fade at map edge
    float originFade = smoothstep(0.0, 0.35, alongCells);
    float endFade = smoothstep(0.0, 0.6, beamLen - alongCells);

    // Beam thins dramatically as afterglow decays
    float lifeTighten = mix(2.6, 1.0, clamp(u._Alpha, 0.0, 1.0));
    float crossSigned = (vUv.x - 0.5) * 2.0; // -1..+1 across width
    float crossDist = abs(crossSigned);

    // High-speed directional flowing arcane noise along world cells
    float scroll1 = alongCells * 0.55 - u._AgeSecs * 26.0;
    float scroll2 = alongCells * 1.15 - u._AgeSecs * 42.0;
    float n1 = valueNoise(vec2(crossSigned * 2.8, scroll1));
    float n2 = valueNoise(vec2(crossSigned * 5.2 + 3.1, scroll2));
    float plasma = n1 * 0.65 + n2 * 0.35;

    // Slight electric lateral jitter of the inner filament
    float arcWobble = (n2 - 0.5) * 0.14 * mix(1.0, 0.3, clamp(u._Alpha, 0.0, 1.0));
    float coreDist = abs(crossSigned + arcWobble);

    // 1. Searing razor core (white-cyan)
    float core = exp(-coreDist * coreDist * 26.0 * lifeTighten);
    // 2. Electric cobalt inner sheath
    float midGlow = exp(-crossDist * crossDist * 7.0 * lifeTighten) * (0.75 + 0.55 * plasma);
    // 3. Wide ethereal arcane outer bloom
    float outerHalo = exp(-crossDist * crossDist * 2.4) * (0.35 + 0.65 * n1);

    // 4. Crackling dual helix/filament streaks flanking the main beam
    float ribbonWaveA = sin(alongCells * 1.35 - u._AgeSecs * 30.0) * 0.24;
    float ribbonWaveB = -sin(alongCells * 1.85 - u._AgeSecs * 36.0 + 1.4) * 0.28;
    float ribbonA = exp(-pow((crossSigned - ribbonWaveA) * 14.0, 2.0)) * (0.45 + 0.55 * n2);
    float ribbonB = exp(-pow((crossSigned - ribbonWaveB) * 16.0, 2.0)) * (0.40 + 0.60 * n1);
    float ribbons = (ribbonA + ribbonB) * clamp(u._Alpha * 1.2, 0.0, 1.0);

    // 5. Blazing supersonic lance head + Mach shock chevron right at _Progress!
    float headZone = clamp(1.0 - abs(distBehindHead) / 2.8, 0.0, 1.0);
    vec2 orbUv = vec2(vUv.x, clamp(0.5 - distBehindHead / 5.2, 0.0, 1.0));
    vec4 orbTex = texture(_MainTex, flipV(orbUv));
    float headFlash = (orbTex.a * 1.4 * headZone + exp(-crossDist * crossDist * 14.0) * headZone * 1.8) * step(u._Progress, 1.08);

    // Mach shock chevron trailing behind the bullet head
    float machOffset = distBehindHead - crossDist * 1.35;
    float machRing = exp(-machOffset * machOffset * 3.8) * smoothstep(0.95, 0.12, crossDist) * step(0.0, distBehindHead) * step(u._Progress, 1.05);

    // 6. Origin portal muzzle flare
    float muzzleFlare = exp(-alongCells * 0.75) * exp(-crossDist * crossDist * 4.5) * 1.25;

    // Combine layers
    float flicker = 0.90 + 0.10 * sin(u._AgeSecs * 55.0 + alongCells * 0.8);
    float beamMask = headLeadMask * originFade * endFade * u._Alpha * flicker;

    vec3 col = u._Color.rgb * (midGlow * 1.15 + outerHalo * 0.65 + ribbons * 0.85 + machRing * 1.1 + muzzleFlare * 0.9);
    col += u._CoreColor.rgb * (core * 1.45 + headFlash * 1.35 + muzzleFlare * 0.65 + machRing * 0.45);
    col *= u._Intensity * beamMask * vColor.rgb;

    float outA = clamp((core + midGlow * 0.8 + outerHalo * 0.4 + headFlash + machRing) * beamMask * u._Color.a * vColor.a, 0.0, 1.0);
    outColor = vec4(clamp(col, 0.0, 1.0), outA);
}
