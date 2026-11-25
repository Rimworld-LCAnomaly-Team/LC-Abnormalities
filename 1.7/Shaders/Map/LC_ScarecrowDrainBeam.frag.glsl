#version 450

// F-01-87 Scarecrow Searching for Wisdom Crimson Straw Drain Aura & Pulse
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.84, 0.12, 0.16, 1.0)
    vec4 _StrawColor; // @color @default = vec4(0.92, 0.72, 0.32, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float r = length(p);
    float ang = atan(p.y, p.x);

    float inward = fract(r * 2.2 + u._AgeSecs * 1.8);
    float fibers = pow(max(0.0, cos(ang * 12.0 + sin(r * 10.0 - u._AgeSecs * 5.0))), 3.0);
    float shieldRing = exp(-pow((r - 0.68) / 0.08, 2.0)) * (0.75 + 0.25 * sin(u._AgeSecs * 7.0));
    float siphon = inward * fibers * smoothstep(0.95, 0.15, r);

    vec3 col = mix(u._Color.rgb, u._StrawColor.rgb, clamp(fibers * 0.45 + shieldRing * 0.35, 0.0, 1.0));
    float intensity = (siphon * 0.65 + shieldRing * 0.90) * smoothstep(1.0, 0.78, r);
    float alpha = intensity * u._Alpha * vColor.a;

    outColor = vec4(col * intensity, clamp(alpha, 0.0, 1.0));
}
