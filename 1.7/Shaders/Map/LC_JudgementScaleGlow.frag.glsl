#version 450

// Luminous Unbalanced Scale Sigil Shader (driven by Unbalanced_Scale.png)
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 0.92, 0.58, 1.0)
    vec4 _PaleColor; // @color @default = vec4(0.68, 0.95, 1.0, 1.0)
    float _Progress; // @default = 0.5
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _Pulse;    // @default = 1.2
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

vec2 flipV(vec2 c) { return vec2(c.x, 1.0 - c.y); }

void main()
{
    vec4 tex = texture(_MainTex, flipV(vUv));
    vec2 centered = (vUv - 0.5) * 2.0;
    float rad = length(centered);

    // Subtle sacred radial aura behind the unbalanced golden scale
    float halo = exp(-rad * rad * 4.2) * (0.28 + 0.32 * u._Progress);
    if (tex.a < 0.015 && halo < 0.02)
        discard;

    // Sweeping divine verdict shimmer across the golden scale bar & pans
    float sheen = 0.5 + 0.5 * sin((vUv.x * 5.0 - vUv.y * 3.0) - u._AgeSecs * 5.5);
    float climax = smoothstep(0.65, 1.0, clamp(u._Progress, 0.0, 1.0));
    float flicker = 0.90 + 0.10 * sin(u._AgeSecs * 18.0);

    // Shift gradually from solemn antique gold toward incandescent pale-white at verdict climax
    vec3 tint = mix(u._Color.rgb, u._PaleColor.rgb, climax * 0.58);
    vec3 spriteCol = tex.rgb * tint * (1.10 + 0.45 * sheen + 0.65 * climax);
    vec3 haloCol = mix(u._Color.rgb, u._PaleColor.rgb, 0.45 + 0.45 * climax) * halo * (0.75 + 0.25 * sheen);

    vec3 col = (spriteCol * tex.a + haloCol) * u._Pulse * flicker * u._Alpha * vColor.rgb;
    float outA = clamp((tex.a * 1.15 + halo * 0.65) * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    outColor = vec4(clamp(col, 0.0, 1.0), outA);
}
