#version 450

// F-05-52 Opened Can of WellCheers - E.G.O. Weapon "Soda" Bolt Carbonated Aura Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.68, 0.34, 1.0, 1.0)
    vec4 _AccentCol;  // @color @default = vec4(1.0, 0.26, 0.50, 1.0)
    vec4 _CoreColor;  // @color @default = vec4(1.0, 0.94, 1.0, 1.0)
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Intensity; // @default = 1.35
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

    float pulse = 0.92 + 0.08 * sin(t * 14.0);
    float core = exp(-r * r * 12.0 / pulse);
    float halo = exp(-r * r * 3.4) * 0.68;

    // Swirling grape & cherry effervescent shimmer ring
    float swirl = 0.5 + 0.5 * sin(ang * 4.0 - t * 9.0 + r * 7.0);
    float fizzRing = exp(-pow((r - 0.52 - 0.04 * sin(t * 10.0)) * 9.5, 2.0)) * (0.55 + 0.45 * swirl);

    // Sparkling micro-glints orbiting the bolt
    float glint1 = exp(-pow(r - 0.44, 2.0) * 45.0) * pow(0.5 + 0.5 * sin(ang * 3.0 + t * 12.0), 6.0) * 0.95;

    float outerSoft = smoothstep(1.0, 0.72, r);
    vec3 sodaHue = mix(u._Color.rgb, u._AccentCol.rgb, swirl * 0.44);

    vec3 col = sodaHue * (halo * 0.95 + fizzRing * 1.15)
             + u._CoreColor.rgb * (core * 1.30 + glint1 * 0.90);
    col *= u._Intensity * u._Alpha * outerSoft * vColor.rgb;

    float energy = (core * 1.15 + halo + fizzRing + glint1) * outerSoft;
    float alpha = clamp(energy * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
