/// <summary>
/// UI面板下的背景模糊，包含下面的UI元素(引用Blur文件夹中的KawaseBlur Shader)
/// by taecg
/// </summary>
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Collections.Generic;

public class BlurUIRenderPassFeature : ScriptableRendererFeature
{
    private static BlurUIRenderPassFeature instance;
    public static BlurUIRenderPassFeature Instance
    {
        get
        {
            return instance;
        }
    }

    class BlurUIRenderPass : ScriptableRenderPass
    {
        private string passName;
        private int rtDownScaling;
        private int iteration;
        private float strength;
        private Material mat;
        private RenderTextureDescriptor blurTextureDescriptor01;
        private RTHandle temp01;
        private RenderTextureDescriptor blurTextureDescriptor02;
        private RTHandle temp02;
        private RenderTexture _renderTexture;

        public BlurUIRenderPass(string passName, RenderPassEvent passEvent, Material mat, int rtDownscaling, int iteration, float strength)
        {
            renderPassEvent = passEvent;
            this.mat = mat;
            this.passName = passName;
            this.rtDownScaling = rtDownscaling;
            this.iteration = iteration;
            this.strength = strength;
            
            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;
            int msaaSamples = urpAsset ? urpAsset.msaaSampleCount : 1; // Default to 1 if no URP asset is found
            
            blurTextureDescriptor01 = new RenderTextureDescriptor(Screen.width,
                Screen.height, RenderTextureFormat.Default, 0)
            {
                msaaSamples = msaaSamples 
            };
            blurTextureDescriptor02 = new RenderTextureDescriptor(Screen.width,
                Screen.height, RenderTextureFormat.Default, 0)
            {
                msaaSamples = msaaSamples 
            };
        }

        public void SetUp(RenderTexture rt)
        {
            _renderTexture = rt;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // Set the blur texture size to be the same as the camera target size.
            blurTextureDescriptor01.width = cameraTextureDescriptor.width;
            blurTextureDescriptor01.height = cameraTextureDescriptor.height;
            blurTextureDescriptor01.msaaSamples = cameraTextureDescriptor.msaaSamples;

            blurTextureDescriptor02.width = cameraTextureDescriptor.width;
            blurTextureDescriptor02.height = cameraTextureDescriptor.height;
            blurTextureDescriptor02.msaaSamples = cameraTextureDescriptor.msaaSamples;
            // Check if the descriptor has changed, and reallocate the RTHandle if necessary
            RenderingUtils.ReAllocateIfNeeded(ref temp01, blurTextureDescriptor01);
            RenderingUtils.ReAllocateIfNeeded(ref temp02, blurTextureDescriptor02);
            if (temp01 != null && temp01.rt != null) temp01.rt.wrapMode = TextureWrapMode.Clamp;
            if (temp02 != null && temp02.rt != null) temp02.rt.wrapMode = TextureWrapMode.Clamp;
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (mat == null) return;

            CommandBuffer cmd = CommandBufferPool.Get(passName);
            RTHandle cameraHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;
            cmd.Blit(cameraHandle, temp01);
            bool tempSwitch = true;
            for (int i = 0; i < iteration; i++)
            {
                mat.SetFloat("_Strength", i / rtDownScaling + strength);
                cmd.Blit(tempSwitch ? temp01 : temp02, tempSwitch ? temp02 : temp01, mat);
                tempSwitch = !tempSwitch;
            }
            mat.SetFloat("_Strength", iteration / rtDownScaling + strength);

            cmd.Blit(temp01, _renderTexture, mat);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (BlurUIRenderPassFeature.instance.BlurUIEvent != null)
                BlurUIRenderPassFeature.instance.BlurUIEvent();
            temp01?.Release();
            temp02?.Release();
        }

        public void Dispose()
        {
            temp01?.Release();
            temp02?.Release();
        }
    }

    [Header("渲染时机")]
    BlurUIRenderPass m_ScriptablePass;
    public Material BlurMat;    //由于时机问题，所以采样直接引用的形式，可以在一开始就取得材质球
    public RenderPassEvent PassEvent;
    [Header("RT图缩小系数")]
    [Range(1, 10)]
    public int RTDownScaling = 8;
    [Header("迭代次数(建议用偶数)")]
    [Range(1, 8)] public int Iteration = 4;
    [Header("模糊强度")]
    [Range(0f, 20f)] public float Strength = 10;

    private RenderTexture _renderTexture;

    public bool IsDoingOnceProper = false;
    public Action BlurUIEvent;//坑：模糊调用后并不是这帧结束后就能得到模糊的RT图，为了正确得到后再显示，只能用委托的形式

    float recordTime = 0f;
    public void DoingBlur(RenderTexture renderTexture)
    {
        _renderTexture = renderTexture;
        IsDoingOnceProper = true;
    }

    /// <inheritdoc/>
    public override void Create()
    {
        instance = this;
        m_ScriptablePass = new BlurUIRenderPass(this.name, PassEvent, BlurMat, RTDownScaling, Iteration, Strength);
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (IsDoingOnceProper)
        {
            m_ScriptablePass.SetUp(_renderTexture);
            renderer.EnqueuePass(m_ScriptablePass);
            IsDoingOnceProper = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass.Dispose();
        base.Dispose(disposing);
    }
}


