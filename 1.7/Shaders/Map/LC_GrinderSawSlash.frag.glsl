#version 450

// Grinder Mk4 E.G.O. Weapon & All-Around Helper Jagged Sawtooth Laceration + Friction Spark Slash Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.95, 0.12, 0.10, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(1.0, 0.94, 0.80, 1.0)
    vec4 _SparkColor; // @color @default = vec4(1.0, 0.72, 0.18, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _ArcSpan;   // @default = 2.6
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
    float halfSpan = max(0.85, u._ArcSpan * 0.5);
    float normAng = (ang / halfSpan) * 0.5 + 0.5;
    if (normAng < -0.05 || normAng > 1.05)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);
    float headPos = clamp(prog * 1.65, 0.0, 1.18);
    float tailPos = clamp((prog - 0.18) * 1.38, 0.0, 1.12);

    float headMask = smoothstep(headPos + 0.05, headPos - 0.04, normAng);
    float tailMask = smoothstep(tailPos - 0.06, tailPos + 0.22, normAng);
    float sweepMask = headMask * tailMask;
    if (sweepMask < 0.01)
        discard;

    float endTaper = sin(clamp(normAng, 0.0, 1.0) * 3.14159265);
    // Sawtooth jagged outer edge modulation (signature Grinder Mk4 serrated bite!)
    float sawTeeth = 0.048 * sin(normAng * 38.0 - u._AgeSecs * 45.0) * endTaper;
    float targetR = 0.76 + sawTeeth - 0.04 * (1.0 - endTaper);
    float distInward = targetR - r;

    float n1 = valueNoise(vec2(normAng * 14.0 - u._AgeSecs * 22.0, r * 14.0));
    float n2 = valueNoise(vec2(normAng * 26.0 - u._AgeSecs * 32.0, r * 24.0 + 1.9));
    float shredNoise = 0.50 + 0.50 * (n1 * 0.6 + n2 * 0.4);

    // 1. White-hot serrated steel leading edge
    float razorCore = exp(-pow((r - targetR) / (0.040 * (0.45 + 0.55 * endTaper)), 2.0));
    // 2. Crimson blood-shred ribbon trailing inward behind the saw teeth
    float bladeWidth = mix(0.05, 0.28, pow(endTaper, 0.65));
    float crimsonTrail = smoothstep(-0.04, 0.02, distInward)
                       * smoothstep(bladeWidth * (1.1 + 0.35 * n1), 0.0, distInward)
                       * shredNoise;
    // 3. Flying orange-gold friction sparks erupting off the outer serrated edge
    float sparkHalo = exp(-pow((r - (targetR + 0.055)) / 0.065, 2.0)) * pow(n2, 2.0) * endTaper * 1.45;
    float tipFlash = exp(-pow((normAng - min(headPos, 0.96)) * 11.0, 2.0)) * razorCore * (1.0 - prog);

    float fade = pow(1.0 - prog, 0.80) * u._Alpha;
    vec3 col = u._Color.rgb * (crimsonTrail * 1.35 + razorCore * 0.45)
             + u._CoreColor.rgb * (razorCore * 1.35 + tipFlash * 1.65)
             + u._SparkColor.rgb * (sparkHalo * 1.55 + tipFlash * 0.55);
    col *= sweepMask * fade * vColor.rgb;

    float outA = clamp((razorCore * 1.25 + crimsonTrail * 0.95 + sparkHalo * 0.85 + tipFlash) * sweepMask * fade * u._Color.a * vColor.a, 0.0, 1.0);
    if (outA < 0.01)
        discard;
    outColor = vec4(clamp(col, 0.0, 1.0), outA);
}
