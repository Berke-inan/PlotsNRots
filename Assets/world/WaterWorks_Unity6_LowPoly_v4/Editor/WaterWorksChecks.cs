using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WaterWorksChecks
{
    [MenuItem("Tools/WaterWorks/4 - Run Wave Math Checks")]
    private static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("Run these checks outside Play Mode."); return; }
        Shader shader = Shader.Find("WaterWorks/LowPolyWater_URP6");
        if (shader == null) throw new InvalidOperationException("WaterWorks shader is missing.");
        Scene preview = EditorSceneManager.NewPreviewScene();
        Material material = new Material(shader);
        try
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(go, preview);
            material.SetFloat("_WaveHeight", 0);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            WaterSurface water = go.AddComponent<WaterSurface>();
            Vector3 origin = new Vector3(3, 0, 4);
            float t = Time.time;
            water.AddRipple(origin, .18f, 1.2f, 2.4f);
            Require(Mathf.Abs(water.GetWaterHeight(origin,t)-water.BaseWaterLevel)<.0001f,"New pulse must start continuously at zero.");
            const float age = .6f, dx = .002f;
            for (int i = 0; i < 12; ++i)
            {
                Vector3 p = origin + new Vector3(.3f+i*.21f,0,.4f);
                float gx = (water.GetWaterHeight(p+Vector3.right*dx,t+age)-water.GetWaterHeight(p-Vector3.right*dx,t+age))/(2*dx);
                float gz = (water.GetWaterHeight(p+Vector3.forward*dx,t+age)-water.GetWaterHeight(p-Vector3.forward*dx,t+age))/(2*dx);
                Vector3 numeric = new Vector3(-gx,1,-gz).normalized;
                Require(Vector3.Angle(numeric,water.GetWaterNormal(p,t+age))<.15f,"Normal must match height derivative.");
            }
            Vector3 crest = origin+Vector3.right*(age*water.rippleSpeed);
            Require(water.GetWaterHeight(crest,t+age)-water.BaseWaterLevel>.001f,"Pulse must propagate outwards.");
            Require(Mathf.Abs(water.GetWaterHeight(crest,t+water.rippleLifetime+.1f)-water.BaseWaterLevel)<.0001f,"Expired pulse must disappear.");
            for (int i=0;i<64;++i) water.AddRipple(origin,.3f);
            Require(water.ActiveRippleCount<=32,"Ripple storage must be bounded.");
            Require(Mathf.Abs(water.GetWaterHeight(crest,t+age)-water.BaseWaterLevel)<=water.maxInteractionHeight+.0001f,"Superposition must respect displacement bound.");
            Debug.Log("WaterWorks: 17 wave assertions passed. This does not validate the GPU, buoyancy, rendering, or Player build.");
        }
        finally { UnityEngine.Object.DestroyImmediate(material); EditorSceneManager.ClosePreviewScene(preview); }
    }
    private static void Require(bool ok,string message) { if (!ok) throw new InvalidOperationException("WaterWorks check failed: "+message); }
}
