#version 450

// T-05-41 All-Around Helper Charge Windup Gauge & Linear Dash Corridor Telegraph Shader
// _Mode < 0.5 : Circular radial yellow->crimson charging gauge + rotating hazard segments + converging energy arcs
// _Mode >= 0.5: Directional linear dash corridor lane with racing warning chevrons and razor edge rails
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(1.0, 0.84, 0.14, 1.0)
    vec4 _DangerColor;// @color @default = vec4(1.0, 0.16, 0.10, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(1.0, 0.97, 0.82, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Mode;      // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

const float PI = 3.14159265;
const float TAU = 6.28318530;

void main()
{
    float prog = clamp(u._Progress, 0.0, 1.0);
    // Color ramps from Lobotomy Corp yellow charge gauge -> orange -> crimson danger flash
    vec3 baseCol = mix(u._Color.rgb, u._DangerColor.rgb, smoothstep(0.35, 0.96, prog));
    float urgentPulse = 0.85 + 0.15 * sin(u._AgeSecs * mix(10.0, 34.0, prog));

    if (u._Mode < 0.5)
    {
        // Circular Charge Windup Gauge around All-Around Helper
        vec2 p = (vUv - 0.5) * 2.0;
        float r = length(p);
        if (r > 1.0 || r < 0.08)
            discard;

        float ang = atan(p.y, p.x);
        float normAng = fract((ang + PI * 0.5) / TAU); // 0..1 clockwise from top

        // 1. Progress fill gauge ring (r ~ 0.72)
        float gaugeBand = exp(-pow((r - 0.72) / 0.055, 2.0));
        float filled = smoothstep(prog + 0.015, prog - 0.015, normAng);
        float gaugeTrack = gaugeBand * 0.24;
        float gaugeFill = gaugeBand * filled * (1.25 + 0.45 * prog);
        // Bright hot leading tip of the charging gauge
        float leadDist = abs(normAng - prog);
        leadDist = min(leadDist, 1.0 - leadDist);
        float leadTip = gaugeBand * exp(-pow(leadDist / 0.032, 2.0)) * (0.7 + 0.8 * prog);

        // 2. Rotating segmented industrial hazard ticks around outer rim (r ~ 0.86)
        float rotSpeed = mix(2.2, 12.0, prog * prog);
        float segWave = smoothstep(0.15, 0.45, sin(ang * 12.0 - u._AgeSecs * rotSpeed));
        float outerRing = exp(-pow((r - 0.87) / 0.032, 2.0)) * segWave * (0.65 + 0.55 * prog);

        // 3. Converging turbine lock-on ring shrinking inward toward center
        float shrinkR = mix(0.92, 0.28, fract(prog * 2.4));
        float shrinkRing = exp(-pow((r - shrinkR) / 0.035, 2.0)) * (0.45 + 0.55 * prog);

        // 4. Spinning inner 4-blade warning reticle
        float bladeSpokes = pow(max(0.0, cos(ang * 4.0 + u._AgeSecs * rotSpeed * 1.4)), 10.0)
                          * smoothstep(0.68, 0.22, r) * smoothstep(0.14, 0.28, r) * (0.4 + 0.7 * prog);

        float outerSoft = smoothstep(1.0, 0.92, r);
        float energy = (gaugeTrack + gaugeFill + leadTip + outerRing + shrinkRing + bladeSpokes) * outerSoft;

        vec3 col = baseCol * (gaugeTrack * 0.7 + gaugeFill * 1.15 + outerRing * 0.95 + shrinkRing * 0.8 + bladeSpokes * 0.9)
                 + u._CoreColor.rgb * (leadTip * 1.55 + gaugeFill * 0.30 * prog + bladeSpokes * 0.35);
        col *= urgentPulse * u._Alpha * vColor.rgb;

        float outA = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;
        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
    else
    {
        // Linear Dash Corridor Danger Telegraph (vUv.x = width across corridor, vUv.y = length along dash direction)
        float edgeDist = abs(vUv.x - 0.5) * 2.0; // 0 = center, 1 = outer rail
        if (edgeDist > 1.0)
            discard;

        // Soft fade at very start and tip of corridor
        float lenFade = smoothstep(0.0, 0.08, vUv.y) * smoothstep(1.0, 0.88, vUv.y);

        // Crisp glowing side boundary rails
        float rails = exp(-pow((edgeDist - 0.86) / 0.085, 2.0)) * (0.75 + 0.55 * prog);

        // Racing forward chevrons (>>>) accelerating as charge approaches 100%
        float chevCoord = vUv.y * 7.5 - u._AgeSecs * mix(4.0, 13.5, prog) - edgeDist * 0.85;
        float chev = pow(max(0.0, sin(chevCoord * TAU)), 4.0) * smoothstep(0.92, 0.15, edgeDist);

        // Inner danger wash filling up with charge progress
        float fillWave = smoothstep(prog + 0.12, prog - 0.08, vUv.y);
        float wash = (1.0 - edgeDist * edgeDist) * (0.14 + 0.24 * fillWave);

        float energy = (rails + chev * (0.55 + 0.55 * prog) + wash) * lenFade;
        vec3 col = baseCol * (rails * 1.05 + chev * 1.1 + wash * 0.85)
                 + u._CoreColor.rgb * (rails * 0.35 + chev * 0.45 * prog);
        col *= urgentPulse * u._Alpha * vColor.rgb;

        float outA = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;
        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
}
