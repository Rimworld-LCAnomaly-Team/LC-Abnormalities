#version 450

// O-05-76 Schadenfreude Surveillance Aperture & Keyhole Gaze Aura Shader
// _Mode < 0.5 : Rotating camera-lens shutter reticle, focus brackets & escalating steel->crimson danger gauge
//               (used for Contained Watched-Work warning & Twin-Chainsaw windup telegraph)
// _Mode >= 0.5: Expanding crimson keyhole-eye shockwave flash (used for Transform awakening, Gaze Rending burst & death)
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;       // @color @default = vec4(0.62, 0.72, 0.78, 1.0)
    vec4 _DangerColor; // @color @default = vec4(0.95, 0.12, 0.14, 1.0)
    vec4 _CoreColor;   // @color @default = vec4(1.0, 0.90, 0.88, 1.0)
    float _Progress;   // @default = 0.5
    float _AgeSecs;    // @default = 0.0
    float _Alpha;      // @default = 1.0
    float _Mode;       // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

float keyholeSdf(vec2 p)
{
    // Upper circular eye chamber + lower trapezoidal lock slot
    float circle = length(p - vec2(0.0, 0.14)) - 0.20;
    vec2 q = p - vec2(0.0, -0.14);
    float halfW = mix(0.11, 0.18, clamp((-q.y + 0.16) / 0.32, 0.0, 1.0));
    vec2 dBox = vec2(abs(q.x) - halfW, abs(q.y) - 0.18);
    float trap = length(max(dBox, 0.0)) + min(max(dBox.x, dBox.y), 0.0);
    return min(circle, trap);
}

void main()
{
    vec2 p = (vUv - 0.5) * 2.0;
    float r = length(p);
    if (r > 1.0)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);
    float ang = atan(p.y, p.x);

    if (u._Mode < 0.5)
    {
        // Surveillance Camera Aperture + Radial Danger Gauge
        vec3 baseTone = mix(u._Color.rgb, u._DangerColor.rgb, smoothstep(0.15, 0.88, prog));

        // 1. Radial progress fill ring (clockwise from top)
        float normAng = fract((-ang + 1.5707963) / 6.2831853);
        float fillMask = smoothstep(prog + 0.02, prog - 0.02, normAng);
        float gaugeBand = exp(-pow((r - 0.78) / 0.048, 2.0)) * (0.22 + 0.85 * fillMask);

        // 2. Rotating 6-blade antique camera aperture iris lines
        float irisSpin = u._AgeSecs * mix(0.9, 3.8, prog);
        float bladeWave = pow(max(0.0, sin(ang * 6.0 + irisSpin + r * 3.2)), 6.0);
        float irisRing = exp(-pow((r - mix(0.64, 0.50, prog)) / 0.08, 2.0)) * bladeWave * 0.85;

        // 3. Crisp viewfinder tick marks around outer circumference
        float ticks = pow(0.5 + 0.5 * cos(ang * 24.0), 12.0) * exp(-pow((r - 0.88) / 0.032, 2.0));
        float outerRim = exp(-pow((r - 0.92) / 0.020, 2.0)) * 0.75;

        // 4. Corner camera focus brackets (45, 135, 225, 315 deg)
        float diag = abs(cos(ang * 2.0));
        float brackets = smoothstep(0.55, 0.12, diag) * exp(-pow((r - 0.68) / 0.024, 2.0)) * 0.95;

        // 5. Heartbeat pulse intensifying sharply near full gauge
        float pulse = 0.78 + 0.22 * sin(u._AgeSecs * mix(5.0, 22.0, prog));
        float dangerFill = smoothstep(0.90, 0.25, r) * (0.10 + 0.24 * prog * pulse);

        // 6. Subtle central keyhole outline silhouette warning
        float kh = abs(keyholeSdf(p * 1.45));
        float khEdge = exp(-pow(kh / 0.032, 2.0)) * (0.35 + 0.65 * prog);

        vec3 col = baseTone * (gaugeBand * 1.35 + irisRing * 0.95 + ticks * 0.70 + outerRim * 0.65 + dangerFill)
                 + u._DangerColor.rgb * (khEdge * 1.15 + brackets * 0.65 * prog)
                 + u._CoreColor.rgb * (brackets * 0.75 + gaugeBand * fillMask * prog * 0.45);

        float energy = (gaugeBand + irisRing * 0.75 + ticks * 0.55 + outerRim * 0.45 + brackets * 0.85 + dangerFill * 0.7 + khEdge * 0.85)
                     * smoothstep(1.0, 0.92, r);
        col *= u._Alpha * vColor.rgb;
        float outA = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;

        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
    else
    {
        // Expanding Crimson Keyhole Shockwave & Shutter Flash
        float waveR = mix(0.14, 0.96, 1.0 - pow(1.0 - prog, 2.2));
        float shockRing = exp(-pow((r - waveR) / (0.075 * (1.0 - prog * 0.45)), 2.0));
        float innerWave = exp(-pow((r - waveR * 0.70) / 0.055, 2.0)) * 0.72;

        // Rotating 8-ray shutter glare spikes
        float shutterRays = pow(max(0.0, cos(ang * 8.0 - u._AgeSecs * 4.5)), 8.0)
                          * smoothstep(0.95, 0.18, r) * (1.0 - prog) * 0.95;

        // Keyhole silhouette glow at center expanding slightly
        float khDist = keyholeSdf(p * mix(1.85, 1.15, prog));
        float khEdge = exp(-pow(abs(khDist) / 0.040, 2.0)) * (1.0 - prog * 0.75);
        float khGlow = smoothstep(0.06, -0.04, khDist) * 0.45 * (1.0 - prog);

        float fade = pow(1.0 - prog, 0.90) * u._Alpha;
        vec3 col = u._DangerColor.rgb * (shockRing * 1.40 + innerWave * 0.95 + khEdge * 1.45 + khGlow)
                 + u._CoreColor.rgb * (shockRing * 0.65 + khEdge * 0.70 + shutterRays * 1.25)
                 + u._Color.rgb * (innerWave * 0.50);

        float energy = (shockRing * 1.15 + innerWave * 0.75 + shutterRays * 0.85 + khEdge * 1.15 + khGlow)
                     * smoothstep(1.0, 0.88, r) * fade;
        col *= fade * vColor.rgb;
        float outA = clamp(energy * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;

        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
}
