#version 450

// The Heart of Aspiration (T-09-77) Living Heart Core Shader:
// Illuminates the dark red inner clot synchronized with the lub-dub double heartbeat,
// escalating into erratic crimson-violet pulses when Restless or rich ruby-gold when Satisfied.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;          // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _GlowColor;      // @color @default = vec4(1.0, 0.18, 0.24, 1.0)
    float _AgeSecs;       // @default = 0.0
    float _BeatAmt;       // @default = 0.0
    float _RestlessAmt;   // @default = 0.0
    float _SatisfiedAmt;  // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

void main()
{
    vec2 center = vec2(0.50, 0.46);
    vec2 d = vUv - center;
    float dist = length(d);

    // Subtle localized pumping expansion inside the heart clot
    float clotMask = smoothstep(0.32, 0.06, dist);
    vec2 uvPumped = vUv - d * (u._BeatAmt * 0.055 * clotMask);

    vec4 tex = texture(_MainTex, clamp(uvPumped, vec2(0.0), vec2(1.0)));
    if (tex.a < 0.05)
        discard;

    vec3 rgb = tex.rgb;
    float coreGlow = exp(-dist * dist * 34.0) * (0.45 + 0.85 * u._BeatAmt);

    // Blend normal crimson, restless crimson-violet, and satisfied ruby-gold core tints
    vec3 restlessTint = vec3(1.0, 0.14, 0.46);
    vec3 satisfiedTint = vec3(1.0, 0.62, 0.24);
    vec3 activeGlow = mix(u._GlowColor.rgb, restlessTint, clamp(u._RestlessAmt, 0.0, 1.0));
    activeGlow = mix(activeGlow, satisfiedTint, clamp(u._SatisfiedAmt, 0.0, 1.0));

    // Rim aspiration luminescence
    float rim = smoothstep(0.18, 0.44, dist) * (0.25 + 0.45 * u._BeatAmt);
    rgb += activeGlow * (coreGlow * 0.95 + rim * 0.35);

    // Restless strobe shimmer when nearing 60s frenzy
    if (u._RestlessAmt > 0.01)
    {
        float flicker = 0.5 + 0.5 * sin(u._AgeSecs * 26.0 + dist * 16.0);
        rgb += vec3(0.55, 0.05, 0.22) * u._RestlessAmt * flicker * 0.45;
    }

    outColor = vec4(clamp(rgb, 0.0, 1.0), tex.a) * u._Color * vColor;
}
