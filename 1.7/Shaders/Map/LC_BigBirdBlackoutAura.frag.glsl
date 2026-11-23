#version 450

// Big Bird Breach Blackout Shroud: dims the ground around escaped Big Bird like a Lobotomy Corp department blackout,
// while letting ghostly golden eyes peek through swirling dark-forest shadows and keeping the lantern center warmly lit.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(0.02, 0.015, 0.03, 0.62)
    vec4 _EyeTint;    // @color @default = vec4(1.0, 0.82, 0.22, 1.0)
    float _AgeSecs;   // @default = 0.0
    float _Alpha;     // @default = 1.0
    float _Intensity; // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

vec2 flipV(vec2 c) { return vec2(c.x, 1.0 - c.y); }

float hash21(vec2 p)
{
    p = fract(p * vec2(127.1, 311.7));
    p += dot(p, p + 34.5);
    return fract(p.x * p.y);
}

float valueNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void main()
{
    vec2 centered = (vUv - 0.5) * 2.0;
    float r = length(centered);
    if (r > 1.0)
        discard;

    float ang = atan(centered.y, centered.x);
    float swirl = valueNoise(vec2(cos(ang) * 2.2 + r * 3.0, sin(ang) * 2.2 - u._AgeSecs * 0.45));
    float mist = valueNoise(centered * 3.8 + vec2(u._AgeSecs * 0.22, -u._AgeSecs * 0.16));

    // Soft annular blackout shroud: clear lantern pool in center, oppressive darkness in mid-field, soft edge fade
    float centerPool = smoothstep(0.08, 0.34, r);
    float outerFade = smoothstep(1.0, 0.42, r + (swirl - 0.5) * 0.14);
    float shroud = centerPool * outerFade * (0.76 + 0.24 * mist);

    // Subtle ghostly BigBirdArrived eyes peeking in the mid-darkness band
    vec2 eyeUv = fract(vUv * 1.45 + vec2(sin(u._AgeSecs * 0.15) * 0.04, cos(u._AgeSecs * 0.12) * 0.04));
    vec4 eyeTex = texture(_MainTex, flipV(eyeUv));
    float eyeGold = clamp((eyeTex.r + eyeTex.g) * 0.5 - eyeTex.b * 0.9, 0.0, 1.0) * eyeTex.a;
    float eyeBand = smoothstep(0.28, 0.52, r) * smoothstep(0.88, 0.58, r);
    float eyePulse = 0.35 + 0.65 * sin(u._AgeSecs * 2.4 + swirl * 6.2831);
    float eyeShow = eyeGold * eyeBand * clamp(eyePulse, 0.0, 1.0) * 0.34 * u._Intensity;

    vec3 rgb = mix(u._Color.rgb, u._EyeTint.rgb, clamp(eyeShow * 1.6, 0.0, 0.85));
    float alpha = clamp((shroud * u._Color.a + eyeShow * 0.45) * u._Alpha * vColor.a, 0.0, 0.78);
    if (alpha < 0.01)
        discard;

    outColor = vec4(rgb * vColor.rgb, alpha);
}
