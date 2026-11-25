#version 450

// D-02-107 Ppodae Frenzy Crimson Shockwave & Paw Slam Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.95, 0.16, 0.22, 1.0)
    vec4 _CoreColor; // @color @default = vec4(1.0, 0.86, 0.80, 1.0)
    float _Progress; // @default = 0.35
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float r = length(p);
    float ang = atan(p.y, p.x);

    float ringRadius = mix(0.14, 0.88, u._Progress);
    float ringWidth = mix(0.14, 0.04, u._Progress);
    float shockRing = exp(-pow((r - ringRadius) / max(ringWidth, 0.01), 2.0));

    float rays = pow(max(0.0, cos(ang * 5.0 - u._AgeSecs * 4.5)), 4.0) * smoothstep(0.95, 0.15, r);
    float core = exp(-r * r * 14.0) * (1.0 - u._Progress);

    vec3 col = mix(u._Color.rgb, u._CoreColor.rgb, clamp(core + shockRing * 0.55, 0.0, 1.0));
    float intensity = (shockRing * 0.95 + rays * 0.45 + core * 0.85) * smoothstep(1.0, 0.82, r);
    float alpha = intensity * u._Alpha * (1.0 - u._Progress * 0.85) * vColor.a;

    outColor = vec4(col * intensity, clamp(alpha, 0.0, 1.0));
}
