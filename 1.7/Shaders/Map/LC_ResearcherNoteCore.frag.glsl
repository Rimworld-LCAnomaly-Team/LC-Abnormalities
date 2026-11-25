#version 450

// Notes from a Crazed Researcher (T-09-78) Living Notebook Core Shader:
// Renders phosphorescent manic ink-glow, subtle page flutter, emerald readiness shift,
// and swelling crimson distortion as cumulative damage nears the lethal threshold.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _GlowColor;  // @color @default = vec4(0.45, 0.95, 0.42, 1.0)
    float _AgeSecs;   // @default = 0.0
    float _DangerAmt; // @default = 0.0
    float _ReadyAmt;  // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

void main()
{
    vec2 center = vec2(0.50, 0.50);
    vec2 d = vUv - center;
    float dist = length(d);

    // Subtle page flutter + swelling expansion under high cumulative damage
    float flutter = sin(vUv.y * 10.0 - u._AgeSecs * 4.5) * 0.008;
    float swell = u._DangerAmt * (0.04 + 0.03 * sin(u._AgeSecs * 18.0)) * smoothstep(0.48, 0.06, dist);
    vec2 uvWarped = clamp(vUv + vec2(flutter, 0.0) - d * swell, vec2(0.0), vec2(1.0));

    vec4 tex = texture(_MainTex, uvWarped);
    if (tex.a < 0.05)
        discard;

    float danger = clamp(u._DangerAmt, 0.0, 1.0);
    float ready = clamp(u._ReadyAmt, 0.0, 1.0);

    vec3 readyCol = vec3(0.32, 0.96, 0.72);
    vec3 dangerCol = vec3(1.0, 0.16, 0.24);
    vec3 activeGlow = mix(u._GlowColor.rgb, readyCol, ready * (1.0 - danger));
    activeGlow = mix(activeGlow, dangerCol, smoothstep(0.25, 0.90, danger));

    float pulse = 0.5 + 0.5 * sin(u._AgeSecs * mix(3.2, 22.0, danger));
    float coreGlow = exp(-dist * dist * 16.0) * (0.20 + 0.35 * pulse + 0.55 * danger);
    float rimGlow = smoothstep(0.22, 0.46, dist) * (0.18 + 0.42 * danger);

    vec3 rgb = tex.rgb + activeGlow * (coreGlow + rimGlow);
    if (danger > 0.40)
    {
        rgb = mix(rgb, vec3(0.95, 0.14, 0.20), (danger - 0.40) * 0.45 * pulse);
    }

    outColor = vec4(clamp(rgb, 0.0, 1.0), tex.a) * u._Color * vColor;
}
