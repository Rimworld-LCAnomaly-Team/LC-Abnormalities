#version 450

// O-03-60 Fragment of the Universe Psychedelic Cosmic Song Resonance Ring
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.66, 0.24, 0.98, 1.0)
    vec4 _AccentColor;// @color @default = vec4(0.18, 0.88, 0.96, 1.0)
    float _Progress;  // @default = 0.4
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

void main()
{
    vec2 p = vUv * 2.0 - 1.0;
    float r = length(p);
    float ang = atan(p.y, p.x);

    float wave1Radius = fract(u._Progress + 0.05);
    float wave2Radius = fract(u._Progress + 0.55);
    float w1 = exp(-pow((r - mix(0.12, 0.92, wave1Radius)) / 0.06, 2.0));
    float w2 = exp(-pow((r - mix(0.12, 0.92, wave2Radius)) / 0.05, 2.0)) * 0.75;

    float harmonic = 0.5 + 0.5 * sin(ang * 7.0 + u._AgeSecs * 4.2 + r * 8.0);
    vec3 prism = mix(u._Color.rgb, u._AccentColor.rgb, harmonic);
    prism = mix(prism, vec3(1.0, 0.28, 0.62), smoothstep(0.65, 1.0, harmonic) * 0.55);

    float starRays = pow(max(0.0, cos(ang * 4.0 - u._AgeSecs * 2.0)), 6.0) * smoothstep(0.95, 0.2, r) * 0.35;
    float intensity = (w1 + w2 + starRays) * smoothstep(1.0, 0.85, r);
    float fade = (1.0 - u._Progress * 0.85) * u._Alpha * vColor.a;

    outColor = vec4(prism * intensity, clamp(intensity * fade, 0.0, 1.0));
}
