#version 450

// Concentric hypnotic Big Bird Eye Sigil shader:
// Used above enchanted colonists (shifting Yellow -> Orange -> Crimson as threat escalates)
// and above victims afflicted with E.G.O. Weapon (Lamp) Black-vulnerability gaze.
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 0.88, 0.22, 1.0)
    vec4 _RimColor;  // @color @default = vec4(1.0, 0.98, 0.75, 1.0)
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _Danger;   // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

vec2 flipV(vec2 c) { return vec2(c.x, 1.0 - c.y); }

void main()
{
    vec2 centered = (vUv - 0.5) * 2.0;
    float r = length(centered);
    if (r > 1.0)
        discard;

    // Gentle hypnotic breathing scale on the eye texture
    float pulseSpeed = mix(4.2, 11.5, clamp(u._Danger, 0.0, 1.0));
    float breath = 1.0 + 0.045 * sin(u._AgeSecs * pulseSpeed);
    vec2 eyeUv = (vUv - 0.5) / breath + 0.5;

    vec4 tex = vec4(0.0);
    if (eyeUv.x >= 0.0 && eyeUv.x <= 1.0 && eyeUv.y >= 0.0 && eyeUv.y <= 1.0)
    {
        tex = texture(_MainTex, flipV(eyeUv));
    }

    // Separate sclera luminance from jet-black concentric rings so the black rings stay crisp while sclera glows!
    float lum = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
    float isSclera = smoothstep(0.18, 0.55, lum);

    // Outer luminous hypnotic halo ring surrounding the floating eye
    float ang = atan(centered.y, centered.x);
    float ringDist = abs(r - (0.76 + 0.03 * sin(u._AgeSecs * pulseSpeed)));
    float haloRing = exp(-ringDist * ringDist * 110.0) * (0.65 + 0.35 * sin(ang * 4.0 - u._AgeSecs * 4.5));
    float softGlow = exp(-r * r * 3.8) * (0.38 + 0.20 * sin(u._AgeSecs * pulseSpeed));

    vec3 eyeRgb = mix(vec3(0.04, 0.02, 0.03), u._Color.rgb * 1.18 + u._RimColor.rgb * 0.25, isSclera);
    vec3 glowRgb = u._Color.rgb * softGlow + u._RimColor.rgb * haloRing * 0.9;

    vec3 finalRgb = mix(glowRgb, eyeRgb, clamp(tex.a * 1.15, 0.0, 1.0));
    finalRgb += u._RimColor.rgb * haloRing * 0.45;

    float alpha = clamp(max(tex.a, max(haloRing * 0.85, softGlow * 0.55)) * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (alpha < 0.01)
        discard;

    outColor = vec4(clamp(finalRgb * vColor.rgb, 0.0, 1.0), alpha);
}
