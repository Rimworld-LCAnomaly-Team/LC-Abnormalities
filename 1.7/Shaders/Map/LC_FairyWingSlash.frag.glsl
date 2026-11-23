#version 450

// Wingbeat E.G.O. Weapon & Fairy Festival Nibble Crescent Slash Shader
// Combines a searing Red-damage crimson fang edge with iridescent mint-emerald gossamer fairy wing veins & sparkles.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.96, 0.20, 0.28, 1.0)
    vec4 _WingColor;  // @color @default = vec4(0.32, 0.98, 0.74, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(0.98, 1.0, 0.95, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _ArcSpan;   // @default = 2.25
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
    if (r > 1.0 || r < 0.14)
        discard;

    float ang = atan(p.y, p.x);
    float halfSpan = max(0.75, u._ArcSpan * 0.5);
    float normAng = (ang / halfSpan) * 0.5 + 0.5;
    if (normAng < -0.06 || normAng > 1.06)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);
    float headPos = clamp(prog * 1.65, 0.0, 1.18);
    float tailPos = clamp((prog - 0.20) * 1.38, 0.0, 1.08);

    float headMask = smoothstep(headPos + 0.06, headPos - 0.05, normAng);
    float tailMask = smoothstep(tailPos - 0.08, tailPos + 0.26, normAng);
    float sweepMask = headMask * tailMask;
    if (sweepMask < 0.01)
        discard;

    float endTaper = sin(clamp(normAng, 0.0, 1.0) * 3.14159265);
    float trailWidth = mix(0.04, 0.30, pow(endTaper, 0.65));

    float targetR = 0.78 - 0.05 * (1.0 - endTaper);
    float distInward = targetR - r;

    // Iridescent insect-wing vein ribs + fairy-dust sparkles trailing behind the swing
    float n1 = valueNoise(vec2(normAng * 10.0 - u._AgeSecs * 11.0, r * 11.0));
    float n2 = valueNoise(vec2(normAng * 22.0 - u._AgeSecs * 16.0, r * 18.0 + 3.1));
    float wingVeins = pow(0.5 + 0.5 * sin(normAng * 28.0 + r * 9.0 + n1 * 2.4), 3.0);
    float sparkles = pow(clamp(n2, 0.0, 1.0), 4.5) * 1.5;

    // 1. Sharp luminous Red-damage leading blade crescent
    float fangCore = exp(-pow((r - targetR) / (0.044 * (0.45 + 0.55 * endTaper)), 2.0));
    // 2. Translucent mint-emerald fairy-wing trail fanning inward
    float wingTrail = smoothstep(-0.04, 0.025, distInward)
                    * smoothstep(trailWidth * (1.10 + 0.35 * n1), 0.0, distInward)
                    * (0.55 + 0.45 * wingVeins + sparkles * 0.45);
    // 3. Outer gossamer emerald shimmer rim
    float outerShimmer = exp(-pow((r - (targetR + 0.05)) / 0.065, 2.0)) * (0.55 + 0.45 * n2) * endTaper;
    // 4. Bright white-mint flash at sweeping mace tip
    float tipGlint = exp(-pow((normAng - min(headPos, 0.95)) * 11.0, 2.0)) * fangCore * (1.0 - prog);

    float fade = pow(1.0 - prog, 0.72) * u._Alpha;
    vec3 col = u._Color.rgb * (fangCore * 1.35 + wingTrail * 0.38)
             + u._WingColor.rgb * (wingTrail * 1.10 + outerShimmer * 1.05 + sparkles * wingTrail * 0.85)
             + u._CoreColor.rgb * (fangCore * 0.90 + tipGlint * 1.65);
    col *= sweepMask * fade * vColor.rgb;

    float outA = clamp((fangCore * 1.25 + wingTrail * 0.90 + outerShimmer * 0.65 + tipGlint) * sweepMask * fade * u._Color.a * vColor.a, 0.0, 1.0);
    if (outA < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), outA);
}
