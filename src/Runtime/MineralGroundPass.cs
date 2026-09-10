#pragma warning disable CS0618, CS0672
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BioEden.NoDOF
{
    // Resource ground is a DBuffer decal, not a Renderer. Preserve the already
    // shaded pixels within its projection volume around the game's event-300
    // saturation pass, before later transparencies and post-processing.
    internal sealed class MineralGroundPass : ScriptableRenderPass
    {
        private static readonly int Pixels = Shader.PropertyToID("_BioEdenMineralPixels");
        private readonly List<FilterController.MineralGroundEntry> entries = new List<FilterController.MineralGroundEntry>();
        private readonly List<Matrix4x4> volumes = new List<Matrix4x4>();
        private readonly Plane[] planes = new Plane[6];
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private Mesh cube;
        private Material material;
        private bool attempted;
        public readonly ScriptableRenderPass Capture;

        internal MineralGroundPass()
        {
            renderPassEvent = (RenderPassEvent)301;
            ConfigureInput(ScriptableRenderPassInput.Depth);
            Capture = new CapturePass();
        }

        internal bool Prepare(Camera camera)
        {
            volumes.Clear();
            entries.Clear();
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            foreach (var entry in FilterController.MineralGround)
            {
                if (!entry.IsExplored) continue;
                var decal = entry.Decal;
                if (decal == null || !decal.isActiveAndEnabled || decal.fadeFactor <= 0) continue;
                if ((camera.cullingMask & (1 << decal.gameObject.layer)) == 0) continue;
                var t = decal.transform;
                if (Vector3.Distance(camera.transform.position, t.position) > decal.drawDistance) continue;
                var scale = decal.scaleMode == DecalScaleMode.InheritFromHierarchy ? t.lossyScale : Vector3.one;
                var matrix = Matrix4x4.TRS(t.position, t.rotation, scale) * Matrix4x4.TRS(decal.pivot, Quaternion.identity, decal.size);
                var bounds = new Bounds(matrix.MultiplyPoint3x4(Vector3.zero), Vector3.zero);
                for (int i = 0; i < 8; i++) bounds.Encapsulate(matrix.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f)));
                if (GeometryUtility.TestPlanesAABB(planes, bounds)) { volumes.Add(matrix); entries.Add(entry); }
            }
            if (volumes.Count == 0) return false;
            if (!attempted)
            {
                attempted = true;
                try
                {
                    using (var stream = typeof(MineralGroundPass).Assembly.GetManifestResourceStream("BioEden.NeutralWater"))
                    using (var memory = new System.IO.MemoryStream())
                    {
                        stream.CopyTo(memory);
                        var bundle = AssetBundle.LoadFromMemory(memory.ToArray());
                        try
                        {
                            var shader = bundle.LoadAsset<Shader>("Assets/NeutralWaterPixels.shader");
                            if (shader == null || !shader.isSupported || shader.passCount < 2) throw new System.Exception("Mineral ground shader unavailable");
                            material = new Material(shader);
                        }
                        finally { if (bundle != null) bundle.Unload(false); }
                    }
                    cube = new Mesh { name = "BioEden mineral projection volume" };
                    cube.vertices = new[] { new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f) };
                    cube.triangles = new[] { 0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5 };
                    cube.RecalculateBounds();
                }
                catch (System.Exception e) { Debug.LogError("[BioEden.NoDOF] Mineral ground: " + e.Message); }
            }
            return material != null && cube != null;
        }

        internal void RestoreAfterNeutralization(ScriptableRenderContext context, ref RenderingData data)
        {
            if (material != null && volumes.Count > 0) Draw(context, ref data, false);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData data) => Draw(context, ref data, true);
        private void Draw(ScriptableRenderContext context, ref RenderingData data, bool early)
        {
            var cmd = CommandBufferPool.Get("BioEden restore mineral ground");
            try
            {
                var camera = data.cameraData.camera;
                var vp = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
                cmd.SetGlobalMatrix("_BioEdenMineralInverseVP", vp.inverse);
                if (early) cmd.SetGlobalTexture(Pixels, new RenderTargetIdentifier(Pixels));
                cmd.SetRenderTarget(data.cameraData.renderer.cameraColorTargetHandle.nameID);
                for (int index = 0; index < volumes.Count; index++)
                {
                    var volume = volumes[index];
                    var entry = entries[index];
                    properties.SetVector("_BioEdenHexCenter", entry.HexCenter);
                    properties.SetVector("_BioEdenHexEdge0", entry.HexEdge0);
                    properties.SetVector("_BioEdenHexEdge1", entry.HexEdge1);
                    properties.SetVector("_BioEdenHexEdge2", entry.HexEdge2);
                    properties.SetMatrix("_BioEdenMineralWorldToLocal", volume.inverse);
                    cmd.DrawMesh(cube, volume, material, 0, 1, properties);
                }
                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        public override void OnCameraCleanup(CommandBuffer cmd) => cmd.ReleaseTemporaryRT(Pixels);
        public void Dispose() { if (material != null) Object.Destroy(material); if (cube != null) Object.Destroy(cube); }

        private sealed class CapturePass : ScriptableRenderPass
        {
            public CapturePass() { renderPassEvent = (RenderPassEvent)299; }
            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                var cmd = CommandBufferPool.Get("BioEden capture mineral ground");
                try
                {
                    var descriptor = data.cameraData.cameraTargetDescriptor;
                    descriptor.depthBufferBits = 0;
                    descriptor.msaaSamples = 1;
                    cmd.GetTemporaryRT(Pixels, descriptor, FilterMode.Point);
                    cmd.Blit(data.cameraData.renderer.cameraColorTargetHandle.nameID, Pixels);
                    context.ExecuteCommandBuffer(cmd);
                }
                finally { CommandBufferPool.Release(cmd); }
            }
        }
    }
}
#pragma warning restore CS0618, CS0672
