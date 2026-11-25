#version 450

// Fairy Festival Spine 3.6 Premultiplied Alpha (PMA) Fragment Shader
// Expects PMA textures (Fairy.png / Fairy2.png) and per-vertex premultiplied slot color in vColor.
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
    vec4 c = vec4(tex.rgb * vColor.rgb * (u._Color.rgb * u._Color.a), tex.a * vColor.a * u._Color.a);
    if (c.a < 0.002 && max(c.r, max(c.g, c.b)) < 0.002)
        discard;
    outColor = clamp(c, 0.0, 1.0);
}
