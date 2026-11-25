#version 450

// Subtle dark-indigo Black-damage smoke trail directly behind the flying Magic Bullet.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec2 vScale;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.02, 0.04, 0.10, 0.55)
    float _Progress; // @default = 1.0
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
} u;

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
    float beamLen = max(vScale.y, 1.0);
    float alongCells = vUv.y * beamLen;
    float headCells = u._Progress * beamLen;
    float distBehindHead = headCells - alongCells;

    if (distBehindHead < -0.15)
        discard;

    float headMask = smoothstep(-0.12, 0.25, distBehindHead);
    float originFade = smoothstep(0.0, 0.25, alongCells);
    float endFade = smoothstep(0.0, 0.4, beamLen - alongCells);

    float tailLen = clamp(beamLen * 0.45, 7.0, 14.0);
    float tailFrac = clamp(1.0 - distBehindHead / tailLen, 0.0, 1.0);
    float tailEnv = pow(tailFrac, 1.35);

    float crossDist = abs((vUv.x - 0.5) * 2.0);
    float n = valueNoise(vec2(crossDist * 3.0, alongCells * 0.65 - u._AgeSecs * 8.0));
    float taper = mix(2.8, 1.0, tailFrac);
    float cometDark = exp(-crossDist * crossDist * 9.5 * taper) * tailEnv * (0.70 + 0.30 * n);
    float traceDark = exp(-crossDist * crossDist * 34.0) * 0.22 * u._Alpha;

    float alpha = clamp((cometDark + traceDark) * headMask * originFade * endFade * u._Alpha * u._Color.a * vColor.a, 0.0, 0.62);
    if (alpha < 0.01)
        discard;

    outColor = vec4(u._Color.rgb * vColor.rgb, alpha);
}
