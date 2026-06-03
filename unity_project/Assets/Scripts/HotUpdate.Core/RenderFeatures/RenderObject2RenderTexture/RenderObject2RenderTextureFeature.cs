using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 通用渲染功能.
/// 渲染对象到RT中,以便于后续继续使用,比如预扭曲特效等.
/// by taecg
/// </summary>
[Tooltip("渲染对象至RT中,供后续使用.")]
public class RenderObject2RenderTextureFeature : ScriptableRendererFeature
{
    class RenderObject2RenderTexturePass : ScriptableRenderPass
    {
        private readonly string m_ProfilerTag;
        private readonly ProfilingSampler m_ProfilingSampler;
        private readonly ERenderQueueType m_RenderQueueType;
        private FilteringSettings m_FilteringSettings;
        private readonly string m_RTName;
        private readonly int m_DownScale;
        private readonly bool m_IsClear;
        private readonly List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        private RTHandle m_RTTemp;

        public RenderObject2RenderTexturePass(string profilerTag, RenderPassEvent renderPassEvent, string[] shaderTags, ERenderQueueType renderQueueType, int layerMask, string rtName, int downScale, bool isClear)
        {
            m_ProfilerTag = profilerTag;
            m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
            m_RTName = rtName;
            m_DownScale = downScale;
            m_IsClear = isClear;
            this.renderPassEvent = renderPassEvent;
            this.m_RenderQueueType = renderQueueType;
            RenderQueueRange renderQueueRange = (renderQueueType == ERenderQueueType.Transparent) ? RenderQueueRange.transparent : RenderQueueRange.opaque;
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);

            if (shaderTags != null && shaderTags.Length > 0)
            {
                foreach (var passName in shaderTags)
                    m_ShaderTagIdList.Add(new ShaderTagId(passName));
            }
            else
            {
                m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
                m_ShaderTagIdList.Add(new ShaderTagId("UniversalForward"));
                m_ShaderTagIdList.Add(new ShaderTagId("UniversalForwardOnly"));
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = (int)DepthBits.None;
            desc.width /= m_DownScale;
            desc.height /= m_DownScale;
            RenderingUtils.ReAllocateIfNeeded(ref m_RTTemp, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, anisoLevel: 0, name: m_RTName);
            cmd.SetGlobalTexture(m_RTName, m_RTTemp);
            ConfigureTarget(m_RTTemp);
            if (m_IsClear) ConfigureClear(ClearFlag.All, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                SortingCriteria sortingCriteria = (m_RenderQueueType == ERenderQueueType.Transparent) ? SortingCriteria.CommonTransparent : renderingData.cameraData.defaultOpaqueSortFlags;
                DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortingCriteria);
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {

        }
        public void Dispose()
        {

            m_RTTemp?.Release();
        }

    }


    public string PassTag = "RenderObject2RT";

    public RenderPassEvent Event = RenderPassEvent.BeforeRenderingPrePasses;

    public FilterSettings FilterSetting = new FilterSettings();

    public RTSettings RTSetting = new RTSettings();

    private RenderObject2RenderTexturePass m_RenderObject2RenderTexturePass;

    public override void Create()
    {
        m_RenderObject2RenderTexturePass = new RenderObject2RenderTexturePass(PassTag, Event, FilterSetting.PassNames, FilterSetting.RenderQueueType, FilterSetting.LayerMask, RTSetting.Name, RTSetting.DownScale, RTSetting.IsClear);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_RenderObject2RenderTexturePass);
    }

    public enum ERenderQueueType
    {
        Opaque,
        Transparent,
    }
    protected override void Dispose(bool disposing)
    {
        m_RenderObject2RenderTexturePass.Dispose();
    }

    [System.Serializable]
    public class FilterSettings
    {
        public ERenderQueueType RenderQueueType = ERenderQueueType.Opaque;

        public LayerMask LayerMask = 0;

        public string[] PassNames;
    }

    [System.Serializable]
    public class RTSettings
    {
        [Tooltip("全局RT名称")] public string Name = "_RTTemp";

        [Tooltip("RT缩小系数")][Range(1, 8)] public int DownScale = 1;

        [Tooltip("绘制前是否清屏")] public bool IsClear = false;
    }
}