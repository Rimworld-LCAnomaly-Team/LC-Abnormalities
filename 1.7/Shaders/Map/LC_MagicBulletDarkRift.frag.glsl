#version 450

// Dark obsidian Black-damage erosion wake drawn beneath the glowing Magic Bullet lance for sharp contrast.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec2 vScale;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.02, 0.03, 0.09, 0.88)
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

    if (distBehindHead < -0.3)
        discard;

    float headMask = smoothstep(-0.25, 0.35, distBehindHead);
    float originFade = smoothstep(0.0, 0.25, alongCells);
    float endFade = smoothstep(0.0, 0.5, beamLen - alongCells);

    float crossDist = abs((vUv.x - 0.5) * 2.0);
    float n = valueNoise(vec2(crossDist * 3.5, alongCells * 0.7 - u._AgeSecs * 12.0));
    float tighten = mix(2.2, 1.0, clamp(u._Alpha, 0.0, 1.0));
    float body = exp(-crossDist * crossDist * 5.2 * tighten) * (0.72 + 0.28 * n);

    float alpha = clamp(body * headMask * originFade * endFade * u._Alpha * u._Color.a * vColor.a, 0.0, 0.92);
    if (alpha < 0.01)
        discard;

    outColor = vec4(u._Color.rgb * vColor.rgb, alpha);
}
