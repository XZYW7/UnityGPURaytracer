Shader "Hidden/AddSample"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
    }
    SubShader
    {
        pass{
            Cull Off 
            ZWrite Off 
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma fragment frag
            #pragma vertex vertex
            sampler2D _MainTex;
            struct a2v
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            float _samples;
            v2f vertex(a2v i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.uv = i.uv.xy;
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                return float4(tex2D(_MainTex, i.uv).rgb, 1.0f / (_samples + 1.0f));
            }
            ENDCG
        }

    }
}
