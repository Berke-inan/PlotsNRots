using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Explicit selection only: never scans or converts every project material.
public static class WeatherMaterialTools
{
    [MenuItem("Tools/Plots & Rots/Weather/Create snow glTF materials for selected objects")]
    public static void ConvertGltfSelection()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/Weather/SnowGltf.shadergraph");
        var sourceShader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.cloud.gltfast/Runtime/Shader/glTF-pbrMetallicRoughness.shadergraph");
        if (shader == null || sourceShader == null) { Debug.LogError("glTF snow shaders have not imported."); return; }
        const string folder = "Assets/Art/Materials/WeatherMigration";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Art/Materials", "WeatherMigration");
        var copies = new Dictionary<Material, Material>();
        foreach (var go in Selection.gameObjects)
        {
            if (EditorUtility.IsPersistent(go)) { Debug.LogWarning("Select scene objects or objects in Prefab Mode."); continue; }
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null || original.shader != sourceShader) continue;
                    if ((original.HasProperty("_Surface") && original.GetFloat("_Surface") != 0)
                        || original.renderQueue >= 3000 || original.IsKeywordEnabled("_TRANSMISSION")) continue;
                    if (!copies.TryGetValue(original, out var copy))
                    {
                        copy = new Material(original) { shader = shader, name = original.name + " Snow" };
                        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/SnowMaterial.mat");
                        AssetDatabase.CreateAsset(copy, path);
                        copies.Add(original, copy);
                    }
                    materials[i] = copy;
                    changed = true;
                }
                if (!changed) continue;
                Undo.RecordObject(renderer, "Assign glTF snow material");
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(renderer);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Created {copies.Count} snow glTF materials; original embedded materials/textures retained. Save the scene. Undo restores renderer assignments; generated assets remain available.");
    }

    [MenuItem("Tools/Plots & Rots/Weather/Convert selected URP Lit materials to Snow Lit")]
    public static void ConvertSelected()
    {
        var shader = Shader.Find("Plots & Rots/Snow Lit");
        if (shader == null) { Debug.LogError("Snow Lit shader has not imported successfully."); return; }
        var materials = new HashSet<Material>();
        foreach (var selected in Selection.objects)
        {
            if (selected is Material material) materials.Add(material);
            if (selected is GameObject go)
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var shared in renderer.sharedMaterials) if (shared != null) materials.Add(shared);
        }
        int converted = 0;
        foreach (var material in materials)
        {
            string path = AssetDatabase.GetAssetPath(material);
            if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit"
                || !path.StartsWith("Assets/") || !path.EndsWith(".mat")
                || material.GetFloat("_Surface") != 0)
            {
                Debug.LogWarning($"Skipped {path}: only standalone opaque URP Lit .mat assets are supported. Embedded/glTF, glass, foliage, water and custom shaders require explicit adaptation.", material);
                continue;
            }
            Undo.RecordObject(material, "Enable global snow");
            // Same URP properties, keywords, passes and render state; no texture/UV remapping.
            material.shader = shader;
            EditorUtility.SetDirty(material);
            converted++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Enabled snow on {converted} material(s). Shared usages also change. Undo is available.");
    }

    [MenuItem("Tools/Plots & Rots/Weather/Log selected renderer shader support")]
    public static void AuditSelected()
    {
        foreach (var go in Selection.gameObjects)
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null) Debug.Log($"{renderer.name}: {material.name} / {material.shader.name} / {AssetDatabase.GetAssetPath(material)}", renderer);
    }
}
