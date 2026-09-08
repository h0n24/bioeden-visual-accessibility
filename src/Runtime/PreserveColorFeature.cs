#pragma warning disable CS0618, CS0672
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BioEden.NoDOF
{
    // The game's AdjustImage pass desaturates the whole world. FilterController
    // temporarily moves only selected renderers to PreserveLayer, so this pass
    // redraws player structures, minerals and polluted water without touching
    // natural terrain or clean water.
    public sealed class PreserveColorFeature : ScriptableRendererFeature
    {
        private PreserveColorPass pass;

        public override void Create() => pass = new PreserveColorPass();
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (FilterController.IsEnabled && renderingData.cameraData.cameraType == CameraType.Game)
                renderer.EnqueuePass(pass);
        }

        private sealed class PreserveColorPass : ScriptableRenderPass
        {
            private FilteringSettings filtering;
            private readonly List<ShaderTagId> shaderTags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            public PreserveColorPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                // Selected structures and water can use transparent or cutout materials.
                // The layer mask is exclusive, so including all queues cannot affect the
                // rest of the scene and keeps those objects visible in the color pass.
                filtering = new FilteringSettings(RenderQueueRange.all, 1 << FilterController.PreserveLayer);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var drawing = CreateDrawingSettings(shaderTags, ref renderingData, SortingCriteria.CommonOpaque);
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
            }
        }
    }
}
#pragma warning restore CS0618, CS0672
