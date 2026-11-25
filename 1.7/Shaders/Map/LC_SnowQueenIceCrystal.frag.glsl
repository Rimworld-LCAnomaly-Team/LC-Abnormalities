#version 450

// The Snow Queen (F-01-37) Crystalline Ice Prism & Frozen Captive Shader:
// Applies a diagonal icy specular sweep, pale-sapphire rim shimmer, and urgent freeze flicker.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _GlowColor;  // @color @default = vec4(0.48, 0.92, 1.0, 1.0)
    float _AgeSecs;   // @default = 0.0
    float _Urgency;   // @default = 0.0
    float _Shimmer;   // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

void main()
{
    vec4 tex = texture(_MainTex, vUv);
    if (tex.a < 0.05)
        discard;

    vec2 d = vUv - vec2(0.5, 0.5);
    float dist = length(d);

    // Diagonal crystalline specular glint sweep
    float sweepCoord = vUv.x * 0.75 + vUv.y * 0.55 - fract(u._AgeSecs * 0.42) * 1.8 + 0.45;
    float glint = exp(-sweepCoord * sweepCoord * 42.0) * 0.48 * u._Shimmer;

    // Breathing icy edge sheen
    float pulse = 0.5 + 0.5 * sin(u._AgeSecs * (2.8 + 9.0 * u._Urgency));
    float rim = smoothstep(0.18, 0.48, dist) * (0.18 + 0.34 * pulse);

    // Urgent frost surge as 90s timer nears zero
    float urgentFlash = u._Urgency * (0.22 + 0.38 * (0.5 + 0.5 * sin(u._AgeSecs * 14.0)));

    vec3 rgb = tex.rgb + u._GlowColor.rgb * (glint + rim + urgentFlash);
    outColor = vec4(clamp(rgb, 0.0, 1.0), tex.a) * u._Color * vColor;
}
