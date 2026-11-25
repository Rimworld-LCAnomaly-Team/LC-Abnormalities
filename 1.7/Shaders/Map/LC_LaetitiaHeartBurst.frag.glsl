#version 450

// O-01-67 Laetitia E.G.O. Weapon Heart-Lace Muzzle Bloom & Heartbreak Black-Lace AoE Burst Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.95, 0.24, 0.58, 1.0)
    vec4 _CoreColor; // @color @default = vec4(0.18, 0.03, 0.22, 1.0)
    float _Progress; // @default = 0.35
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _Mode;     // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

// Implicit heart distance approximation centered around origin
float heartField(vec2 p)
{
    p.x *= 1.08;
    p.y = -p.y + 0.14;
    float a = p.x * p.x + p.y * p.y - 0.26;
    return a * a * a - p.x * p.x * p.y * p.y * p.y;
}

void main()
{
    vec2 p = (vUv - 0.5) * 2.0; // -1..+1
    float r = length(p);
    if (r > 1.0)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);
    float fade = pow(1.0 - prog, 0.72) * u._Alpha;
    if (fade <= 0.01)
        discard;

    float ang = atan(p.y, p.x);

    if (u._Mode < 0.5)
    {
        // Mode 0: Crimson-magenta heart bloom with sparkling ribbon rays
        float hDist = heartField(p * mix(1.35, 0.85, prog));
        float heart = smoothstep(0.018, -0.008, hDist);
        float ribbonRays = pow(abs(sin(ang * 4.0 + u._AgeSecs * 6.0)), 5.0) * smoothstep(0.85, 0.15, r);
        float aura = exp(-pow(r / 0.48, 2.0));

        float outA = clamp((heart * 0.95 + ribbonRays * 0.60 + aura * 0.55) * fade, 0.0, 1.0);
        if (outA <= 0.01)
            discard;

        vec3 col = mix(u._Color.rgb, vec3(1.0, 0.82, 0.92), heart * 0.55)
                 + u._CoreColor.rgb * (aura * 0.35)
                 + vec3(1.0, 0.55, 0.80) * ribbonRays * 0.65;
        outColor = vec4(clamp(col * vColor.rgb, 0.0, 1.0), outA * u._Color.a * vColor.a);
        return;
    }

    // Mode 1: Expanding Heartbreak Black-Lace AoE ring & shattered obsidian heart aura
    float ringR = mix(0.16, 0.92, 1.0 - pow(1.0 - prog, 1.8));
    float laceRipple = 0.06 * cos(ang * 12.0 - u._AgeSecs * 8.0);
    float mainRing = exp(-pow((r - (ringR + laceRipple)) / 0.085, 2.0));
    float blackLace = exp(-pow((r - (ringR * 0.78 - laceRipple)) / 0.095, 2.0)) * (0.55 + 0.45 * sin(ang * 12.0));
    float centerHeart = smoothstep(0.015, -0.008, heartField(p * (1.2 + prog * 0.7)));

    float outA = clamp((mainRing * 1.15 + blackLace * 0.85 + centerHeart * 0.80) * fade, 0.0, 1.0);
    if (outA <= 0.01)
        discard;

    vec3 col = u._Color.rgb * (mainRing * 1.2 + centerHeart * 0.85)
             + u._CoreColor.rgb * (blackLace * 1.1 + centerHeart * 0.35)
             + vec3(1.0, 0.75, 0.90) * (mainRing * 0.45);
    outColor = vec4(clamp(col * vColor.rgb, 0.0, 1.0), outA * u._Color.a * vColor.a);
}
