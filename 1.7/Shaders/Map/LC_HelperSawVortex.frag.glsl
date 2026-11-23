#version 450

// T-05-41 All-Around Helper High-RPM Rotary Buzzsaw Vortex & Dash Streak Trail Shader
// _Mode < 0.5 : Blinding spinning crimson-steel circular sawblade whirlwind with friction sparks & blood arcs
// _Mode >= 0.5: Streaking crimson-white high-speed buzzsaw dash afterimage corridor trail
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.92, 0.95, 1.0, 1.0)
    vec4 _BloodColor; // @color @default = vec4(0.92, 0.08, 0.08, 1.0)
    vec4 _SparkColor; // @color @default = vec4(1.0, 0.78, 0.28, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _SpinSpeed; // @default = 22.0
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
        vec2 p = (vUv - 0.5) * 2.0;
        float r = length(p);
        if (r > 1.0 || r < 0.14)
            discard;

        float ang = atan(p.y, p.x);
        float spin = u._AgeSecs * u._SpinSpeed;

        // 1. Primary razor-sharp circular motion-blurred steel saw disc (r ~ 0.62..0.88)
        float teeth = 0.5 + 0.5 * sin(ang * 10.0 - spin + r * 5.0);
        float toothRipple = 0.5 + 0.5 * sin(ang * 20.0 - spin * 1.35);
        float outerBladeEdge = exp(-pow((r - (0.76 + 0.07 * teeth)) / 0.075, 2.0));
        float steelDisc = smoothstep(0.24, 0.44, r) * smoothstep(0.95, 0.72, r) * (0.45 + 0.55 * toothRipple);

        // 2. Spiraling crimson arterial blood ribbons flung off the saw teeth
        float spiralCoord = ang * 4.0 - spin * 0.85 + r * 8.5;
        float bloodSpiral = pow(0.5 + 0.5 * sin(spiralCoord), 3.0)
                          * smoothstep(0.28, 0.52, r) * smoothstep(0.99, 0.64, r);
        float bloodNoise = valueNoise(vec2(ang * 5.0 - spin * 0.4, r * 9.0));
        float bloodMist = bloodSpiral * (0.65 + 0.55 * bloodNoise);

        // 3. Hot white-gold friction sparks racing along the outer perimeter
        float sparkArc = pow(max(0.0, sin(ang * 6.0 - spin * 1.65 + r * 4.0)), 9.0)
                       * exp(-pow((r - 0.82) / 0.09, 2.0));

        // 4. Inner gleaming steel hub ring
        float innerRim = exp(-pow((r - 0.36) / 0.045, 2.0)) * 0.75;

        float outerSoft = smoothstep(1.0, 0.86, r);
        vec3 col = u._Color.rgb * (outerBladeEdge * 1.15 + steelDisc * 0.48 + innerRim * 0.85)
                 + u._BloodColor.rgb * (bloodMist * 1.45 + outerBladeEdge * 0.40)
                 + u._SparkColor.rgb * (sparkArc * 1.65);

        float energy = (outerBladeEdge * 1.15 + steelDisc * 0.50 + bloodMist * 1.05 + sparkArc * 1.2 + innerRim * 0.65) * outerSoft;
        col *= u._Alpha * vColor.rgb;
        float outA = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;

        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
    else
    {
        // High-speed crimson-steel dash wake / ribbon trail (vUv.x = width, vUv.y = along dash tail -> head)
        float edgeDist = abs(vUv.x - 0.5) * 2.0;
        if (edgeDist > 1.0)
            discard;

        float prog = clamp(u._Progress, 0.0, 1.0);
        float tailFade = smoothstep(0.0, 0.85, vUv.y) * smoothstep(1.0, 0.90, vUv.y);
        float n = valueNoise(vec2(vUv.x * 8.0, vUv.y * 12.0 - u._AgeSecs * 26.0));
        float n2 = valueNoise(vec2(vUv.x * 16.0 + 3.1, vUv.y * 22.0 - u._AgeSecs * 34.0));

        // Razor steel speed lines + crimson blood-mist core
        float coreStreak = exp(-pow(edgeDist / 0.42, 2.0)) * (0.55 + 0.55 * n);
        float razorLines = exp(-pow((edgeDist - 0.62) / 0.09, 2.0)) * (0.5 + 0.5 * n2);
        float sparkFlecks = pow(n2, 4.0) * smoothstep(0.95, 0.2, edgeDist) * 1.4;

        float fade = pow(1.0 - prog, 1.15) * u._Alpha;
        vec3 col = u._BloodColor.rgb * (coreStreak * 1.35 + razorLines * 0.45)
                 + u._Color.rgb * (razorLines * 1.20 + coreStreak * 0.25)
                 + u._SparkColor.rgb * sparkFlecks;
        col *= tailFade * fade * vColor.rgb;

        float outA = clamp((coreStreak * 0.95 + razorLines * 0.90 + sparkFlecks * 0.7) * tailFade * fade * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;
        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
}
