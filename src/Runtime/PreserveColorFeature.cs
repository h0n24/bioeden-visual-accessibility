#pragma warning disable CS0618, CS0672
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BioEden.NoDOF
{
    // The game's AdjustImage pass desaturates the whole world. FilterController
    // caches selected renderers without changing their gameplay layers; this pass
    // redraws player structures, minerals and polluted water without touching
    // natural terrain or clean water.
    public sealed class PreserveColorFeature : ScriptableRendererFeature
    {
        private PreserveColorPass pass;

        private MineralGroundPass mineralGround;
        public override void Create()
        {
            mineralGround = new MineralGroundPass();
            pass = new PreserveColorPass(mineralGround);
        }
        protected override void Dispose(bool disposing) { pass?.Dispose(); mineralGround?.Dispose(); }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (FilterController.IsEnabled && renderingData.cameraData.cameraType == CameraType.Game)
            {
                if (mineralGround.Prepare(renderingData.cameraData.camera))
                {
                    renderer.EnqueuePass(mineralGround.Capture);
                    renderer.EnqueuePass(mineralGround);
                }
                renderer.EnqueuePass(pass);
            }
        }

        private sealed class PreserveColorPass : ScriptableRenderPass
        {
            private FilteringSettings filtering = new FilteringSettings(RenderQueueRange.all, -1, FilterController.PreserveRenderingMask);
            private Material neutralPixels;
            private bool shaderLoadAttempted;
            private static readonly int WaterPixels = Shader.PropertyToID("_BioEdenWaterPixels");

            public void Dispose() { if (neutralPixels != null) Object.Destroy(neutralPixels); }

            private bool EnsureNeutralShader()
            {
                if (shaderLoadAttempted) return neutralPixels != null;
                shaderLoadAttempted = true;
                try
                {
                    using (var stream = typeof(PreserveColorFeature).Assembly.GetManifestResourceStream("BioEden.NeutralWater"))
                    {
                        if (stream == null) throw new System.InvalidOperationException("Missing neutral water bundle");
                        var bytes = new byte[stream.Length];
                        int count = 0;
                        while (count < bytes.Length)
                        {
                            int read = stream.Read(bytes, count, bytes.Length - count);
                            if (read == 0) throw new System.IO.EndOfStreamException();
                            count += read;
                        }
                        var bundle = AssetBundle.LoadFromMemory(bytes);
                        if (bundle == null) throw new System.InvalidOperationException("Cannot load neutral water bundle");
                        try
                        {
                            var shader = bundle.LoadAsset<Shader>("Assets/NeutralWaterPixels.shader");
                            if (shader == null || !shader.isSupported) throw new System.InvalidOperationException("Unsupported neutral water shader");
                            neutralPixels = new Material(shader);
                        }
                        finally { bundle.Unload(false); }
                    }
                }
                catch (System.Exception e) { Debug.LogError("[BioEden.NoDOF] " + e.Message); }
                return neutralPixels != null;
            }

            private readonly MineralGroundPass mineralGround;
            public PreserveColorPass(MineralGroundPass ground)
            {
                mineralGround = ground;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                // Selected objects retain their original physics and camera layers.
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // Remove late lighting/grading tint before restoring selected colors.
                if (EnsureNeutralShader())
                {
                    var neutral = CommandBufferPool.Get("BioEden neutral background");
                    try
                    {
                        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                        descriptor.depthBufferBits = 0; descriptor.msaaSamples = 1;
                        int snapshot = Shader.PropertyToID("_BioEdenBackgroundPixels");
                        var renderer = renderingData.cameraData.renderer;
                        var color = renderer.cameraColorTargetHandle.nameID;
                        neutral.GetTemporaryRT(snapshot, descriptor, FilterMode.Point);
                        neutral.Blit(color, snapshot);
                        // Bind explicitly: the multi-pass material must sample
                        // this frame's copy, never the fallback _MainTex texture.
                        neutral.SetGlobalTexture("_BioEdenBackgroundPixels", new RenderTargetIdentifier(snapshot));
                        neutral.Blit(new RenderTargetIdentifier(snapshot), color, neutralPixels, 2);
                        // MineralGround's mask restores the final pre-neutralization
                        // pixels, preserving lighting and the exact discovered cell.
                        neutral.SetGlobalTexture("_BioEdenMineralPixels", new RenderTargetIdentifier(snapshot));
                        neutral.SetRenderTarget(color, renderer.cameraDepthTargetHandle.nameID);
                        context.ExecuteCommandBuffer(neutral);
                        neutral.Clear();
                        mineralGround.RestoreAfterNeutralization(context, ref renderingData);
                        neutral.ReleaseTemporaryRT(snapshot);
                        neutral.SetRenderTarget(color, renderer.cameraDepthTargetHandle.nameID);
                        context.ExecuteCommandBuffer(neutral);
                    }
                    finally { CommandBufferPool.Release(neutral); }
                }
                // Native culling, sorting and SRP batching replace per-object commands.
                var drawing = CreateDrawingSettings(new ShaderTagId("UniversalForward"), ref renderingData, SortingCriteria.CommonOpaque);
                drawing.SetShaderPassName(1, new ShaderTagId("UniversalForwardOnly"));
                drawing.SetShaderPassName(2, new ShaderTagId("SRPDefaultUnlit"));
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
                var commands = CommandBufferPool.Get("BioEden neutral clean water");
                try
                {
                    FilterController.DrawCleanWater(commands);
                    if (FilterController.NeedsLakeNeutralization && EnsureNeutralShader())
                    {
                        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                        descriptor.depthBufferBits = 0;
                        descriptor.msaaSamples = 1;
                        commands.GetTemporaryRT(WaterPixels, descriptor, FilterMode.Point);
                        var renderer = renderingData.cameraData.renderer;
                        var color = renderer.cameraColorTargetHandle.nameID;
                        var depth = renderer.cameraDepthTargetHandle.nameID;
                        commands.Blit(color, WaterPixels);
                        commands.SetRenderTarget(color, depth);
                        commands.SetGlobalTexture(WaterPixels, new RenderTargetIdentifier(WaterPixels));
                        FilterController.DrawCleanLakeMask(commands, neutralPixels);
                        commands.ReleaseTemporaryRT(WaterPixels);
                    }
                    context.ExecuteCommandBuffer(commands);
                }
                finally { CommandBufferPool.Release(commands); }
            }
        }
    }
}
#pragma warning restore CS0618, CS0672
