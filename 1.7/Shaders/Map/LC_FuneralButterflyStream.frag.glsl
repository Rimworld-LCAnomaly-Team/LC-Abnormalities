#version 450

// T-01-68 Funeral of the Dead Butterflies linear open-coffin swarm wind ribbon:
// Interwoven pale-white mourning mist and dark obsidian wind filaments streaming straight out of the open coffin.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec2 vScale;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;        // @color @default = vec4(0.92, 0.94, 0.98, 0.85)
    vec4 _DarkColor;    // @color @default = vec4(0.05, 0.05, 0.08, 0.72)
    float _HeadProgress;// @default = 1.0
    float _TailProgress;// @default = 0.0
    float _AgeSecs;     // @default = 0.0
    float _Alpha;       // @default = 1.0
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
    float streamLen = max(vScale.y, 1.0);
    float alongFrac = vUv.y;
    float alongCells = alongFrac * streamLen;

    // Leading edge surges forward from coffin mouth; trailing edge sweeps out when coffin closes
    float headMask = smoothstep(u._HeadProgress + 0.05, u._HeadProgress - 0.14, alongFrac);
    float tailMask = smoothstep(u._TailProgress - 0.05, u._TailProgress + 0.14, alongFrac);
    float originFade = smoothstep(0.0, 0.45, alongCells);
    float tipFade = smoothstep(0.0, 1.15, streamLen - alongCells);

    float crossDist = abs((vUv.x - 0.5) * 2.0); // 0 at center, 1 at outer corridor edge
    if (crossDist > 1.0)
        discard;

    // Flowing mourning currents along the straight corridor
    float flow1 = valueNoise(vec2((vUv.x - 0.5) * 3.8, alongCells * 0.42 - u._AgeSecs * 9.5));
    float flow2 = valueNoise(vec2((vUv.x - 0.5) * 7.2 + 2.1, alongCells * 0.85 - u._AgeSecs * 15.0));

    // Soft dark-mourning corridor bed + brighter pale-white braided wind streaks
    float darkBed = exp(-crossDist * crossDist * 2.8) * (0.48 + 0.32 * flow1);
    float whiteRibbonA = exp(-pow(((vUv.x - 0.5) * 2.0 - sin(alongCells * 0.85 - u._AgeSecs * 11.0) * 0.28) / 0.24, 2.0));
    float whiteRibbonB = exp(-pow(((vUv.x - 0.5) * 2.0 + sin(alongCells * 1.15 - u._AgeSecs * 13.5 + 1.4) * 0.34) / 0.20, 2.0));
    float whiteCore = (exp(-crossDist * crossDist * 6.5) * 0.42 + (whiteRibbonA * 0.48 + whiteRibbonB * 0.36) * (0.55 + 0.45 * flow2));

    // Subtle butterfly wing-dust glints streaming along the corridor
    float dust = smoothstep(0.76, 0.96, valueNoise(vec2(vUv.x * 18.0, alongCells * 2.2 - u._AgeSecs * 21.0))) * exp(-crossDist * crossDist * 3.2);

    float envelope = headMask * tailMask * originFade * tipFade * u._Alpha;
    if (envelope <= 0.005)
        discard;

    float darkWeight = darkBed * u._DarkColor.a;
    float lightWeight = (whiteCore * 0.58 + dust * 0.55) * u._Color.a;
    float combinedAlpha = clamp((darkWeight * 0.46 + lightWeight * 0.62) * envelope * vColor.a, 0.0, 0.72);
    if (combinedAlpha < 0.01)
        discard;

    // Blend obsidian mourning shadow with pale silver-white spirit currents
    float whiteMix = clamp(lightWeight / max(lightWeight + darkWeight * 0.85, 0.001), 0.0, 1.0);
    vec3 col = mix(u._DarkColor.rgb, u._Color.rgb, whiteMix) * vColor.rgb;
    outColor = vec4(clamp(col, 0.0, 1.0), combinedAlpha);
}
