#version 450

// Luminous arcane magic-circle portal shader for matanjin_1 / matanjin_2 runic gates.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.24, 0.66, 1.0, 1.0)
    vec4 _RimColor;  // @color @default = vec4(0.85, 0.97, 1.0, 1.0)
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _Pulse;    // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

vec2 flipV(vec2 c) { return vec2(c.x, 1.0 - c.y); }

void main()
{
    vec4 tex = texture(_MainTex, flipV(vUv));
    if (tex.a < 0.02)
        discard;

    // Extract fine runic linework from the sprite (dark blue lines on pale translucent fill)
    float lum = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
    float runeInk = clamp((0.86 - lum) * 2.2, 0.0, 1.0) * tex.a;
    float fillGlow = tex.a * 0.34;

    // Foreshortened elliptical coordinates for shimmering angular rune sweep
    vec2 centered = (vUv - 0.5) * vec2(2.6, 1.0);
    float rad = length(centered);
    float ang = atan(centered.y, centered.x);

    float sweep = 0.5 + 0.5 * sin(ang * 3.0 - u._AgeSecs * 5.2);
    float ripple = 0.5 + 0.5 * sin(rad * 18.0 - u._AgeSecs * 9.0);
    float centerBloom = exp(-rad * rad * 10.0) * 0.65 * tex.a;

    vec3 col = u._Color.rgb * (fillGlow + runeInk * (1.25 + 0.55 * sweep) + centerBloom * 0.9);
    col += u._RimColor.rgb * (runeInk * (0.45 + 0.55 * sweep * ripple) + centerBloom * 0.55);
    col *= u._Pulse * u._Alpha * vColor.rgb;

    float alpha = clamp((fillGlow + runeInk * 1.1 + centerBloom) * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
