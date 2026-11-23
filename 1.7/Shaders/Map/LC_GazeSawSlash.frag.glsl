#version 450

// E.G.O. Weapon "Gaze" (O-05-76) Jagged Crimson-Obsidian Buzzsaw Slash & Grinding Halo Shader
// _Mode < 0.5 : High-contrast dark-iron & arterial-crimson serrated crescent slash with spinning saw teeth & metallic sparks
// _Mode >= 0.5: Compact high-RPM rotary grinding vortex surrounding the spinning Gaze_SawDisc on the struck target
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.92, 0.08, 0.12, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(0.88, 0.95, 0.98, 1.0)
    vec4 _SparkColor; // @color @default = vec4(1.0, 0.62, 0.24, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _ArcSpan;   // @default = 2.65
    float _Mode;      // @default = 0.0
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
    float prog = clamp(u._Progress, 0.0, 1.0);

    if (u._Mode < 0.5)
    {
        float halfSpan = max(0.85, u._ArcSpan * 0.5);
        float normAng = (ang / halfSpan) * 0.5 + 0.5;
        if (normAng < -0.05 || normAng > 1.05)
            discard;

        float headPos = clamp(prog * 1.70, 0.0, 1.18);
        float tailPos = clamp((prog - 0.16) * 1.40, 0.0, 1.12);
        float headMask = smoothstep(headPos + 0.05, headPos - 0.04, normAng);
        float tailMask = smoothstep(tailPos - 0.06, tailPos + 0.22, normAng);
        float sweepMask = headMask * tailMask;
        if (sweepMask < 0.01)
            discard;

        float endTaper = sin(clamp(normAng, 0.0, 1.0) * 3.14159265);
        // Signature aggressive sawtooth jagged edge of Gaze buzzsaw
        float teeth = 0.056 * sin(normAng * 44.0 - u._AgeSecs * 52.0) * endTaper;
        float targetR = 0.75 + teeth - 0.04 * (1.0 - endTaper);
        float distInward = targetR - r;

        float n1 = valueNoise(vec2(normAng * 16.0 - u._AgeSecs * 24.0, r * 14.0));
        float n2 = valueNoise(vec2(normAng * 28.0 - u._AgeSecs * 36.0, r * 24.0 + 2.3));
        float shred = 0.48 + 0.52 * (n1 * 0.6 + n2 * 0.4);

        // Cold gunmetal-white serrated steel leading teeth
        float razorCore = exp(-pow((r - targetR) / (0.038 * (0.45 + 0.55 * endTaper)), 2.0));
        // Deep arterial crimson blood ribbon trailing behind the teeth
        float bandWidth = mix(0.05, 0.30, pow(endTaper, 0.62));
        float crimsonTrail = smoothstep(-0.04, 0.02, distInward)
                           * smoothstep(bandWidth * (1.08 + 0.35 * n1), 0.0, distInward)
                           * shred;
        // Hot sparks flung from saw bite
        float sparkHalo = exp(-pow((r - (targetR + 0.055)) / 0.060, 2.0)) * pow(n2, 2.2) * endTaper * 1.45;
        float tipFlash = exp(-pow((normAng - min(headPos, 0.96)) * 12.0, 2.0)) * razorCore * (1.0 - prog);

        float fade = pow(1.0 - prog, 0.78) * u._Alpha;
        vec3 col = u._Color.rgb * (crimsonTrail * 1.45 + razorCore * 0.50)
                 + u._CoreColor.rgb * (razorCore * 1.25 + tipFlash * 1.50)
                 + u._SparkColor.rgb * (sparkHalo * 1.50 + tipFlash * 0.50);
        col *= sweepMask * fade * vColor.rgb;

        float outA = clamp((razorCore * 1.20 + crimsonTrail * 1.00 + sparkHalo * 0.85 + tipFlash) * sweepMask * fade * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;
        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
    else
    {
        // Compact high-RPM Gaze rotary grinding halo behind Gaze_SawDisc.png
        float spin = u._AgeSecs * 32.0;
        float teeth = 0.5 + 0.5 * sin(ang * 18.0 - spin + r * 4.0);
        float ring = exp(-pow((r - (0.76 + 0.06 * teeth)) / 0.070, 2.0));
        float spiral = pow(0.5 + 0.5 * sin(ang * 4.0 - spin * 0.8 + r * 8.0), 2.5)
                     * smoothstep(0.28, 0.52, r) * smoothstep(0.98, 0.68, r);
        float sparks = pow(max(0.0, sin(ang * 6.0 - spin * 1.5)), 10.0)
                     * exp(-pow((r - 0.84) / 0.08, 2.0)) * 1.5;

        vec3 col = u._Color.rgb * (spiral * 1.50 + ring * 0.75)
                 + u._CoreColor.rgb * (ring * 1.05)
                 + u._SparkColor.rgb * (sparks * 1.45);
        float energy = (ring * 1.10 + spiral * 0.95 + sparks * 1.10) * smoothstep(1.0, 0.86, r);
        col *= u._Alpha * vColor.rgb;
        float outA = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;
        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
}
