/// <summary>
/// 模糊后处理(KawaseBlur算法)
/// by taecg
/// </summary>
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RealTimeBlurRenderPassFeature : ScriptableRendererFeature
{
    private static RealTimeBlurRenderPassFeature instance;
    public static RealTimeBlurRenderPassFeature Instance
    {
        get
        {
            return instance;
        }
    }

    class BlurRenderPass : ScriptableRenderPass
    {
        public bool bRTCreated = true;
        private string passName;
        private int rtDownScaling;
        private int iteration;
        private float strength;
        private int refreshInterval;
        private Material mat;
        private Material[] mats;
        private RenderTextureDescriptor blurTextureDescriptor01;
        private RTHandle temp01;
        private RenderTextureDescriptor blurTextureDescriptor02;
        private RTHandle temp02;

        public BlurRenderPass(string passName, RenderPassEvent passEvent, Material mat, int rtDownscaling, int iteration, float strength, int refreshInterval)
        {
            renderPassEvent = passEvent;
            this.mat = mat;
            this.passName = passName;
            this.rtDownScaling = rtDownscaling;
            this.iteration = iteration;
            this.strength = strength;
            this.refreshInterval = refreshInterval;
            blurTextureDescriptor01 = new RenderTextureDescriptor(Screen.width,
                Screen.height, RenderTextureFormat.Default, 0);
            blurTextureDescriptor02 = new RenderTextureDescriptor(Screen.width,
                Screen.height, RenderTextureFormat.Default, 0);
            mats = new Material[iteration];
            for (int i = 0; i < iteration; i++)
            {
                mats[i] = new Material(mat);
            }
        }
        public void ReleaseRT()
        {
            temp01?.Release();
            temp02?.Release();
        }

        public void Dispose()
        {
            ReleaseRT();
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // Set the blur texture size to be the same as the camera target size.
            blurTextureDescriptor01.width = cameraTextureDescriptor.width / rtDownScaling;
            blurTextureDescriptor01.height = cameraTextureDescriptor.height / rtDownScaling;

            blurTextureDescriptor02.width = cameraTextureDescriptor.width / rtDownScaling;
            blurTextureDescriptor02.height = cameraTextureDescriptor.height / rtDownScaling;
            // Check if the descriptor has changed, and reallocate the RTHandle if necessary

            if (bRTCreated) return;
            bRTCreated = true;
            RenderingUtils.ReAllocateIfNeeded(ref temp01, blurTextureDescriptor01);
            RenderingUtils.ReAllocateIfNeeded(ref temp02, blurTextureDescriptor02);
            if (temp01 != null && temp01.rt != null) temp01.rt.wrapMode = TextureWrapMode.Clamp;
            if (temp02 != null && temp02.rt != null) temp02.rt.wrapMode = TextureWrapMode.Clamp;
        }
        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (mat == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get(passName);
            //RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            //int width = descriptor.width / rtDownScaling;
            //int height = descriptor.height / rtDownScaling;
            //descriptor.depthBufferBits = 0;
            //cmd.GetTemporaryRT(temp01.id, width, height, 0, FilterMode.Bilinear);
            RTHandle cameraHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;
            if (Time.frameCount % refreshInterval == 0)
            {
                if (cameraHandle.isMSAAEnabled)
                {
                    cmd.EnableShaderKeyword("UNITY_MULTISAMPLE_ENABLED");
                }
                cmd.Blit(cameraHandle, temp01);
                bool tempSwitch = true;
                for (int i = 0; i < iteration; i++)
                {
                    mats[i].SetFloat("_Strength", i / rtDownScaling + strength);
                    cmd.Blit(tempSwitch ? temp01 : temp02, tempSwitch ? temp02 : temp01, mats[i]);
                    tempSwitch = !tempSwitch;
                }
                mat.SetFloat("_Strength", iteration / rtDownScaling + strength);
                cmd.Blit(temp01, cameraHandle, mat);
            }
            else
            {
                cmd.Blit(temp01, cameraHandle, mat);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
    }

    public Material BlurMat; 
    [Header("渲染时机")]
    public RenderPassEvent PassEvent;
    [Header("RT图缩小系数")]
    [Range(1, 10)]
    public int RTDownScaling = 6;
    [Header("迭代次数(建议用偶数)")]
    [Range(1, 8)] public int Iteration = 4;
    [Header("模糊强度")]
    [Range(0f, 20f)] public float Strength = 10;
    [Header("刷新间隔(帧)")]
    [Range(1, 10)] public int RefreshInterval = 2;
    BlurRenderPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        instance = this;
        m_ScriptablePass = new BlurRenderPass(this.name, PassEvent, BlurMat, RTDownScaling, Iteration, Strength, RefreshInterval);
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }

    public void CreateRT()
    {
        m_ScriptablePass.bRTCreated = false;
    }

    public void ReleaseRT()
    {
        m_ScriptablePass.ReleaseRT();
    }

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass.Dispose();
        base.Dispose(disposing);
    }
}


