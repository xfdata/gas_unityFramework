using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;


public class CitySoilFeature : ScriptableRendererFeature
{
    private CitySoilPass _citySoilPass;
    
    public override void Create()
    {
        _citySoilPass ??= new();
    }

    public void AddRenderFunc(Action<CommandBuffer> func)
    {
        Debug.Log($"CitySoilFeature pass:{_citySoilPass._id} AddRenderFunc");
        _citySoilPass.renderFuc += func;

    }

    public void RemoveRenderFunc(Action<CommandBuffer> func)
    {
        Debug.Log($"CitySoilFeature pass:{_citySoilPass._id} RemoveRenderFunc");
        _citySoilPass.renderFuc -= func;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
#if UNITY_EDITOR
        if (renderingData.cameraData.cameraType == CameraType.SceneView)
        {
            return;
        }
#endif
        
        renderer.EnqueuePass(_citySoilPass);
    }
    
    protected override void Dispose(bool disposing)
    {
        _citySoilPass.Dispose();
    }

    class CitySoilPass : ScriptableRenderPass
    {
        private CommandBuffer m_buff;
        public Action<CommandBuffer> renderFuc;
        private static int genId;
        public int _id;
        public CitySoilPass()
        {
            _id = genId++;
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            Debug.Log($"CitySoilFeature Create pass {_id}");
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderFuc == null) return;
            m_buff ??= CommandBufferPool.Get("DrawCitySoil");
            renderFuc.Invoke(m_buff);
            context.ExecuteCommandBuffer(m_buff);
        }
        
        public void Dispose()
        {
            if (m_buff != null)
            {
                CommandBufferPool.Release(m_buff);
                m_buff = null;
            }
        }
    }
}