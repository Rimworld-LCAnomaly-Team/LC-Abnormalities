#version 450

// T-02-99 Void Dream Hypnotic Slumber Cloud & Escalating Nightmare Static Shader.
// Samples VoidDream_CloudPuff.png tinted pastel lavender-violet, shifting toward pulsing crimson-violet nightmare static as _Danger -> 1.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.84, 0.76, 0.98, 0.90)
    vec4 _DangerCol;  // @color @default = vec4(0.88, 0.16, 0.44, 0.96)
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Danger;    // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

vec2 flipV(vec2 c) { return vec2(c.x, 1.0 - c.y); }

void main()
{
    vec2 centered = (vUv - 0.5) * 2.0;
    float r = length(centered);
    if (r > 1.02)
        discard;

    float t = u._AgeSecs;
    float danger = clamp(u._Danger, 0.0, 1.0);

    // Gentle dreamy cloud wobble; intensifies into jittery nightmare tremor near detonation
    vec2 wobble = vec2(
        sin(vUv.y * 8.0 + t * (2.2 + danger * 8.0)) * (0.012 + danger * 0.016),
        cos(vUv.x * 7.0 - t * (1.8 + danger * 7.0)) * (0.010 + danger * 0.014)
    );
    vec2 sampleUv = clamp(vUv + wobble, 0.0, 1.0);
    vec4 cloudTex = texture(_MainTex, flipV(sampleUv));

    // Orbiting hypnotic dream halo ring around the sleeper
    float ang = atan(centered.y, centered.x);
    float ringR = 0.76 + 0.04 * sin(ang * 3.0 + t * 3.0);
    float ring = exp(-pow((r - ringR) * 14.0, 2.0)) * (0.42 + 0.48 * danger);
    float innerGlow = exp(-r * r * 3.2) * (0.30 + 0.25 * sin(t * (3.5 + danger * 7.5)));

    float pulse = 0.85 + 0.15 * sin(t * (3.0 + danger * 11.0));
    vec3 tint = mix(u._Color.rgb, u._DangerCol.rgb, danger * 0.78);

    vec3 rgb = cloudTex.rgb * mix(vec3(0.95, 0.92, 1.0), tint, 0.42 + danger * 0.42);
    rgb += tint * (ring * 0.95 + innerGlow * 0.55) * pulse;

    float cloudAlpha = cloudTex.a * 0.84;
    float alpha = clamp((cloudAlpha + ring * 0.65 + innerGlow * 0.35) * u._Alpha * vColor.a, 0.0, 0.95);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(rgb * vColor.rgb, 0.0, 1.0), alpha);
}
