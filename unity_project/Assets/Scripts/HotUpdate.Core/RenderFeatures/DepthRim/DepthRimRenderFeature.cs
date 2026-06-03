using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DepthRimRenderFeature : ScriptableRendererFeature
{
    class DepthRimPass : ScriptableRenderPass
    {
        private Material m_Material;
        private readonly string m_ProfilerTag;
        private readonly ProfilingSampler m_ProfilingSampler;
        private RTHandle dest;
        private DepthRimVolum post;
        private readonly int m_RimRange = Shader.PropertyToID("_RimRange");
        private readonly int m_RimColor = Shader.PropertyToID("_RimColor");

        public DepthRimPass(Material material, RenderPassEvent passEvent)
        {
            m_ProfilerTag = "DepthRimPost";
            m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
            renderPassEvent = passEvent;
            m_Material = material;
        }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = (int)DepthBits.None;
            RenderingUtils.ReAllocateIfNeeded(ref dest, desc, name: m_ProfilerTag);
            ConfigureInput(ScriptableRenderPassInput.Normal);
        }

       
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        { 
            if (m_Material == null)
            {
                Debug.LogError("深度边缘光材质球为空!");
                return;
            }
            if (!renderingData.cameraData.postProcessEnabled) return;
            var stack = VolumeManager.instance.stack;
            post = stack.GetComponent<DepthRimVolum>();
            if (post.Bool.value == false)
            {
                return;
            }
            m_Material.SetFloat(m_RimRange, post.RimRange.value);
            m_Material.SetColor(m_RimColor, post.RimColor.value);
            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                Blit(cmd, source, dest, m_Material, 0);
                Blit(cmd, dest, source);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public void Dispose()
        {

            dest?.Release();
        }
    }

     public RenderPassEvent PassEvent;
     DepthRimPass m_DepthRimPass;
    public Material Material;

    /// <inheritdoc/>
    public override void Create()
    {
        m_DepthRimPass = new DepthRimPass(Material, PassEvent);

        // Configures where the render pass should be injected.
        m_DepthRimPass.renderPassEvent = PassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_DepthRimPass);
    }
    protected override void Dispose(bool disposing)
    {
        // CoreUtils.Destroy(Material);
        m_DepthRimPass.Dispose();
    }
}


