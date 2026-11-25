#version 450

// O-02-56 Punishing Bird E.G.O. Weapon (Beak) Peck Starburst & Crimson Maw Punish Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.95, 0.16, 0.20, 1.0)
    vec4 _CoreColor; // @color @default = vec4(1.0, 0.96, 0.92, 1.0)
    float _Progress; // @default = 0.35
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _Mode;     // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

void main()
{
    vec2 p = (vUv - 0.5) * 2.0; // -1..+1
    float r = length(p);
    if (r > 1.0)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);
    float fade = pow(1.0 - prog, 0.72) * u._Alpha;
    if (fade <= 0.01)
        discard;

    float ang = atan(p.y, p.x);

    if (u._Mode < 0.5)
    {
        // Mode 0: Snappy 6-point white-crimson peck starburst & muzzle spark
        float star = pow(abs(cos(ang * 3.0 + u._AgeSecs * 4.0)), 6.0);
        float beamR = 0.24 + 0.56 * star;
        float burst = smoothstep(beamR, beamR * 0.25, r) * (1.0 - prog * 0.4);
        float core = exp(-pow(r / 0.18, 2.0));
        float ring = exp(-pow((r - mix(0.18, 0.85, prog)) / 0.075, 2.0));

        float outA = clamp((burst + ring * 0.85 + core) * fade, 0.0, 1.0);
        if (outA <= 0.01)
            discard;

        vec3 col = u._Color.rgb * (burst * 0.85 + ring * 1.15) + u._CoreColor.rgb * (core * 1.35 + burst * 0.35);
        outColor = vec4(clamp(col * vColor.rgb, 0.0, 1.0), outA * u._Color.a * vColor.a);
        return;
    }

    // Mode 1: Crimson enraged Punishing Maw snap shockwave with sharp radial fangs
    float teeth = smoothstep(0.30, 0.72, cos(ang * 8.0));
    float ringPos = mix(0.20, 0.88, 1.0 - pow(1.0 - prog, 1.6));
    float biteRing = exp(-pow((r - ringPos) / (0.11 + 0.08 * teeth), 2.0));
    float fangSpikes = smoothstep(0.68, 0.25, r) * pow(max(0.0, cos(ang * 8.0)), 4.0) * smoothstep(0.14, 0.28, r);
    float darkVoid = smoothstep(0.55, 0.12, r) * 0.85 * (1.0 - fangSpikes);

    float outA = clamp((biteRing * 1.15 + fangSpikes * 0.95 + darkVoid * 0.65) * fade, 0.0, 1.0);
    if (outA <= 0.01)
        discard;

    vec3 col = u._Color.rgb * (biteRing * 1.25 + fangSpikes * 0.45)
             + u._CoreColor.rgb * (fangSpikes * 1.15)
             + vec3(0.10, 0.01, 0.03) * darkVoid;
    outColor = vec4(clamp(col * vColor.rgb, 0.0, 1.0), outA * u._Color.a * vColor.a);
}
