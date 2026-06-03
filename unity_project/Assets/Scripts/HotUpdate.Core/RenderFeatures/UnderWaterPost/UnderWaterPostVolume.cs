using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

[System.Serializable, VolumeComponentMenu("Post-Processing(ONEMT)/UnderWaterPost")]
public class UnderWaterPostVolume : VolumeComponent, IPostProcessComponent
{
    [Header("扭曲")] 
    [Tooltip("焦散开关")]public BoolParameter DistortBool = new BoolParameter(true);
    [Tooltip("扭曲强度")] public ClampedFloatParameter DistortStrength = new ClampedFloatParameter(0f, 0f, 1f);
    [Header("暗角")]
    [Tooltip("圆形遮罩范围X,平滑Y;上下遮罩范围Z,平滑W")] public Vector4Parameter Limit = new Vector4Parameter(new Vector4(1, 1, 1,1));
    [Tooltip("暗角颜色")] public  ColorParameter color = new ColorParameter(Color.white,true);
    [Space()] 
    [Header("焦散")] 
    [Tooltip("焦散开关")]public BoolParameter Bool = new BoolParameter(false);
    [Tooltip("焦散开关")]public BoolParameter DUNGEONBool = new BoolParameter(false);
    public  ColorParameter CausticColor = new ColorParameter(Color.white,true);
    [Tooltip("焦散纹理")]public TextureParameter CausticTexture = new TextureParameter(null);
    [Tooltip("焦散01流动方向(XY,速度(Z))")] public Vector3Parameter CausticFlow01 = new Vector3Parameter(new Vector3(1, 1, 1));
    
    [Header("屏幕扭曲特效")] 
    [Tooltip("扭曲开关")]public BoolParameter DistBool = new BoolParameter(false);
    [Tooltip("扭曲纹理")]public TextureParameter DistTexture = new TextureParameter(null);
    public  ColorParameter DistColor = new ColorParameter(Color.white,true);
    public Vector3Parameter Tiling = new Vector3Parameter(new Vector3(1, 1, 1));
    public Vector3Parameter Speed = new Vector3Parameter(new Vector3(1, 1, 1));
    
    
    [Header("场景扫描特效")]
    public BoolParameter ScanBool = new BoolParameter(false);
    public TextureParameter ScanTexture = new TextureParameter(null);
    public  ColorParameter ScanColor = new ColorParameter(Color.white,true);
    public Vector3Parameter CenterParame = new Vector3Parameter(new Vector3(0, 0, 0));
    public Vector3Parameter SmoothSpeed = new Vector3Parameter(new Vector3(0, 0, 0));
    public Vector3Parameter TextureTil = new Vector3Parameter(new Vector3(0, 0, 0));
    
    
    public bool IsActive() => DistortStrength.value > 0 || CausticTexture.value != null;
    // public bool IsVignet() => Limit != null;

    public bool IsTileCompatible() => false;
}