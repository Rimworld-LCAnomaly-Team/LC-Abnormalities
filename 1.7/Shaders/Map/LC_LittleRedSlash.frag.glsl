#version 450

// F-01-57 Little Red Riding Hooded Mercenary & Crimson Scar E.G.O. Weapon
// Visceral Crimson-and-Obsidian Hooked Sickle Crescent Slash Shader
// Inspired by Lobotomy Corporation slash-half-circle-1C.png & slash-strong.png
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;     // @color @default = vec4(0.95, 0.09, 0.14, 1.0)
    vec4 _CoreColor; // @color @default = vec4(0.04, 0.01, 0.02, 1.0)
    vec4 _TipColor;  // @color @default = vec4(1.0, 0.86, 0.56, 1.0)
    float _Progress; // @default = 0.4
    float _AgeSecs;  // @default = 0.0
    float _Alpha;    // @default = 1.0
    float _ArcSpan;  // @default = 2.55
    float _Berserk;  // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

float hash21(vec2 p)
{
    p = fract(p * vec2(234.34, 435.345));
    p += dot(p, p + 34.23);
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
    vec2 p = (vUv - 0.5) * 2.0; // -1..+1
    float r = length(p);
    if (r > 1.0 || r < 0.12)
        discard;

    float ang = atan(p.y, p.x);
    float halfSpan = max(0.75, u._ArcSpan * 0.5);
    float normAng = (ang / halfSpan) * 0.5 + 0.5; // 0 = swing start, 1 = swing end
    if (normAng < -0.06 || normAng > 1.06)
        discard;

    float prog = clamp(u._Progress, 0.0, 1.0);
    // Razor-fast whipping sweep across the crescent
    float headPos = clamp(prog * 1.75, 0.0, 1.25);
    float tailPos = clamp((prog - 0.18) * 1.42, 0.0, 1.12);

    float headMask = smoothstep(headPos + 0.06, headPos - 0.04, normAng);
    float tailMask = smoothstep(tailPos - 0.07, tailPos + 0.22, normAng);
    float sweepMask = headMask * tailMask;
    if (sweepMask < 0.01)
        discard;

    float endTaper = sin(clamp(normAng, 0.0, 1.0) * 3.14159265);
    float berserk = clamp(u._Berserk, 0.0, 1.0);
    float bladeWidth = mix(0.035, 0.24 + 0.08 * berserk, pow(endTaper, 0.60));

    float targetR = 0.79 - 0.05 * (1.0 - endTaper);
    float distInward = targetR - r; // positive = trailing inward behind sickle edge

    // Jagged blood-splatter & razor scratch noise along the arc
    float n1 = valueNoise(vec2(normAng * 12.0 - u._AgeSecs * 16.0, r * 14.0));
    float n2 = valueNoise(vec2(normAng * 26.0 - u._AgeSecs * 24.0, r * 24.0 + 4.1));
    float scratch = 0.52 + 0.48 * (n1 * 0.6 + n2 * 0.4);

    // 1. Razor-sharp obsidian-black inner gash (signature Lobotomy Corp black-core red slash!)
    float blackRidge = exp(-pow((r - (targetR - 0.032)) / (0.040 * (0.35 + 0.65 * endTaper)), 2.0));
    // 2. Blazing scarlet outer edge framing the black gash
    float outerCrimson = exp(-pow((r - (targetR + 0.025)) / (0.052 * (0.45 + 0.55 * endTaper)), 2.0)) * (0.75 + 0.35 * n1);
    // 3. Trailing serrated blood-ribbon + secondary inner laceration lines
    float trailVeil = smoothstep(-0.04, 0.02, distInward)
                    * smoothstep(bladeWidth * (1.05 + 0.40 * n1), 0.0, distInward)
                    * scratch;
    float secondLaceration = exp(-pow((r - (targetR - 0.135)) / (0.020 + 0.012 * berserk), 2.0))
                           * smoothstep(0.25, 0.75, n2) * endTaper;
    // 4. White-gold scorching spark glint right at the swinging sickle tip
    float tipGlint = exp(-pow((normAng - min(headPos, 0.96)) * 11.0, 2.0))
                   * exp(-pow((r - targetR) / 0.075, 2.0))
                   * (1.0 - prog * 0.85);

    // Flying crimson blood droplets shed off the outer arc
    float sparkNoise = valueNoise(vec2(normAng * 32.0 - prog * 8.0, r * 28.0));
    float droplets = smoothstep(0.76, 0.92, sparkNoise)
                   * smoothstep(0.98, 0.84, r) * smoothstep(0.72, 0.83, r)
                   * endTaper * (0.65 + 0.65 * berserk);

    float fade = pow(1.0 - prog, 0.72) * u._Alpha;
    float totalWeight = clamp(outerCrimson * 1.15 + blackRidge * 1.25 + trailVeil * 0.92 + secondLaceration * 0.85 + tipGlint * 1.1 + droplets, 0.0, 1.0);
    float outA = clamp(totalWeight * sweepMask * fade * u._Color.a * vColor.a, 0.0, 1.0);
    if (outA < 0.01)
        discard;

    // Compose visceral dark-cored crimson color: black gash center, blazing crimson rim, white-gold tip glint
    vec3 crimsonCol = u._Color.rgb * (1.15 + 0.35 * berserk);
    vec3 col = crimsonCol * (outerCrimson * 1.25 + trailVeil * 0.95 + secondLaceration * 1.05 + droplets * 1.25);
    // Darken the interior blade spine sharply toward obsidian black (exact match to slash-half-circle-1C.png!)
    float darkFactor = clamp(blackRidge * (0.88 - 0.18 * berserk), 0.0, 0.92);
    col = mix(col, u._CoreColor.rgb, darkFactor);
    // Add scorching tip glint & berserk crimson-gold highlight on top
    col += u._TipColor.rgb * (tipGlint * (1.35 + 0.45 * berserk) + outerCrimson * berserk * 0.28);
    col *= vColor.rgb;

    outColor = vec4(clamp(col, 0.0, 1.0), outA);
}
