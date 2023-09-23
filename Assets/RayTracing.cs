using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class RayTracing : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    private RenderTexture _target;
    private RenderTexture _converged;//to preserve high precision result
    public Texture SkyTex;
    public Light DirectionalLight;


    private uint _currentSample = 0;
    private Material _addMaterial;

    public bool EnableTAA = false;
    private Camera _camera;
    
    //Scene
    public Vector2 SphereRadius = new Vector2(3.0f, 8.0f);
    public uint SpheresMax = 100;
    public float SpherePlacementRadius = 100.0f;
    private ComputeBuffer _sphereBuffer;
    [Range(0,10000)]public int SphereSeed;
    void OnEnable()
    {
        _currentSample = 0;
        SetUpScene();
    }
    void OnDisable(){
        if(_sphereBuffer!=null){
            _sphereBuffer.Release();
        }
    }
    void Start(){
        _currentSample = 0;
        if (Application.isPlaying)  
        {  
            EnableTAA = true;
            _camera = GetComponent<Camera>();
        }else{
            EnableTAA = false;
             _camera = SceneView.lastActiveSceneView.camera;
        }
    }

    private void Update()
    {
        if (transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
        }
    }
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Render(dest);
    }
    private void Render(RenderTexture dest)
    {
        InitRenderTexture();
        RayTracingShader.SetTexture(0, "Result", _target);
        SetRayTracingShaderParam();
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

                // Blit the result texture to the screen
        if(EnableTAA){
            if (_addMaterial == null)
                _addMaterial = new Material(Shader.Find("Hidden/AddSample"));
            _addMaterial.SetFloat("_samples", _currentSample);
            Graphics.Blit(_target, _converged, _addMaterial);
            Graphics.Blit(_converged, dest);
            _currentSample++;
        }else {
            Graphics.Blit(_target, dest);
        }

    }
    private void InitRenderTexture()
    {
        if(_target == null ||_target.width!=Screen.width ||_target.height != Screen.height){
            if(_target!=null){
                _target.Release();
            }
            _currentSample = 0;
            _target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
        }
        if(_converged == null ||_converged.width!=Screen.width ||_converged.height != Screen.height){
            if(_converged!=null){
                _converged.Release();
            }
            _converged = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();
        }

    }
    private void SetRayTracingShaderParam()
    {
        RayTracingShader.SetTexture(0, "_SkyTex", SkyTex);
        Vector2 offset;
        if(EnableTAA){
            offset = new Vector2(Random.value, Random.value);
        }else{
            offset = new Vector2(0.5f, 0.5f);
        }
        RayTracingShader.SetVector("_PixelOffset", offset);
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));
        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
        RayTracingShader.SetVector("_Time", new Vector4(Time.time, Time.deltaTime, 0.0f, 0.0f));
        RayTracingShader.SetFloat("_Seed", Random.value);
    }
 
    struct Sphere
    {
        public Vector3 center;
        public float radius;
        public Vector3 albedo;
        public float metallic;
        public float roughness;
        public Vector3 emission;
    };
    private void SetUpScene()
    {
        Random.InitState(SphereSeed);
        List<Sphere> spheres = new List<Sphere>();
        for (int i = 0; i < SpheresMax; i++)
        {
            Sphere sphere = new Sphere();
            sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.center = new Vector3(randomPos.x, sphere.radius, randomPos.y);
            bool intersect = false;
            foreach (Sphere s in spheres)
            {
                float minDis = s.radius + sphere.radius;
                if (Vector3.Distance(s.center, sphere.center) < minDis)
                {
                    intersect = true;
                    break;
                }
            }
            if (intersect)
            {
                continue;
            }
            Color color = Random.ColorHSV();

            sphere.albedo = new Vector3(color.r, color.g, color.b);//Vector3.zero;
            sphere.metallic = Random.value;

            sphere.roughness = Random.value;
            float t = Random.value;
            sphere.emission = sphere.albedo * Mathf.Pow(t,10)* 5;
            spheres.Add(sphere);
        }
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Sphere));
        //Debug.Log(stride);
        _sphereBuffer = new ComputeBuffer(spheres.Count, stride);
        _sphereBuffer.SetData(spheres);
    }
}
