#version 450

// O-06-20 Nothing There & Mimicry E.G.O. Weapon
// Visceral Flesh-and-Bone Crescent Cleave Shader (arterial crimson core, obsidian edge, serrated bone highlights)
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.92, 0.07, 0.11, 1.0)
    vec4 _CoreColor; // @color @default = vec4(0.04, 0.005, 0.01, 1.0)
    vec4 _BoneColor; // @color @default = vec4(1.0, 0.88, 0.74, 1.0)
    float _Progress; // @default = 0.35
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _ArcSpan;  // @default = 2.75
    float _Heavy;    // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

float hash21(vec2 p)
{
    p = fract(p * vec2(234.34, 435.345));
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
    if (r > 1.0 || r < 0.10)
        discard;

    float ang = atan(p.y, p.x);
    float halfSpan = max(0.85, u._ArcSpan * 0.5);
    float normAng = (ang / halfSpan) * 0.5 + 0.5;
    if (normAng < -0.06 || normAng > 1.06)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);
    float heavy = clamp(u._Heavy, 0.0, 1.0);
    float headPos = clamp(prog * 1.80, 0.0, 1.25);
    float tailPos = clamp((prog - 0.16) * 1.38, 0.0, 1.12);

    float headMask = smoothstep(headPos + 0.06, headPos - 0.04, normAng);
    float tailMask = smoothstep(tailPos - 0.07, tailPos + 0.24, normAng);
    float sweepMask = headMask * tailMask;
    if (sweepMask < 0.01)
        discard;

    float endTaper = sin(clamp(normAng, 0.0, 1.0) * 3.14159265);
    float bladeWidth = mix(0.04, 0.28 + 0.10 * heavy, pow(endTaper, 0.58));

    float n1 = valueNoise(vec2(normAng * 14.0 - u._AgeSecs * 18.0, r * 14.0));
    float n2 = valueNoise(vec2(normAng * 28.0 - u._AgeSecs * 26.0, r * 26.0 + 3.7));
    float sinew = 0.50 + 0.50 * (n1 * 0.62 + n2 * 0.38);

    // Jagged serrated teeth ripple along the outer arc
    float serration = 0.022 * sin(normAng * 42.0) * endTaper * (0.6 + 0.6 * heavy);
    float targetR = 0.80 - 0.05 * (1.0 - endTaper) + serration;
    float distInward = targetR - r;

    float outerCrimson = exp(-pow((r - (targetR + 0.022)) / (0.054 * (0.45 + 0.55 * endTaper)), 2.0)) * (0.78 + 0.36 * n1);
    float blackFleshSpine = exp(-pow((r - (targetR - 0.036)) / (0.044 * (0.38 + 0.62 * endTaper)), 2.0));
    float innerTrail = smoothstep(-0.04, 0.02, distInward)
                     * smoothstep(bladeWidth * (1.08 + 0.42 * n1), 0.0, distInward)
                     * sinew;
    float boneEdge = exp(-pow((r - (targetR + 0.008)) / 0.018, 2.0))
                   * smoothstep(0.45, 0.88, sin(normAng * 42.0) * 0.5 + 0.5)
                   * endTaper * (0.55 + 0.65 * heavy);
    float tipFlash = exp(-pow((normAng - min(headPos, 0.96)) * 10.0, 2.0))
                   * exp(-pow((r - targetR) / 0.085, 2.0))
                   * (1.0 - prog * 0.80);

    // Arterial blood droplets flung off the outer arc
    float dropNoise = valueNoise(vec2(normAng * 36.0 - prog * 9.0, r * 30.0));
    float droplets = smoothstep(0.74, 0.91, dropNoise)
                   * smoothstep(0.99, 0.85, r) * smoothstep(0.70, 0.83, r)
                   * endTaper * (0.65 + 0.70 * heavy);

    float fade = pow(1.0 - prog, 0.68) * u._Alpha;
    float totalW = clamp(outerCrimson * 1.15 + blackFleshSpine * 1.25 + innerTrail * 0.95 + boneEdge * 0.85 + tipFlash + droplets, 0.0, 1.0);
    float outA = clamp(totalW * sweepMask * fade * u._Color.a * vColor.a, 0.0, 1.0);
    if (outA < 0.01)
        discard;

    vec3 crimson = u._Color.rgb * (1.15 + 0.35 * heavy);
    vec3 col = crimson * (outerCrimson * 1.30 + innerTrail * 0.98 + droplets * 1.30);
    float darkMask = clamp(blackFleshSpine * 0.85, 0.0, 0.90);
    col = mix(col, u._CoreColor.rgb, darkMask);
    col += u._BoneColor.rgb * (boneEdge * 0.95 + tipFlash * (1.25 + 0.35 * heavy));
    col *= vColor.rgb;

    outColor = vec4(clamp(col, 0.0, 1.0), outA);
}
