using UnityEditor;
using System.IO;
public static class BuildWaterShader {
 public static void Build() {
  var shader=AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>("Assets/NeutralWaterPixels.shader");
  if(shader==null || ShaderUtil.ShaderHasError(shader)) throw new System.Exception("Water shader compilation failed");
  Directory.CreateDirectory("Output");
  BuildPipeline.BuildAssetBundles("Output", new[]{new AssetBundleBuild {assetBundleName="neutralwater", assetNames=new[]{"Assets/NeutralWaterPixels.shader"}}},BuildAssetBundleOptions.ForceRebuildAssetBundle,BuildTarget.StandaloneWindows64);
 }
}
