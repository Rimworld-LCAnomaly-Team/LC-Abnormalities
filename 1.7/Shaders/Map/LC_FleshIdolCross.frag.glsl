#version 450

// Flesh Idol (T-09-79) Living Cross Shader:
// Applies localized organic writhing & pulsating glow to the central flesh lump while keeping the iron cross rigid.
// _Phase: 0=Idle, 1=Warmup(0-19s), 2=Miracle(20-80s), 3=Overload(80-90s), 4=Charred Cooldown
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color;      // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _GlowColor;  // @color @default = vec4(1.0, 0.82, 0.42, 1.0)
    float _AgeSecs;   // @default = 0.0
    float _Phase;     // @default = 0.0
    float _PulseAmt;  // @default = 0.0
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex; // @default = white

void main()
{
    // Central flesh lump sits near the cross junction around uv (0.50, 0.68).
    vec2 fleshCenter = vec2(0.50, 0.67);
    vec2 d = (vUv - fleshCenter) * vec2(1.95, 1.75);
    float dist = length(d);
    float fleshMask = smoothstep(0.52, 0.14, dist);

    // Organic writhing & rhythmic heartbeat expansion scaled by prayer phase.
    float speed = 2.0;
    float amp = 0.0045;
    if (u._Phase >= 0.5 && u._Phase < 1.5)
    {
        speed = 4.2;
        amp = 0.010;
    }
    else if (u._Phase >= 1.5 && u._Phase < 2.5)
    {
        speed = 5.4;
        amp = 0.012 + u._PulseAmt * 0.010;
    }
    else if (u._Phase >= 2.5 && u._Phase < 3.5)
    {
        speed = 13.5;
        amp = 0.020;
    }
    else if (u._Phase >= 3.5)
    {
        speed = 0.8;
        amp = 0.001;
    }

    float beat = pow(max(0.0, sin(u._AgeSecs * speed)), 2.0);
    float writheX = sin(vUv.y * 28.0 + u._AgeSecs * speed * 1.15) * amp;
    float writheY = cos(vUv.x * 26.0 - u._AgeSecs * speed * 0.95) * amp;
    vec2 radialPush = -normalize(d + vec2(0.0001, 0.0001)) * (beat * amp * 1.35);

    vec2 sampleUv = vUv + (vec2(writheX, writheY) + radialPush) * fleshMask;
    vec4 tex = texture(_MainTex, clamp(sampleUv, vec2(0.0), vec2(1.0)));
    if (tex.a < 0.05)
        discard;

    vec3 rgb = tex.rgb;
    // Detect reddish flesh pixels vs dark iron cross.
    float redness = clamp((rgb.r - max(rgb.g, rgb.b)) * 2.8, 0.0, 1.0) * fleshMask;

    if (u._Phase >= 0.5 && u._Phase < 1.5)
    {
        // Warmup: throbbing arterial crimson sheen on the flesh mass
        rgb += vec3(0.42, 0.06, 0.04) * redness * (0.35 + 0.65 * beat);
    }
    else if (u._Phase >= 1.5 && u._Phase < 2.5)
    {
        // Miracle: eerie sacred golden-rose luminance radiating through the flesh and veins
        float sheen = (0.35 + 0.45 * beat + 0.45 * u._PulseAmt);
        rgb = mix(rgb, rgb * vec3(1.18, 1.08, 0.88) + u._GlowColor.rgb * 0.42, fleshMask * sheen * 0.72);
        rgb += u._GlowColor.rgb * redness * (0.38 + 0.42 * u._PulseAmt);
    }
    else if (u._Phase >= 2.5 && u._Phase < 3.5)
    {
        // Overload: violent crimson-white strobe veins & scorched edges
        float strobe = 0.5 + 0.5 * sin(u._AgeSecs * 22.0);
        rgb += vec3(0.85, 0.14, 0.10) * redness * (0.55 + 0.45 * strobe);
        rgb += vec3(1.0, 0.82, 0.76) * fleshMask * pow(strobe, 3.0) * 0.48;
    }
    else if (u._Phase >= 3.5)
    {
        // Post-explosion charred dormancy
        float gray = dot(rgb, vec3(0.299, 0.587, 0.114));
        rgb = mix(rgb, vec3(gray * 0.48), 0.78);
    }
    else
    {
        // Idle: faint living pulse in the flesh
        rgb += vec3(0.16, 0.02, 0.02) * redness * beat;
    }

    vec4 finalCol = vec4(clamp(rgb, 0.0, 1.0), tex.a) * u._Color * vColor;
    outColor = finalCol;
}
