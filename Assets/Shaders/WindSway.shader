Shader "Custom/WindSway"
{
    // Sprite shader that bends the sprite like a tree in the wind: base (uv.y = 0) pinned, top
    // teeters most. The ambient sway is NOT a constant wave — each instance leans once every
    // ~6-10s with a slow ~4s out-and-back, randomized per instance via _SwaySeed.
    //
    // Time comes from the global _WindTime (set to UNSCALED time by WindController), so changing
    // game speed does not change how fast plants sway. Overall amplitude is scaled by the global
    // _GlobalWindMul (ambient breeze + storm boost). A separate _RustleAmount adds a quick wobble
    // when an agent brushes the crop.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _WindStrength ("Wind Strength (world units at top)", Range(0,0.5)) = 0.06
        _SwayBend ("Bend Exponent (top-heaviness)", Range(1,5)) = 2.6
        _SwayMinPeriod ("Sway Min Period (s)", Range(1,20)) = 6
        _SwayMaxPeriod ("Sway Max Period (s)", Range(1,20)) = 10
        _SwayDuration ("Single Sway Duration (s)", Range(0.5,8)) = 4

        // Per-instance (driven via MaterialPropertyBlock):
        _SwaySeed ("Sway Seed (0..1)", Range(0,1)) = 0.5
        _SwayEnable ("Sway Enable (0/1)", Float) = 1
        _RustleAmount ("Rustle Amount (0..1)", Float) = 0
        _RustleStrength ("Rustle Strength", Range(0,0.6)) = 0.12
        _RustleSpeed ("Rustle Speed", Range(0,40)) = 20

        // Per-instance sway shaping (driven via MaterialPropertyBlock — e.g. WindSeed on trees):
        _SwayStrengthMul ("Sway Strength Mul", Float) = 1
        _SwayPeriodMul ("Sway Period Mul", Float) = 1

        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            #define WIND_PI 3.14159265

            sampler2D _MainTex;
            fixed4 _Color;
            float _WindStrength;
            float _SwayBend;
            float _SwayMinPeriod;
            float _SwayMaxPeriod;
            float _SwayDuration;
            float _SwaySeed;
            float _SwayEnable;
            float _RustleAmount;
            float _RustleStrength;
            float _RustleSpeed;
            float _SwayStrengthMul;
            float _SwayPeriodMul;
            float _GlobalWindMul; // amplitude (WindController: ambient + storm)
            float _WindTime;      // UNSCALED seconds (WindController) — game-speed agnostic

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            v2f vert(appdata IN)
            {
                v2f OUT;

                // Height factor: 0 at the bottom, 1 at the top; bend exponent keeps the base planted
                // and concentrates the teeter near the top.
                float h    = saturate(IN.uv.y);
                float bend = pow(h, _SwayBend);

                float gw    = max(_GlobalWindMul, 0.0);
                float seed  = _SwaySeed;
                float seed2 = frac(seed * 91.7 + 0.13);

                // Each instance leans once per (random) period, for _SwayDuration, then rests.
                // _SwayPeriodMul (per-renderer) lets trees lean slower than crops.
                float period   = lerp(_SwayMinPeriod, _SwayMaxPeriod, seed) * max(_SwayPeriodMul, 0.01);
                float phase    = seed2 * period;
                float cyclePos = frac((_WindTime + phase) / max(period, 0.01));
                float window   = saturate(_SwayDuration / max(period, 0.01));

                float pulse = 0.0;
                if (cyclePos < window)
                {
                    float local = cyclePos / max(window, 0.0001);
                    pulse = sin(local * WIND_PI); // 0 -> 1 -> 0, a single slow out-and-back
                }
                float dir  = (seed2 < 0.5) ? -1.0 : 1.0;
                float sway = dir * pulse;

                // _SwayStrengthMul (per-renderer) lets trees bend further than crops.
                float windDx = _WindStrength * sway * gw * _SwayEnable * _SwayStrengthMul;

                // Rustle: a quick wobble that spikes and decays when something brushes the crop.
                float rustle = sin(_WindTime * _RustleSpeed + seed * 30.0) * _RustleStrength * saturate(_RustleAmount);

                float dx = (windDx + rustle) * bend;
                IN.vertex.x += dx; // object-space horizontal shift

                OUT.pos   = UnityObjectToClipPos(IN.vertex);
                OUT.uv    = IN.uv;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.pos = UnityPixelSnap(OUT.pos);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.uv) * IN.color;
                c.rgb *= c.a; // premultiplied alpha, matches Sprites/Default
                return c;
            }
        ENDCG
        }
    }
}
