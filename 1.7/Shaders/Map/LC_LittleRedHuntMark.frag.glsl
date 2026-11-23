#version 450

// F-01-57 Little Red Riding Hooded Mercenary — Hunter's Prey Mark Shader
// Samples unpacked eye_hunt_noeye.png with pulsing blood-red lock-on ring and rotating hunter ticks
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(1.0, 0.16, 0.16, 1.0)
    vec4 _GoldColor; // @color @default = vec4(1.0, 0.84, 0.28, 1.0)
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _Pulse;    // @default = 1.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

vec2 flipV(vec2 c) { return vec2(c.x, 1.0 - c.y); }

void main()
{
    vec2 p = (vUv - 0.5) * 2.0; // -1..+1
    float r = length(p);
    if (r > 1.0)
        discard;

    // Slight breathing zoom on the iconic Hunter's Eye reticle texture
    float breathe = 1.0 + 0.045 * sin(u._AgeSecs * 6.5);
    vec2 eyeUv = (vUv - 0.5) / (0.76 * breathe) + 0.5;
    vec4 eyeTex = vec4(0.0);
    if (eyeUv.x >= 0.0 && eyeUv.x <= 1.0 && eyeUv.y >= 0.0 && eyeUv.y <= 1.0)
    {
        eyeTex = texture(_MainTex, flipV(eyeUv));
    }

    // Outer rotating crimson hunter crosshair ring
    float ang = atan(p.y, p.x);
    float rotAng = ang + u._AgeSecs * 1.4;
    float outerRing = exp(-pow((r - 0.82) / 0.035, 2.0));
    float ticks = smoothstep(0.86, 0.97, abs(cos(rotAng * 2.0))) * smoothstep(0.68, 0.78, r) * smoothstep(0.98, 0.88, r);

    // Repeating inward contracting lock-on pulse wave
    float lockWave = fract(-u._AgeSecs * 0.85);
    float lockR = mix(0.35, 0.96, lockWave);
    float lockRing = exp(-pow((r - lockR) / 0.028, 2.0)) * (1.0 - lockWave) * 0.85;

    // Soft crimson threat aura behind the eye
    float aura = exp(-r * r * 3.8) * (0.38 + 0.14 * sin(u._AgeSecs * 8.0));

    float ringAlpha = clamp((outerRing * 0.78 + ticks * 0.92 + lockRing + aura * 0.65) * u._Pulse, 0.0, 1.0);
    vec3 ringCol = u._Color.rgb * (outerRing + ticks * 1.1 + lockRing * 1.2 + aura * 0.8)
                 + u._GoldColor.rgb * (ticks * 0.35 + lockRing * 0.25);

    // Composite authentic eye_hunt_noeye.png crisply over the pulsing crimson ring
    vec3 eyeCol = eyeTex.rgb * (1.05 + 0.22 * sin(u._AgeSecs * 7.5)) + u._Color.rgb * (0.12 * eyeTex.a);
    float outA = clamp((eyeTex.a + ringAlpha * (1.0 - eyeTex.a * 0.75)) * u._Alpha * u._Color.a * vColor.a, 0.0, 1.0);
    if (outA < 0.01)
        discard;

    vec3 col = mix(ringCol, eyeCol, clamp(eyeTex.a * 1.15, 0.0, 1.0)) * vColor.rgb;
    outColor = vec4(clamp(col, 0.0, 1.0), outA);
}
