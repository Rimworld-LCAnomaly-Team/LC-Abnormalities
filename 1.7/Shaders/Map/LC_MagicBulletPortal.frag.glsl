#version 450

// Faithful Lobotomy Corporation steel-blue runic magic circle shader (Transparent alpha-blended)
// Preserves the crisp geometric dark-slate & cobalt linework of matanjin_1 / matanjin_2 with a subtle cool shimmer.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.32, 0.56, 0.88, 1.0)
    vec4 _RimColor;  // @color @default = vec4(0.75, 0.89, 1.0, 1.0)
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

    // Gentle rotating sheen across the geometric runes without washing out the original dark-blue linework
    vec2 centered = (vUv - 0.5) * vec2(2.6, 1.0);
    float ang = atan(centered.y, centered.x);
    float sweep = 0.5 + 0.5 * sin(ang * 2.0 - u._AgeSecs * 3.4);

    // Keep original matanjin sprite contrast, subtly tinted with cool cobalt luminescence
    float lum = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
    float isLine = clamp((0.82 - lum) * 2.0, 0.0, 1.0);

    vec3 baseCol = mix(tex.rgb * vec3(0.78, 0.88, 1.0), u._Color.rgb * 1.15, 0.45);
    vec3 highlight = u._RimColor.rgb * (isLine * (0.14 + 0.22 * sweep) * u._Pulse);
    vec3 col = clamp(baseCol + highlight, 0.0, 1.0) * vColor.rgb;

    float alpha = clamp(tex.a * (0.78 + 0.22 * isLine) * u._Alpha * u._Color.a * vColor.a, 0.0, 0.94);
    outColor = vec4(col, alpha);
}
