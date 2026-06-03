using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Post-Processing(ONEMT)/DepthRim")]
public class DepthRimVolum : VolumeComponent, IPostProcessComponent
{
    public BoolParameter Bool = new BoolParameter(false);
    public ClampedFloatParameter RimRange = new ClampedFloatParameter(0f, 0f, 0.1f);
    public ColorParameter RimColor = new ColorParameter(new Color(1f, 1f, 1f, 1f));
    
    public bool IsActive() => Bool.value != false ;
    public bool IsTileCompatible() => false;
}
