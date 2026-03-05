Shader "Unlit/CircleShader"
{
    Properties
    {
        // 主纹理
        _MainTex ("Texture", 2D) = "white" {}
        // 主颜色，默认为白色
        _Color ("Color", Color) = (1,1,1,1)
        // 圆的半径，范围0到0.5，因为纹理坐标范围是0到1，中心在0.5
        _Radius ("Radius", Range(0.0, 0.5)) = 0.4
        // 边缘模糊程度
        _Smoothness ("Edge Smoothness", Range(0.0, 0.1)) = 0.01
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha // 开启透明度混合
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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Radius;
            float _Smoothness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // 传递UV坐标，范围是[0,1]
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 采样原始纹理
                fixed4 texColor = tex2D(_MainTex, i.uv);

                // 计算当前像素到中心点(0.5, 0.5)的距离
                float dist = distance(i.uv, float2(0.5, 0.5));

                // 使用smoothstep根据距离和半径进行平滑裁剪，产生抗锯齿效果
                // 当dist < (_Radius - _Smoothness)时，alpha为1
                // 当dist > (_Radius + _Smoothness)时，alpha为0
                // 中间部分平滑过渡
                float alpha = 1 - smoothstep(_Radius - _Smoothness, _Radius + _Smoothness, dist);

                // 返回纹理颜色，透明度由计算出的alpha值决定
                return fixed4(texColor.rgb * _Color.rgb, texColor.a * _Color.a * alpha);
            }
            ENDCG
        }
    }
}