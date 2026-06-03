using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 水底屏幕效果（扭曲+焦散）
/// by taecg
/// </summary>
public class UnderWaterPostFeature : ScriptableRendererFeature
{
    class UnderWaterPass : ScriptableRenderPass
    {
        private readonly string m_ProfilerTag;
        private readonly ProfilingSampler m_ProfilingSampler;
        // private readonly Shader m_Shader;
        private Material m_Material;
        private RTHandle dest;
        private UnderWaterPostVolume post;
        private Vector3 m_position;
        private Camera mainCamera;
        private readonly int m_DistortStrength = Shader.PropertyToID("_DistortStrength");
        private readonly int m_CausticTexture = Shader.PropertyToID("_CausticTexture");
        private readonly int m_CausticMaskTexture = Shader.PropertyToID("_CausticMaskTexture");
        private readonly int m_CausticFlow01 = Shader.PropertyToID("_CausticFlow01");
        private readonly int m_color = Shader.PropertyToID("_color");
        private readonly int m_CausticColor = Shader.PropertyToID("_CausticColor");
       
        // private readonly int m_Position = Shader.PropertyToID("_Position");
        private readonly int m_FoamTexture = Shader.PropertyToID("_FoamTexture");
        private readonly int m_Limit = Shader.PropertyToID("_Limit");
        private readonly int m_FoamLimit = Shader.PropertyToID("_FoamLimit");
        private readonly int m_DistTexture = Shader.PropertyToID("_DistTexture");
        private readonly int m_DistColor = Shader.PropertyToID("_DistColor");
        private readonly int m_Tiling = Shader.PropertyToID("_DistTiling");
        private readonly int m_Speed = Shader.PropertyToID("_DistSpeed");
        //SCAN
        private readonly int m_ScanTexture = Shader.PropertyToID("_ScanTexture");
        private readonly int m_ScanColor = Shader.PropertyToID("_ScanColor");
        private readonly int m_CenterParame = Shader.PropertyToID("_CenterParame");
        private readonly int m_SmoothSpeed = Shader.PropertyToID("_SmoothSpeed");
        private readonly int m_TextureTil = Shader.PropertyToID("_TextureTil");
        //  private readonly int m_Itereat = Shader.PropertyToID("_itereat");

        // private readonly int m_Bool = Shader.PropertyToID("_Bool");

        //   public void Setup()
        // {
        //     // destination.Init("_CameraTexture01");
        //     dest.Init("_CameraTexture");
        // }

        public UnderWaterPass(Material material)
        {
            m_ProfilerTag = "UnderWaterPost";
            m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            m_Material = material;
            // m_Shader = Shader.Find("Hidden/UnderWaterPost");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // if (m_Material == null && m_Shader != null)
            // m_Material = CoreUtils.CreateEngineMaterial(m_Shader);

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = (int)DepthBits.None;
            RenderingUtils.ReAllocateIfNeeded(ref dest, desc, name: m_ProfilerTag);
            ConfigureInput(ScriptableRenderPassInput.Normal);
            // m_position = new Vector3(0,0,0);
            // if(Camera.main != null)
            // {
            //     m_position = Camera.main.transform.position;
            // }


        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {

            if (m_Material == null)
            {
                Debug.LogError("用于水底渲染效果的材质球为空!");
                return;
            }
            // renderingData.cameraData.volumeLayerMask = 0<<15;
            if (!renderingData.cameraData.postProcessEnabled) return;
            var stack = VolumeManager.instance.stack;
            post = stack.GetComponent<UnderWaterPostVolume>();
            if (post == null || !post.IsActive()) return;
            if (post.DistortBool.value == true)
            {
                m_Material.EnableKeyword("_DISTORT_ON");
                m_Material.SetFloat(m_DistortStrength, post.DistortStrength.value);
            }
            // if(post.)
            else
            {
                m_Material.DisableKeyword("_DISTORT_ON");
            }

            if (post.Bool.value != false)
            {
                if (post.DUNGEONBool.value != false)
                {
                    m_Material.SetFloat("_DUNGEON", 1);
                }
                else
                {
                    m_Material.SetFloat("_DUNGEON", 0);
                }
                m_Material.EnableKeyword("_CAUSTIC_ON");
                m_Material.SetTexture(m_CausticTexture, post.CausticTexture.value);
                m_Material.SetVector(m_CausticFlow01, post.CausticFlow01.value);
                m_Material.SetColor(m_CausticColor, post.CausticColor.value);

            }
            else
            {
                m_Material.DisableKeyword("_CAUSTIC_ON");
            }

            if (post.DistBool.value != false)
            {
                m_Material.EnableKeyword("_POLAR_ON");
                m_Material.SetTexture(m_DistTexture, post.DistTexture.value);
                m_Material.SetColor(m_DistColor, post.DistColor.value);
                m_Material.SetVector(m_Tiling, post.Tiling.value);
                m_Material.SetVector(m_Speed, post.Speed.value);
            }
            else
            {
                m_Material.DisableKeyword("_POLAR_ON");
            }

            if (post.ScanBool.value != false)
            {
                m_Material.EnableKeyword("_SCAN_ON");
                m_Material.SetTexture(m_ScanTexture, post.ScanTexture.value);
                m_Material.SetColor(m_ScanColor, post.ScanColor.value);
                m_Material.SetVector(m_CenterParame, post.CenterParame.value);
                m_Material.SetVector(m_SmoothSpeed, post.SmoothSpeed.value);
                m_Material.SetVector(m_TextureTil, post.TextureTil.value);


            }
            else
            {
                m_Material.DisableKeyword("_SCAN_ON");
            }



            m_Material.SetVector(m_Limit, post.Limit.value);
            m_Material.SetColor(m_color, post.color.value);
            // m_Material.SetFloat(m_DefocusStrength, post.DefocusStrength.value);
            // m_Material.SetVector(m_Position, m_position);




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

        public override void OnCameraCleanup(CommandBuffer cmd)
        {


        }
        public void Dispose()
        {
            dest?.Release();
        }
    }

    public Material material;
    private UnderWaterPass m_UnderWaterPass;

    public override void Create()
    {
        m_UnderWaterPass ??= new UnderWaterPass(material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_UnderWaterPass);
    }
    protected override void Dispose(bool disposing)
    {
        // CoreUtils.Destroy(material);
        m_UnderWaterPass.Dispose();
    }

    public void EnableCaustic(bool enable)
    {
        var stack = VolumeManager.instance.stack;
        var post = stack.GetComponent<UnderWaterPostVolume>();
        if (post != null)
        {
            post.Bool = new BoolParameter(enable);
        }
    }

    public void EnterCityRoom(bool enter)
    {
        var stack = VolumeManager.instance.stack;
        var vignette = stack.GetComponent<Vignette>();
        vignette.center = new Vector2Parameter(new Vector2(0.5f, enter ? 0.8f : 0.5f));
    }
}