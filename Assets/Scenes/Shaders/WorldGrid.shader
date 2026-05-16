Shader "Custom/WorldGrid"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0.5, 0.5, 0.5, 1)
        _BackgroundColor ("Background Color", Color) = (0.1, 0.1, 0.1, 1)
        _GridSpacing ("Grid Spacing", Float) = 1.0
        _LineThickness ("Line Thickness", Float) = 0.05
        _Fade ("Fade", Range(0, 1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 worldPos : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            fixed4 _GridColor;
            fixed4 _BackgroundColor;
            float _GridSpacing;
            float _LineThickness;

            float _Fade;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Get world position for grid calculation
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = worldPos.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Shift by 0.5 to make integer coordinates the centers of cells, 
                // and the lines the boundaries.
                float2 pos = i.worldPos / _GridSpacing;
                float2 grid = abs(frac(pos) - 0.5) / fwidth(pos);
                float lineVal = min(grid.x, grid.y);
                
                // Slightly thicker lines for better visibility
                float alpha = (1.0 - smoothstep(0.0, _LineThickness * 30.0, lineVal)) * _Fade;
                
                return lerp(_BackgroundColor, _GridColor, alpha);
            }
            ENDCG
        }
    }
}
