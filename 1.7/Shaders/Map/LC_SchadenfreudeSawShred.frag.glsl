#version 450

// O-05-76 Schadenfreude Twin Chainsaw Shred Vortex & Mechanical Drill-Leg Piercing Lance Shader
// _Mode < 0.5 : Counter-rotating twin chainsaw crimson-iron shredding whirlwind with arterial blood spray & steel sparks
// _Mode >= 0.5: Directional corkscrew drill-leg piercing thrust cone with spiral ridges & crimson impact burst
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.72, 0.82, 0.86, 1.0)
    vec4 _BloodColor; // @color @default = vec4(0.92, 0.06, 0.09, 1.0)
    vec4 _SparkColor; // @color @default = vec4(1.0, 0.82, 0.42, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _SpinSpeed; // @default = 24.0
    float _Mode;      // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

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
    if (u._Mode < 0.5)
    {
        // Twin Chainsaw 360-degree Shredding Vortex
        vec2 p = (vUv - 0.5) * 2.0;
        float r = length(p);
        if (r > 1.0 || r < 0.16)
            discard;

        float ang = atan(p.y, p.x);
        float spin = u._AgeSecs * u._SpinSpeed;

        // 1. Outer serrated chain-blade track (16 sharp teeth whirling CW)
        float outerTeeth = 0.5 + 0.5 * sin(ang * 16.0 - spin + r * 4.5);
        float outerChain = exp(-pow((r - (0.80 + 0.065 * outerTeeth)) / 0.060, 2.0));

        // 2. Inner counter-rotating second chainsaw arc (12 teeth whirling CCW)
        float innerTeeth = 0.5 + 0.5 * sin(ang * 12.0 + spin * 1.15 - r * 3.5);
        float innerChain = exp(-pow((r - (0.54 + 0.045 * innerTeeth)) / 0.052, 2.0)) * 0.85;

        // 3. Swirling arterial crimson blood spray flung outward from both saws
        float n1 = valueNoise(vec2(ang * 6.0 - spin * 0.35, r * 10.0));
        float bloodRibbons = pow(0.5 + 0.5 * sin(ang * 5.0 - spin * 0.90 + r * 9.0), 2.5)
                           * smoothstep(0.24, 0.50, r) * smoothstep(0.99, 0.66, r)
                           * (0.60 + 0.60 * n1);

        // 4. White-orange metallic friction sparks along outer teeth
        float sparks = pow(max(0.0, sin(ang * 7.0 - spin * 1.75 + r * 5.0)), 10.0)
                     * exp(-pow((r - 0.84) / 0.085, 2.0)) * 1.55;

        // 5. Cold dark-iron motion blur sweep between the two chains
        float steelBlur = smoothstep(0.32, 0.50, r) * smoothstep(0.92, 0.72, r) * (0.28 + 0.32 * outerTeeth);

        float outerSoft = smoothstep(1.0, 0.88, r);
        vec3 col = u._Color.rgb * (outerChain * 1.10 + innerChain * 0.85 + steelBlur * 0.45)
                 + u._BloodColor.rgb * (bloodRibbons * 1.55 + outerChain * 0.55 + innerChain * 0.45)
                 + u._SparkColor.rgb * (sparks * 1.45);

        float energy = (outerChain * 1.15 + innerChain * 0.85 + bloodRibbons * 1.10 + sparks * 1.15 + steelBlur * 0.40) * outerSoft;
        col *= u._Alpha * vColor.rgb;
        float outA = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;

        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
    else
    {
        // Mechanical Drill-Leg Piercing Thrust Lance (vUv.x = width, vUv.y = base 0 -> tip 1)
        float prog = clamp(u._Progress, 0.0, 1.0);
        float tipPos = clamp(prog * 2.1, 0.0, 1.0);
        float y = vUv.y;
        if (y > tipPos + 0.08)
            discard;

        // Sharp tapering drill spear envelope (widest near base-mid, needle point at tip)
        float coneHalfW = mix(0.34, 0.025, pow(clamp(y / max(0.05, tipPos), 0.0, 1.0), 0.75));
        float dx = abs(vUv.x - 0.5);
        if (dx > coneHalfW * 2.1)
            discard;

        // Corkscrew spiral drill ridges racing along the thrust shaft
        float spiral = 0.5 + 0.5 * sin(y * 34.0 - u._AgeSecs * 46.0 + (vUv.x - 0.5) * 18.0);
        float shaftCore = exp(-pow(dx / max(0.02, coneHalfW * 0.55), 2.0));
        float spiralEdge = exp(-pow((dx - coneHalfW * 0.85) / max(0.018, coneHalfW * 0.32), 2.0)) * (0.45 + 0.85 * spiral);

        // Arterial blood spray erupting near the piercing tip
        float n = valueNoise(vec2(vUv.x * 12.0, y * 16.0 - u._AgeSecs * 22.0));
        float tipBurst = exp(-pow((y - tipPos * 0.88) / 0.16, 2.0)) * exp(-pow(dx / 0.28, 2.0)) * (0.55 + 0.65 * n);

        float lengthMask = smoothstep(0.0, 0.12, y) * smoothstep(tipPos + 0.05, tipPos - 0.06, y);
        float fade = pow(1.0 - prog, 0.75) * u._Alpha;

        vec3 col = u._Color.rgb * (shaftCore * 1.25 + spiralEdge * 1.15)
                 + u._BloodColor.rgb * (tipBurst * 1.65 + shaftCore * 0.35 + spiralEdge * 0.40)
                 + u._SparkColor.rgb * (shaftCore * spiral * 0.85 + tipBurst * 0.55);

        float energy = (shaftCore * 1.20 + spiralEdge * 0.95 + tipBurst * 1.25) * lengthMask * fade;
        col *= lengthMask * fade * vColor.rgb;
        float outA = clamp(energy * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;

        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
}
