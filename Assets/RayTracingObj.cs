using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class RayTracingObj : MonoBehaviour
{
    private void OnEnable()
    {
        RayTracing.RegisterObject(this);
    }
    private void OnDisable()
    {
        RayTracing.UnregisterObject(this);
    }
    private void Update()
    {
        if(transform.hasChanged){
            transform.hasChanged = false;
            RayTracing.UpdateObject(this);
        }
    }
}
