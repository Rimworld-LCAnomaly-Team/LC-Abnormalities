#version 450

// O-06-20 Nothing There & Mimicry E.G.O. Weapon
// _Mode = 0.0: Ground-Slam Crimson Shockwave Ring + Jagged Forward Blood Fissure
// _Mode = 1.0: Warmup Flesh-Energy Vortex Charge Aura
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.95, 0.08, 0.12, 1.0)
    vec4 _CoreColor; // @color @default = vec4(1.0, 0.78, 0.62, 1.0)
    vec4 _DarkColor; // @color @default = vec4(0.08, 0.005, 0.01, 1.0)
    float _Progress; // @default = 0.3
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _Mode;     // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

float hash21(vec2 p)
{
    p = fract(p * vec2(127.1, 311.7));
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
    vec2 p = (vUv - 0.5) * 2.0;
    float r = length(p);
    if (r > 1.0)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);

    if (u._Mode < 0.5)
    {
        // Mode 0: Ground-Slam Shockwave & Jagged Forward Fissure along +X axis
        float ringR = 0.15 + prog * 0.80;
        float ring = exp(-pow((r - ringR) / 0.085, 2.0)) * pow(1.0 - prog, 1.15);

        float ang = atan(p.y, p.x);
        float noise = valueNoise(vec2(r * 10.0 - u._AgeSecs * 4.0, ang * 4.0));
        float rays = pow(max(0.0, cos(ang * 7.0 + noise * 2.5)), 4.0) * smoothstep(0.95, 0.25, r) * (1.0 - prog);

        // Directional jagged fissure along forward slam line (+Y in UV = +Z or +X depending on rotation)
        float along = clamp(p.x * 0.5 + 0.5, 0.0, 1.0);
        float zig = (valueNoise(vec2(along * 18.0, 2.1)) - 0.5) * 0.14;
        float fissureDist = abs(p.y - zig);
        float fissureMask = smoothstep(-0.08, 0.15, p.x) * smoothstep(1.0, 0.72, p.x);
        float fissureGlow = exp(-pow(fissureDist / 0.065, 2.0)) * fissureMask * pow(1.0 - prog, 0.65);
        float fissureCore = exp(-pow(fissureDist / 0.024, 2.0)) * fissureMask * pow(1.0 - prog, 0.85);

        float craterCore = exp(-r * r * 9.0) * pow(1.0 - prog, 1.45) * 0.90;

        float weight = ring * 1.1 + rays * 0.65 + fissureGlow * 1.25 + craterCore;
        float outA = clamp(weight * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;

        vec3 col = u._Color.rgb * (ring * 1.2 + rays * 0.95 + fissureGlow * 1.1)
                 + u._CoreColor.rgb * (fissureCore * 1.4 + craterCore * 1.1 + ring * 0.45);
        col = mix(col, u._DarkColor.rgb, clamp(fissureCore * 0.35 * (1.0 - (1.0 - prog)), 0.0, 0.5));
        col *= vColor.rgb;
        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
    else
    {
        // Mode 1: Swirling Crimson Flesh-Energy Charge Aura
        float ang = atan(p.y, p.x);
        float swirl = ang + r * 5.0 - u._AgeSecs * 7.5;
        float tendrils = 0.5 + 0.5 * sin(swirl * 5.0 + valueNoise(vec2(r * 6.0, ang * 3.0)) * 3.0);
        float contractR = mix(0.88, 0.32, prog);
        float pulseRing = exp(-pow((r - contractR) / 0.12, 2.0)) * (0.65 + 0.45 * tendrils);
        float outerMist = smoothstep(1.0, 0.25, r) * tendrils * 0.55;
        float centerFlash = exp(-r * r * 6.0) * (0.4 + 0.6 * prog);

        float weight = (pulseRing * 1.25 + outerMist + centerFlash) * u._Alpha;
        float outA = clamp(weight * u._Color.a * vColor.a, 0.0, 1.0);
        if (outA < 0.01)
            discard;

        vec3 col = u._Color.rgb * (pulseRing * 1.3 + outerMist * 1.1)
                 + u._CoreColor.rgb * (centerFlash * 1.1 + pulseRing * prog * 0.45);
        col *= vColor.rgb;
        outColor = vec4(clamp(col, 0.0, 1.0), outA);
    }
}
