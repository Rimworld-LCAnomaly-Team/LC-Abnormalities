#version 450

// O-03-03 One Sin and Hundreds of Good Deeds E.G.O. Weapon (Penitence) Radiant Cross & Absolution Halo Shader
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.92, 0.96, 1.0, 1.0)
    vec4 _CoreColor; // @color @default = vec4(1.0, 0.92, 0.68, 1.0)
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
    float fade = pow(1.0 - prog, 0.68) * u._Alpha;
    if (fade <= 0.01)
        discard;

    float crossH = exp(-pow(abs(p.y) / 0.095, 2.0)) * smoothstep(0.95, 0.15, abs(p.x));
    float crossV = exp(-pow(abs(p.x) / 0.095, 2.0)) * smoothstep(0.95, 0.15, abs(p.y));
    float holyCross = max(crossH, crossV);
    float coreHalo = exp(-pow(r / 0.28, 2.0));

    if (u._Mode < 0.5)
    {
        // Mode 0: Crisp radiant cross flash on strike / smite
        float ring = exp(-pow((r - mix(0.20, 0.85, prog)) / 0.08, 2.0));
        float outA = clamp((holyCross * 0.92 + coreHalo * 0.85 + ring * 0.72) * fade, 0.0, 1.0);
        if (outA <= 0.01)
            discard;

        vec3 col = u._Color.rgb * (ring + holyCross * 0.60)
                 + u._CoreColor.rgb * (coreHalo * 1.15 + holyCross * 0.70);
        outColor = vec4(clamp(col * vColor.rgb, 0.0, 1.0), outA * u._Color.a * vColor.a);
        return;
    }

    // Mode 1: Expanding Absolution sanctum ring with luminous cross beams
    float ringPos = mix(0.18, 0.92, 1.0 - pow(1.0 - prog, 1.7));
    float sanctumRing = exp(-pow((r - ringPos) / 0.095, 2.0));
    float innerRing = exp(-pow((r - ringPos * 0.62) / 0.06, 2.0)) * 0.75;
    float rays = pow(abs(cos(atan(p.y, p.x) * 2.0)), 6.0) * smoothstep(0.96, 0.12, r);

    float outA = clamp((sanctumRing * 1.15 + innerRing + rays * 0.68 + coreHalo * 0.65) * fade, 0.0, 1.0);
    if (outA <= 0.01)
        discard;

    vec3 col = u._Color.rgb * (sanctumRing * 1.25 + innerRing * 0.85)
             + u._CoreColor.rgb * (rays * 0.95 + coreHalo * 1.10);
    outColor = vec4(clamp(col * vColor.rgb, 0.0, 1.0), outA * u._Color.a * vColor.a);
}
