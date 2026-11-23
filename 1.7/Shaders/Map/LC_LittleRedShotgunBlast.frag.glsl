#version 450

// F-01-57 Little Red Riding Hooded Mercenary & Crimson Scar E.G.O. Weapon
// Volumetric Blunderbuss / Shotgun Muzzle Flash Cone & Crimson Buckshot Tracer Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 0.24, 0.14, 1.0)
    vec4 _CoreColor; // @color @default = vec4(1.0, 0.92, 0.62, 1.0)
    float _Progress; // @default = 0.25
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _Spread;   // @default = 0.55
    float _Berserk;  // @default = 0.0
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
    // vUv.y = 0 at muzzle, 1 at far cone end; vUv.x = 0.5 along shot axis
    float along = clamp(vUv.y, 0.0, 1.0);
    float lateral = (vUv.x - 0.5) * 2.0; // -1..+1
    float prog = clamp(u._Progress, 0.0, 1.0);
    float berserk = clamp(u._Berserk, 0.0, 1.0);

    // Expanding cone half-width from barrel mouth outward
    float halfWidth = mix(0.09, clamp(u._Spread, 0.25, 0.95), pow(along, 0.78));
    float normLat = abs(lateral) / max(halfWidth, 0.001);
    if (normLat > 1.35)
        discard;

    // Cone blasts forward swiftly during early progress
    float blastFront = clamp(prog * 3.4 + 0.22, 0.0, 1.25);
    float frontMask = smoothstep(blastFront + 0.08, blastFront - 0.14, along);
    float muzzleFade = smoothstep(0.0, 0.05, along);
    float tailFade = smoothstep(1.0, 0.52, along);

    // Fiery turbulent muzzle flame near the barrel
    float n1 = valueNoise(vec2(lateral * 5.0, along * 9.0 - u._AgeSecs * 28.0));
    float n2 = valueNoise(vec2(lateral * 11.0 + 2.3, along * 18.0 - u._AgeSecs * 44.0));
    float flameTurb = 0.6 + 0.4 * (n1 * 0.6 + n2 * 0.4);

    // 1. Blazing white-gold-orange muzzle flash petal right at barrel opening
    float muzzleDist = length(vec2(lateral * 2.6, along * 2.2));
    float muzzleCore = exp(-muzzleDist * muzzleDist * 7.5) * pow(1.0 - prog, 1.35) * 1.75;
    float muzzleSpikes = exp(-pow(normLat * 1.4, 2.0)) * exp(-along * 4.2) * flameTurb * pow(1.0 - prog, 0.95);

    // 2. Diverging crimson-gold buckshot pellet tracer rays (5 distinct pellet lines!)
    float rayAngle = lateral / max(along + 0.12, 0.12);
    float pellets = 0.0;
    for (int i = -2; i <= 2; i++)
    {
        float fi = float(i);
        float targetSlope = fi * (0.21 * u._Spread) + sin(fi * 4.7) * 0.03;
        float distRay = abs(rayAngle - targetSlope) * (along + 0.15);
        float rayLine = exp(-pow(distRay / 0.022, 2.0));
        // Each pellet has a bright hot tracer head racing down the cone
        float pelletHead = clamp(prog * (1.85 + 0.18 * abs(fi)) + 0.08, 0.0, 1.15);
        float headPulse = exp(-pow((along - pelletHead) / 0.11, 2.0));
        float wake = smoothstep(pelletHead + 0.04, pelletHead - 0.04, along)
                   * smoothstep(pelletHead - 0.48, pelletHead, along);
        pellets += rayLine * (wake * 0.72 + headPulse * 1.45);
    }

    // 3. Expanding crimson blast shock-arc near muzzle
    float shockDist = abs(along - (0.06 + prog * 0.45));
    float shockArc = exp(-pow(shockDist / 0.045, 2.0))
                   * smoothstep(1.1, 0.25, normLat)
                   * pow(1.0 - prog, 1.15) * 0.85;

    float coneFade = frontMask * muzzleFade * tailFade * pow(1.0 - prog, 0.80) * u._Alpha;
    vec3 mainCrimson = mix(u._Color.rgb, vec3(1.0, 0.08, 0.14), berserk * 0.55);

    vec3 col = mainCrimson * (muzzleSpikes * 1.25 + pellets * 0.95 + shockArc * 1.15)
             + u._CoreColor.rgb * (muzzleCore * 1.45 + pellets * 0.72 + muzzleSpikes * 0.45);
    col *= coneFade * (1.15 + 0.35 * berserk) * vColor.rgb;

    float outA = clamp((muzzleCore + muzzleSpikes * 0.9 + pellets * 0.95 + shockArc * 0.7) * coneFade * u._Color.a * vColor.a, 0.0, 1.0);
    if (outA < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), outA);
}
