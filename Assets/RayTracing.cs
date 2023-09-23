using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    private static bool _meshObjectsNeedRebuilding = false;
    private static List<RayTracingObj> _rayTracingObjects = new List<RayTracingObj>();

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
        if(_meshObjectBuffer!=null){
            _meshObjectBuffer.Release();
        }
        if(_vertexBuffer!=null){
            _vertexBuffer.Release();
        }
        if(_indexBuffer!=null){
            _indexBuffer.Release();
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


    public static void RegisterObject(RayTracingObj obj)
    {
        _rayTracingObjects.Add(obj);
        _meshObjectsNeedRebuilding = true;
    }
    public static void UnregisterObject(RayTracingObj obj)
    {
        _rayTracingObjects.Remove(obj);
        _meshObjectsNeedRebuilding = true;
    }
    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
    where T : struct
    {
        // Do we already have a compute buffer?
        if (buffer != null)
        {
            // If no data or buffer doesn't match the given criteria, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }
        if (data.Count != 0)
        {
            // If the buffer has been released or wasn't there to
            // begin with, create it
            if (buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }
            // Set data on the buffer
            buffer.SetData(data);
        }
    }
    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            RayTracingShader.SetBuffer(0, name, buffer);
        }
    }
    struct MeshObject
    {
        public Matrix4x4 localToWorldMatrix;
        public int indices_offset;
        public int indices_count;
        public Vector3 albedo;
        public float metallic;
        public float roughness;
        public Vector3 emission;
    }
    private static List<MeshObject> _meshObjects = new List<MeshObject>();
    private static List<Vector3> _vertices = new List<Vector3>();
    private static List<int> _indices = new List<int>();
    private ComputeBuffer _meshObjectBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;

    public void RebuildMeshObjectBuffers()
    {
        if (!_meshObjectsNeedRebuilding)
        {
            return;
        }
        _meshObjectsNeedRebuilding = false;
        _currentSample = 0;
        // Clear all lists
        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();
        // Loop over all objects and gather their data
        foreach (RayTracingObj obj in _rayTracingObjects)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            Material mat = obj.GetComponent<MeshRenderer>().sharedMaterial;
            // Add vertex data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices);
            // Add index data - if the vertex buffer wasn't empty before, the
            // indices need to be offset
            // Select is a standard extension method in 
            // LINQ, which yields a new collection based on 
            // the condition in the predicate.
            int firstIndex = _indices.Count;
            var indices = mesh.GetIndices(0);
            _indices.AddRange(indices.Select(index => index + firstVertex));
            // Add the object itself

            _meshObjects.Add(new MeshObject()
            {
                localToWorldMatrix = obj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length,
                albedo = mat.GetVector("_Color"),
                metallic = mat.GetFloat("_Metallic"),
                roughness = 1.0f - mat.GetFloat("_Glossiness"),
                emission = mat.GetVector("_EmissionColor")                
            });
        }
        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, System.Runtime.InteropServices.Marshal.SizeOf(typeof(MeshObject)));
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
    }
    public static void UpdateObject(RayTracingObj obj)
    {
        _meshObjectsNeedRebuilding = true;
    }
    private void Update()
    {
        if (transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
            RebuildMeshObjectBuffers();
        }
    }
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        RebuildMeshObjectBuffers();
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

        SetComputeBuffer("_Spheres", _sphereBuffer);
        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);
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
            sphere.emission = sphere.albedo * Mathf.Pow(t,5)* 5;
            spheres.Add(sphere);
        }
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Sphere));
        //Debug.Log(stride);
        _sphereBuffer = new ComputeBuffer(spheres.Count, stride);
        _sphereBuffer.SetData(spheres);
    }



}
