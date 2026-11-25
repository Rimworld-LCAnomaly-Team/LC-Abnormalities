#version 450

// Fairy Festival Spine 3.6 Additive Blend Fragment Shader
// Used for light1..6 bioluminescent orbs, Shimmer1..3 glow clouds, and Give_fairy/Feast eye flare.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

void main()
{
    vec4 tex = texture(_MainTex, vUv);
    vec3 rgb = clamp(tex.rgb * vColor.rgb * (u._Color.rgb * u._Color.a), 0.0, 1.0);
    float a = clamp(tex.a * vColor.a * u._Color.a, 0.0, 1.0);
    if (a < 0.002 && max(rgb.r, max(rgb.g, rgb.b)) < 0.002)
        discard;
    outColor = vec4(rgb, a);
}
