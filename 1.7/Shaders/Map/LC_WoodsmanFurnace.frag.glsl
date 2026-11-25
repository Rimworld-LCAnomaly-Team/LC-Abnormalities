#version 450

// F-05-32 Warm-Hearted Woodsman Chest Furnace Heart Pulse & Amber Ember Aura
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.96, 0.14, 0.12, 1.0)
    vec4 _EmberColor; // @color @default = vec4(1.0, 0.58, 0.18, 1.0)
    float _Progress;  // @default = 0.5
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float r = length(p);
    float beat = 0.72 + 0.28 * pow(max(0.0, sin(u._AgeSecs * 5.2)), 4.0);

    float heartGlow = exp(-r * r * (7.5 / max(beat, 0.4)));
    float heatRing = exp(-pow((r - (0.42 + 0.10 * sin(u._AgeSecs * 4.0))) / 0.09, 2.0));
    float sparks = max(0.0, sin(atan(p.y, p.x) * 9.0 + u._AgeSecs * 3.0) * cos(r * 14.0 - u._AgeSecs * 6.0));
    float vignette = smoothstep(0.98, 0.55, r);

    vec3 col = mix(u._Color.rgb, u._EmberColor.rgb, clamp(heartGlow * 0.5 + sparks * 0.55, 0.0, 1.0));
    float intensity = (heartGlow * 0.90 + heatRing * 0.60 + sparks * 0.30) * vignette * beat;
    float alpha = intensity * u._Alpha * vColor.a;

    outColor = vec4(col * intensity, clamp(alpha, 0.0, 1.0));
}
