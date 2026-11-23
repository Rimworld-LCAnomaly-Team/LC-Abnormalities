#version 450

// Justitia E.G.O. Weapon & Judgement Bird Pale Soul Crescent Slash Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.56, 0.94, 1.0, 1.0)
    vec4 _CoreColor; // @color @default = vec4(0.96, 0.99, 1.0, 1.0)
    vec4 _EdgeColor; // @color @default = vec4(0.74, 0.60, 1.0, 1.0)
    float _Progress; // @default = 0.5
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _ArcSpan;  // @default = 2.4
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
    vec2 p = (vUv - 0.5) * 2.0; // -1..+1
    float r = length(p);
    if (r > 1.0 || r < 0.15)
        discard;

    // Angle in [-PI, PI], centered around +X (0 rad)
    float ang = atan(p.y, p.x);
    float halfSpan = max(0.8, u._ArcSpan * 0.5);
    float normAng = (ang / halfSpan) * 0.5 + 0.5; // 0 = start of swing, 1 = end of swing
    if (normAng < -0.05 || normAng > 1.05)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);
    // Sweeping blade head races across 0..1.15 while the luminous tail dissolves smoothly behind it
    float headPos = clamp(prog * 1.55, 0.0, 1.20);
    float tailPos = clamp((prog - 0.22) * 1.35, 0.0, 1.10);

    float headMask = smoothstep(headPos + 0.06, headPos - 0.05, normAng);
    float tailMask = smoothstep(tailPos - 0.08, tailPos + 0.24, normAng);
    float sweepMask = headMask * tailMask;
    if (sweepMask < 0.01)
        discard;

    // Crescent tapers cleanly to razor points at both ends
    float endTaper = sin(clamp(normAng, 0.0, 1.0) * 3.14159265);
    float bladeWidth = mix(0.03, 0.25, pow(endTaper, 0.65));

    // Outer razor edge sits around r = 0.78, inner pale ribbon trails inward
    float targetR = 0.78 - 0.04 * (1.0 - endTaper);
    float distFromEdge = targetR - r; // positive = inward behind blade edge

    // Animated soul-cleave wispy streaks along the slash arc
    float n1 = valueNoise(vec2(normAng * 9.0 - u._AgeSecs * 12.0, r * 12.0));
    float n2 = valueNoise(vec2(normAng * 18.0 - u._AgeSecs * 18.0, r * 20.0 + 2.7));
    float streaks = 0.55 + 0.45 * (n1 * 0.65 + n2 * 0.35);

    // 1. Blazing pure-white leading razor blade arc
    float razorCore = exp(-pow((r - targetR) / (0.042 * (0.4 + 0.6 * endTaper)), 2.0));
    // 2. Trailing icy-cyan Pale soul veil behind the blade edge
    float innerVeil = smoothstep(-0.04, 0.02, distFromEdge)
                    * smoothstep(bladeWidth * (1.15 + 0.35 * n1), 0.0, distFromEdge)
                    * streaks;
    // 3. Outer ethereal lavender-pale spectral rim (matching Lobotomy Corp attackslash.png)
    float outerRim = exp(-pow((r - (targetR + 0.045)) / 0.065, 2.0)) * (0.55 + 0.45 * n2) * endTaper;

    // Bright tip glint right at the swinging blade head
    float tipFlash = exp(-pow((normAng - min(headPos, 0.96)) * 10.0, 2.0)) * razorCore * (1.0 - prog);

    float fade = pow(1.0 - prog, 0.75) * u._Alpha;
    vec3 col = u._Color.rgb * (innerVeil * 1.15 + razorCore * 0.55)
             + u._CoreColor.rgb * (razorCore * 1.45 + tipFlash * 1.65)
             + u._EdgeColor.rgb * (outerRim * 0.95 + innerVeil * 0.22);
    col *= sweepMask * fade * vColor.rgb;

    float outA = clamp((razorCore * 1.25 + innerVeil * 0.85 + outerRim * 0.65 + tipFlash) * sweepMask * fade * u._Color.a * vColor.a, 0.0, 1.0);
    outColor = vec4(clamp(col, 0.0, 1.0), outA);
}
