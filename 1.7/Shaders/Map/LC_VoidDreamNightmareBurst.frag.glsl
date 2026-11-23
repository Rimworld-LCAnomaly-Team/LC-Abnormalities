#version 450

// T-02-99 Void Dream Nightmare Shatter Burst Shader (2nd crow or 60s timeout White+Black dual shock).
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.84, 0.22, 0.72, 1.0)
    vec4 _VoidColor; // @color @default = vec4(0.05, 0.01, 0.10, 0.94)
    float _Progress; // @default = 0.5
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

void main()
{
    vec2 p = (vUv - 0.5) * 2.0;
    float r = length(p);
    if (r > 1.0)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);
    float ang = atan(p.y, p.x);

    // Expanding jagged obsidian-violet nightmare void sphere
    float jag = sin(ang * 7.0 + u._AgeSecs * 6.0) * 0.06 + cos(ang * 13.0 - prog * 8.0) * 0.04;
    float sphereR = mix(0.20, 0.94, 1.0 - pow(1.0 - prog, 2.2));
    float voidMask = smoothstep(sphereR + 0.06, sphereR - 0.20, r + jag);

    // Jagged dream-glass fracture spikes radiating from center (White+Black mental shatter)
    float rays = pow(abs(sin(ang * 6.0 + 0.5)), 8.0) * smoothstep(sphereR, 0.02, r);
    float rim = exp(-pow((r - sphereR * 0.88 - jag) * 15.0, 2.0));
    float whiteCore = exp(-r * r * 12.0) * pow(1.0 - prog, 1.35);

    vec3 rgb = u._VoidColor.rgb;
    rgb += u._Color.rgb * (rim * 1.35 + rays * 1.15);
    rgb += vec3(1.0, 0.94, 1.0) * (whiteCore * 1.35 + rays * 0.45);

    float alpha = clamp((voidMask * u._VoidColor.a + rim * 0.85 + rays * 0.75 + whiteCore * 0.8) * u._Alpha * vColor.a, 0.0, 0.96);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(rgb * vColor.rgb, 0.0, 1.0), alpha);
}
