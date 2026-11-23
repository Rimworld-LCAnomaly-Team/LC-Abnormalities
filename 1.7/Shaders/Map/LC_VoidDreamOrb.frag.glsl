#version 450

// T-02-99 Void Dream Sheep Hypnotic Light Orb (BakuSheepSkill) Shader.
// Renders a luminous pearl-white dream orb wrapped in swirling magenta-pink & emerald-mint eye halos and a 4-point star flare.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.76, 0.44, 1.0, 1.0)
    vec4 _AccentCol;  // @color @default = vec4(0.38, 0.96, 0.66, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(1.0, 0.97, 1.0, 1.0)
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Intensity; // @default = 1.45
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

void main()
{
    vec2 p = (vUv - 0.5) * 2.0;
    float r = length(p);
    if (r > 1.0)
        discard;

    float ang = atan(p.y, p.x);
    float t = u._AgeSecs;

    // Breathing pearl core + soft dreamy nebula halo
    float pulse = 0.92 + 0.08 * sin(t * 7.5);
    float core = exp(-r * r * 14.5 / pulse);
    float halo = exp(-r * r * 3.6) * 0.68;

    // Swirling dual-tone dream aura echoing Void Dream's pink & green eyes
    float swirl1 = 0.5 + 0.5 * sin(ang * 3.0 - t * 4.2 + r * 6.5);
    float swirl2 = 0.5 + 0.5 * sin(ang * 2.0 + t * 3.1 - r * 5.0);
    float ring = exp(-pow((r - 0.54 - 0.05 * sin(t * 5.0)) * 9.5, 2.0)) * 0.72;

    // Rotating 4-point twinkling starlight cross
    float ca = cos(t * 1.4);
    float sa = sin(t * 1.4);
    vec2 rp = vec2(p.x * ca - p.y * sa, p.x * sa + p.y * ca);
    float starH = exp(-abs(rp.y) * 18.0) * exp(-abs(rp.x) * 2.8);
    float starV = exp(-abs(rp.x) * 18.0) * exp(-abs(rp.y) * 2.8);
    float star = (starH + starV) * (0.68 + 0.25 * sin(t * 10.0));

    float outerSoft = smoothstep(1.0, 0.78, r);
    vec3 auraCol = mix(u._Color.rgb, u._AccentCol.rgb, swirl1 * 0.42 + swirl2 * 0.18);

    vec3 col = auraCol * (halo * 0.95 + ring * 1.15);
    col += u._CoreColor.rgb * (core * 1.35 + star * 0.85);
    col *= u._Intensity * u._Alpha * outerSoft * vColor.rgb;

    float energy = (core * 1.25 + halo + ring + star * 0.85) * outerSoft;
    float alpha = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
