using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Explicit menu actions only. Never changes project-wide renderer settings.</summary>
public static class WaterWorksSetup
{
    private const string Folder = "Assets/WaterWorksGenerated";
    private static void EnsureFolder() { if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "WaterWorksGenerated"); }
    [MenuItem("Tools/WaterWorks/1 - Prepare Selected Lake")]
    private static void PrepareLake()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null || EditorUtility.IsPersistent(go) || go.GetComponent<MeshFilter>() == null || go.GetComponent<MeshRenderer>() == null)
        { Debug.LogWarning("Select the water MeshRenderer in the scene Hierarchy."); return; }
        if (Application.isPlaying) { Debug.LogWarning("Exit Play Mode before setup."); return; }
        if (Vector3.Dot(go.transform.up, Vector3.up) < .9999f || Mathf.Abs(go.transform.eulerAngles.y) > .01f)
        { Debug.LogWarning("This lake setup requires world rotation (0,0,0). Set it before setup.", go); return; }
        if (go.transform.parent != null && Mathf.Abs(go.transform.parent.lossyScale.y) < .0001f)
        { Debug.LogWarning("The parent has zero Y scale. Fix that first.", go); return; }
        Shader shader = Shader.Find("WaterWorks/LowPolyWater_URP6");
        if (shader == null) { Debug.LogError("Import the WaterWorks surface shader first."); return; }
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Prepare WaterWorks Lake");
        Undo.RecordObject(go.transform, "Fix Water Scale");
        Vector3 scale = go.transform.localScale;
        if (Mathf.Abs(scale.y) < .0001f) scale.y = 1;
        go.transform.localScale = scale;
        EnsureFolder();
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        Material mat = renderer.sharedMaterial != null && renderer.sharedMaterial.shader == shader ? new Material(renderer.sharedMaterial) : new Material(shader);
        mat.name = go.name + "_Water_v4";
        // Preserve existing colors when the old material already uses the compatible surface shader.
        mat.SetFloat("_FacetStrength", .12f); mat.SetFloat("_WakeFoam", .35f);
        mat.SetFloat("_EnableRefraction", 0); // Opaque Texture is optional until explicitly enabled by the user.
        AssetDatabase.CreateAsset(mat, AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+mat.name+".mat"));
        Undo.RecordObject(renderer, "Assign v4 Water Material"); renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        WaterSurface surface = go.GetComponent<WaterSurface>();
        if (surface == null) surface = Undo.AddComponent<WaterSurface>(go);
        Undo.RecordObject(surface,"Assign Water Material"); surface.waterMaterial = mat;
        if (go.GetComponent<WaterPhysicsVolume>() == null) Undo.AddComponent<WaterPhysicsVolume>(go);
        foreach (MeshCollider collider in go.GetComponents<MeshCollider>())
        { Undo.RecordObject(collider,"Disable Solid Water Collider"); collider.enabled = false; }
        if (go.GetComponent<WaterAdaptiveMesh>() == null) Undo.AddComponent<WaterAdaptiveMesh>(go);
        Water_Settings settings = go.GetComponent<Water_Settings>();
        if (settings != null && settings.waterVolumeMaterial != null && settings.waterVolumeMaterial.HasProperty("_UnderwaterOnly"))
        {
            Undo.RecordObject(settings.waterVolumeMaterial,"Use Smooth Underwater Effect");
            settings.waterVolumeMaterial.SetFloat("_UnderwaterOnly",1);
        }
        go.GetComponent<WaterPhysicsVolume>().ConfigureTrigger();
        EditorUtility.SetDirty(go); AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(group);
        Debug.Log("WaterWorks v4 prepared. Enable URP Depth Texture. Assign your boat/camera to WaterAdaptiveMesh > Follow Target. Disable duplicate older water surfaces/physics manually. The adaptive grid is generated in Play Mode.",go);
    }
    [MenuItem("Tools/WaterWorks/2 - Prepare Selected Floating Object")]
    private static void PrepareBody()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null || EditorUtility.IsPersistent(go) || Application.isPlaying) return;
        if (go.GetComponent<Collider>() == null) Undo.AddComponent<BoxCollider>(go);
        Rigidbody body = go.GetComponent<Rigidbody>(); if (body == null) body = Undo.AddComponent<Rigidbody>(go);
        if (go.GetComponent<BuoyantObject>() == null) Undo.AddComponent<BuoyantObject>(go);
        Undo.RecordObject(body,"Configure Buoyant Rigidbody"); body.interpolation = RigidbodyInterpolation.Interpolate;
        Debug.Log("Buoyancy added. Set mass, hull collider and (for a boat) displaced volume. Rigidbody must be non-kinematic with Use Gravity enabled. Do not run another buoyancy script on this object.",go);
    }
    [MenuItem("Tools/WaterWorks/3 - Create Wake Test At Selected Lake")]
    private static void CreateTest()
    {
        WaterSurface surface = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<WaterSurface>() : null;
        if (surface == null || Application.isPlaying) { Debug.LogWarning("Select a prepared lake before creating the test boat."); return; }
        GameObject boat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boat.name = "WaterWorks_WakeTest"; Undo.RegisterCreatedObjectUndo(boat,"Create Wake Test");
        boat.transform.position = new Vector3(surface.transform.position.x,surface.BaseWaterLevel+1.5f,surface.transform.position.z);
        boat.transform.localScale = new Vector3(2,1,4);
        Rigidbody rb = Undo.AddComponent<Rigidbody>(boat); rb.mass = 2500; rb.interpolation = RigidbodyInterpolation.Interpolate;
        BuoyantObject buoy = Undo.AddComponent<BuoyantObject>(boat); buoy.linearWaterDrag = .25f; buoy.wakeStrength = .035f;
        WaterAdaptiveMesh grid = surface.GetComponent<WaterAdaptiveMesh>();
        if (grid != null) { Undo.RecordObject(grid,"Follow Test Boat"); grid.followTarget = boat.transform; }
        Selection.activeGameObject = boat;
        Debug.Log("Enter Play Mode, wait for flotation, then BuoyantObject component menu > Test - Push forward (Play Mode). The cube is a boat proxy; no input package required.", boat);
    }
}
