#version 450

// Fairy Festival Jealous Devouring Feast Execution Shader (referencing fairyAgentDead)
// Swirling crimson gore vortex, jagged predatory bite-rip flashes, and corrupted mint-to-blood halo.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.92, 0.08, 0.12, 1.0)
    vec4 _MintColor;  // @color @default = vec4(0.28, 0.98, 0.68, 1.0)
    vec4 _VoidColor;  // @color @default = vec4(0.10, 0.01, 0.02, 0.92)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Frenzy;    // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

vec2 flipV(vec2 c) { return vec2(c.x, 1.0 - c.y); }

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
    vec2 p = (vUv - 0.5) * 2.0;
    float r = length(p);
    if (r > 1.0)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);
    float ang = atan(p.y, p.x);

    // Swirling predatory swarm vortex
    float swirlAng = ang + r * 3.8 - u._AgeSecs * (4.5 + u._Frenzy * 3.5);
    vec2 swirlUv = vec2(cos(swirlAng), sin(swirlAng)) * r;
    float n1 = valueNoise(swirlUv * 4.5 + vec2(u._AgeSecs * 2.2, -u._AgeSecs * 1.9));
    float n2 = valueNoise(p * 9.5 - vec2(u._AgeSecs * 3.4, u._AgeSecs * 2.7));

    // Gore mark texture sampled at center (dead/belly_hole)
    vec2 texUv = clamp(vUv + (vec2(n1, n2) - 0.5) * 0.05 * u._Frenzy, 0.0, 1.0);
    vec4 goreTex = texture(_MainTex, flipV(texUv));

    // Expanding jagged blood-mist domain
    float domainR = mix(0.32, 0.94, 1.0 - pow(1.0 - prog, 2.0));
    float mistMask = smoothstep(domainR + 0.08, domainR - 0.28, r + (n1 - 0.5) * 0.22);

    // Transition from deceptive mint glow (prog < 0.22) into violent blood-red devouring
    float corruption = smoothstep(0.05, 0.28, prog);
    vec3 primaryCol = mix(u._MintColor.rgb, u._Color.rgb, corruption);

    // Rapid erratic tear/chomp claw slashes flashing across center during frenzy
    float chompPulse = pow(0.5 + 0.5 * sin(u._AgeSecs * 26.0), 3.0) * u._Frenzy;
    float slash1 = exp(-pow((p.y * 0.85 + p.x * 0.52) / 0.075, 2.0)) * smoothstep(0.88, 0.12, r);
    float slash2 = exp(-pow((p.y * 0.82 - p.x * 0.57) / 0.075, 2.0)) * smoothstep(0.88, 0.12, r);
    float biteFlash = (slash1 + slash2) * (0.45 + 0.95 * chompPulse) * corruption;

    // Crimson pulsing ring matching fairyAgentDead effect_eating/light
    float ringR = mix(0.25, 0.82, 0.5 + 0.5 * sin(u._AgeSecs * 7.5));
    float crimsonRing = exp(-pow((r - ringR) / 0.085, 2.0)) * (0.55 + 0.45 * n2);

    // Splatter droplets flying outward
    float splatter = smoothstep(0.68, 0.86, n2) * smoothstep(0.98, 0.22, r) * corruption;

    vec3 rgb = u._VoidColor.rgb * mistMask;
    rgb = mix(rgb, goreTex.rgb * primaryCol * 1.45, clamp(goreTex.a * 0.88 * corruption, 0.0, 1.0));
    rgb += primaryCol * (crimsonRing * 0.95 + splatter * 1.15 + biteFlash * 0.85);
    rgb += vec3(1.0, 0.82, 0.82) * biteFlash * 0.65;

    float outA = clamp((mistMask * u._VoidColor.a * 0.78 + crimsonRing * 0.65 + biteFlash * 0.75 + splatter * 0.55 + goreTex.a * 0.55 * corruption) * u._Alpha * vColor.a, 0.0, 0.96);
    if (outA < 0.01)
        discard;

    outColor = vec4(clamp(rgb * vColor.rgb, 0.0, 1.0), outA);
}
