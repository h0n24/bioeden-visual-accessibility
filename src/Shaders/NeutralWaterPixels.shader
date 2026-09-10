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
  Pass {
   Name "PreserveMineralGround"
   ZTest Always ZWrite Off Cull Front Blend Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _BioEdenMineralPixels;
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   float4x4 _BioEdenMineralInverseVP;
   float4x4 _BioEdenMineralWorldToLocal;
   float4 _BioEdenHexCenter, _BioEdenHexEdge0, _BioEdenHexEdge1, _BioEdenHexEdge2;
   struct v2f { float4 pos : SV_POSITION; float4 screen : TEXCOORD0; };
   v2f vert(float4 vertex : POSITION) { v2f o; o.pos=UnityObjectToClipPos(vertex); o.screen=ComputeScreenPos(o.pos); return o; }
   float4 frag(v2f i) : SV_Target {
    float2 uv=i.screen.xy/i.screen.w;
    float depth=SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv);
    #if !defined(UNITY_REVERSED_Z)
     depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
    #endif
    float2 ndc=uv*2-1;
    ndc.y *= _ProjectionParams.x;
    float4 world=mul(_BioEdenMineralInverseVP,float4(ndc,depth,1));
    world/=world.w;
    float3 local=mul(_BioEdenMineralWorldToLocal,world).xyz;
    clip(0.5-abs(local));
    // Clip to the actual map cell using its three pairs of neighbor bisectors.
    float2 offset=world.xz-_BioEdenHexCenter.xy;
    clip(_BioEdenHexEdge0.z-abs(dot(offset,_BioEdenHexEdge0.xy)));
    clip(_BioEdenHexEdge1.z-abs(dot(offset,_BioEdenHexEdge1.xy)));
    clip(_BioEdenHexEdge2.z-abs(dot(offset,_BioEdenHexEdge2.xy)));
    return tex2D(_BioEdenMineralPixels,uv);
   }
   ENDHLSL
  }
  Pass {
   Name "NeutralBackground"
   ZTest Always ZWrite Off Cull Off Blend Off
   HLSLPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _BioEdenBackgroundPixels;
   float4 frag(v2f_img i) : SV_Target {
    float4 c=tex2D(_BioEdenBackgroundPixels,i.uv);
    float gray=dot(c.rgb,float3(0.2126,0.7152,0.0722));
    return float4(lerp(c.rgb,gray.xxx,0.8),c.a);
   }
   ENDHLSL
  }
 }
}
