Shader "BioEden/NeutralWaterPixels" {
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" }
  Pass {
   ZTest LEqual ZWrite Off Cull Off Blend Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _BioEdenWaterPixels;
   struct v2f { float4 pos : SV_POSITION; float4 screen : TEXCOORD0; };
   v2f vert(float4 vertex : POSITION) { v2f o; o.pos=UnityObjectToClipPos(vertex); o.screen=ComputeScreenPos(o.pos); return o; }
   float4 frag(v2f i) : SV_Target {
    float3 c=tex2Dproj(_BioEdenWaterPixels, UNITY_PROJ_COORD(i.screen)).rgb;
    float g=dot(c,float3(0.2126,0.7152,0.0722));
    return float4(g,g,g,1);
   }
   ENDHLSL
  }
 }
}
