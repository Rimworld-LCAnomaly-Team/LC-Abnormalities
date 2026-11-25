#version 450

// Restrained Lobotomy Corporation Magic Bullet piercing trail:
// A sleek, tapered luminous cobalt comet tail behind the fast-moving bullet + a faint razor-thin residual trace.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec2 vScale;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.18, 0.46, 0.90, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(0.76, 0.91, 1.0, 1.0)
    float _Progress;  // @default = 1.0
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Intensity; // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

vec2 flipV(vec2 c) { return vec2(c.x, 1.0 - c.y); }

float hash21(vec2 p)
{
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
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
    float beamLen = max(vScale.y, 1.0);
    float alongCells = vUv.y * beamLen;
    float headCells = u._Progress * beamLen;
    float distBehindHead = headCells - alongCells;

    // Mask out everything ahead of the bullet tip
    if (distBehindHead < -0.45)
        discard;

    float headLeadMask = smoothstep(-0.35, 0.12, distBehindHead);
    float originFade = smoothstep(0.0, 0.25, alongCells);
    float endFade = smoothstep(0.0, 0.4, beamLen - alongCells);

    float crossDist = abs((vUv.x - 0.5) * 2.0); // 0 at center, 1 at quad edge

    // Finite-length comet tail (~11 cells long) that smoothly tapers to a sharp point behind the bullet
    float tailLen = clamp(beamLen * 0.42, 6.0, 12.5);
    float tailFrac = clamp(1.0 - distBehindHead / tailLen, 0.0, 1.0);
    float tailEnvelope = pow(tailFrac, 1.55);

    // Subtle organic wisp variation along world cells (calm, cold smoke-energy feel)
    float wisp = valueNoise(vec2(crossDist * 2.4, alongCells * 0.65 - u._AgeSecs * 14.0));

    // Taper width smoothly from bullet body down to razor point along the comet tail
    float taperTighten = mix(3.2, 1.0, tailFrac);
    float cometCore = exp(-crossDist * crossDist * 42.0 * taperTighten) * tailEnvelope;
    float cometSheath = exp(-crossDist * crossDist * 11.5 * taperTighten) * tailEnvelope * (0.72 + 0.35 * wisp);
    float softAura = exp(-crossDist * crossDist * 4.5) * tailEnvelope * 0.28;

    // Subtle soft glow right behind the bullet head using FreischutzBulletTrailTex
    float headZone = clamp(1.0 - abs(distBehindHead) / 1.4, 0.0, 1.0) * step(u._Progress, 1.02);
    vec2 orbUv = vec2(vUv.x, clamp(0.5 - distBehindHead / 2.6, 0.0, 1.0));
    float headOrb = texture(_MainTex, flipV(orbUv)).a * headZone * 0.55;

    // Very delicate, razor-thin residual vapor trace along the swept trajectory (fades cleanly via _Alpha)
    float traceTighten = mix(2.2, 1.0, clamp(u._Alpha, 0.0, 1.0));
    float residualThread = exp(-crossDist * crossDist * 68.0 * traceTighten) * 0.34 * u._Alpha;
    float residualGlow = exp(-crossDist * crossDist * 16.0 * traceTighten) * 0.14 * u._Alpha * (0.65 + 0.35 * wisp);

    float mask = headLeadMask * originFade * endFade;
    float activeTrail = mask * clamp(u._Alpha * 1.15, 0.0, 1.0);

    vec3 col = u._Color.rgb * ((cometSheath * 0.95 + softAura + headOrb * 0.7) * activeTrail + residualGlow * mask);
    col += u._CoreColor.rgb * ((cometCore * 1.05 + headOrb * 0.45) * activeTrail + residualThread * mask);
    col *= u._Intensity * vColor.rgb;

    float outA = clamp(((cometCore + cometSheath * 0.75 + softAura + headOrb) * activeTrail + (residualThread + residualGlow) * mask) * u._Color.a * vColor.a, 0.0, 0.95);
    outColor = vec4(clamp(col, 0.0, 1.0), outA);
}
