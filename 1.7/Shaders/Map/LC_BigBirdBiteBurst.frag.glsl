#version 450

// Big Bird Decapitation Bite & E.G.O. Lamp Obsidian-Crimson Eclipse Burst Shader.
// Samples BigBird_EyesCrimson (BigBirdDead.png) inside a dark void sphere torn by a searing horizontal bite slash.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.92, 0.10, 0.08, 1.0)
    vec4 _VoidColor; // @color @default = vec4(0.03, 0.01, 0.04, 0.94)
    float _Progress; // @default = 0.5
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _SlashAmt; // @default = 1.0
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
    vec2 centered = (vUv - 0.5) * 2.0;
    float r = length(centered);
    if (r > 1.0)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);
    float n = valueNoise(centered * 4.5 + vec2(u._AgeSecs * 1.8, -u._AgeSecs * 1.4));

    // Expanding jagged void sphere enclosing the prey
    float sphereRadius = mix(0.28, 0.95, 1.0 - pow(1.0 - prog, 2.2));
    float voidMask = smoothstep(sphereRadius + 0.08, sphereRadius - 0.22, r + (n - 0.5) * 0.16);

    // Sample sinister crimson-eyed background from BigBirdDead.png
    vec2 eyesUv = clamp(vUv * 0.85 + 0.075 + vec2((n - 0.5) * 0.03), 0.0, 1.0);
    vec4 eyesTex = texture(_MainTex, flipV(eyesUv));
    float crimsonEye = clamp(eyesTex.r * 1.35 - eyesTex.b * 0.6, 0.0, 1.0) * eyesTex.a;

    // Razor jaw-snap slash across the center (flares violently on chomp!)
    float slashWidth = mix(0.03, 0.18, sin(prog * 3.14159));
    float slashLine = exp(-pow(centered.y / max(slashWidth, 0.015), 2.0)) * smoothstep(0.96, 0.08, abs(centered.x));
    float slashRim = exp(-pow((r - sphereRadius * 0.85) * 14.0, 2.0));
    float slashGlow = (slashLine * 1.55 + slashRim * 0.85) * u._SlashAmt;

    vec3 rgb = u._VoidColor.rgb;
    rgb = mix(rgb, u._Color.rgb * 1.25, clamp(crimsonEye * 0.95, 0.0, 1.0));
    rgb += u._Color.rgb * slashGlow * 0.95;
    rgb += vec3(1.0, 0.88, 0.72) * slashLine * u._SlashAmt * pow(1.0 - prog, 1.2) * 0.85;

    float alpha = clamp((voidMask * u._VoidColor.a + slashGlow * 0.7) * u._Alpha * vColor.a, 0.0, 0.96);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(rgb * vColor.rgb, 0.0, 1.0), alpha);
}
