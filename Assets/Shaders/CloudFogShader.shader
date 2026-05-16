Shader "Custom/CloudFogShader"
{
    Properties
    {
        _MainTex ("Cloud Texture", 2D) = "white" {}
        _MaskTex ("Current Visibility Mask", 2D) = "white" {}
        _ExploredTex ("Explored Mask", 2D) = "black" {}
        _CloudColor ("Cloud Color", Color) = (0.8, 0.8, 0.8, 1)
        _ShroudColor ("Shroud Color", Color) = (0.4, 0.4, 0.4, 0.7)
        _Speed ("Speed", Float) = 0.5
        _Scale ("Scale", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            sampler2D _ExploredTex;
            fixed4 _CloudColor;
            fixed4 _ShroudColor;
            float _Speed;
            float _Scale;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float visible = tex2D(_MaskTex, i.uv).r;
                float explored = tex2D(_ExploredTex, i.uv).r;
                
                // If currently visible, it's clear (alpha 0)
                if (visible > 0.5) return fixed4(0,0,0,0);
                
                // If explored but not visible, show shroud (semi-transparent gray)
                if (explored > 0.5) return _ShroudColor;
                
                // Otherwise show full clouds
                return _CloudColor;
            }
            ENDCG
        }
    }
}
