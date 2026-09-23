#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PlotNRots.World.Weather.Editor
{
    public static class SnowMaterialTools
    {
        private const string OutputFolder = "Assets/Generated/Weather/SnowMaterials";

        [MenuItem("Tools/Plots & Rots/Weather/Create Snow Variants For Selected Renderers")]
        private static void CreateSnowVariants()
        {
            Shader snowShader = Shader.Find("Plots & Rots/Stylized Snow Lit");
            if (snowShader == null)
            {
                EditorUtility.DisplayDialog("Plots & Rots", "Could not find 'Plots & Rots/Stylized Snow Lit'.", "OK");
                return;
            }

            EnsureFolder(OutputFolder);
            var cache = new Dictionary<Material, Material>();
            int changedRenderers = 0;
            int skippedMaterials = 0;

            foreach (GameObject root in Selection.gameObjects)
            {
                if (root == null) continue;
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    bool changed = false;

                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material source = materials[i];
                        if (source == null) continue;
                        if (!CanConvert(source))
                        {
                            skippedMaterials++;
                            continue;
                        }

                        if (!cache.TryGetValue(source, out Material variant))
                        {
                            variant = CreateVariant(source, snowShader);
                            cache.Add(source, variant);
                        }

                        materials[i] = variant;
                        changed = true;
                    }

                    if (!changed) continue;
                    Undo.RecordObject(renderer, "Assign Snow Material Variants");
                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                    changedRenderers++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Snow variants assigned to {changedRenderers} renderer(s). Skipped material references: {skippedMaterials}.");
        }

        private static bool CanConvert(Material material)
        {
            if (material.shader == null) return false;
            string shaderName = material.shader.name.ToLowerInvariant();
            string materialName = material.name.ToLowerInvariant();

            if (material.renderQueue >= 3000) return false;
            if (shaderName.Contains("water") || shaderName.Contains("glass") || shaderName.Contains("particle") || shaderName.Contains("sky")) return false;
            if (shaderName.Contains("tree") || shaderName.Contains("foliage") || shaderName.Contains("nature")) return false;
            if (materialName.Contains("water") || materialName.Contains("glass") || materialName.Contains("window")) return false;

            return material.HasProperty("_BaseMap") || material.HasProperty("_MainTex");
        }

        private static Material CreateVariant(Material source, Shader snowShader)
        {
            var result = new Material(source)
            {
                shader = snowShader,
                name = source.name + "_Snow"
            };

            CopyTexture(source, result, "_BaseMap", "_BaseMap");
            CopyColor(source, result, "_BaseColor", "_BaseColor");
            CopyTexture(source, result, "_BumpMap", "_BumpMap");
            CopyFloat(source, result, "_BumpScale", "_BumpScale");
            CopyFloat(source, result, "_Metallic", "_Metallic");
            CopyFloat(source, result, "_Smoothness", "_Smoothness");
            CopyFloat(source, result, "_Cutoff", "_Cutoff");

            if (source.IsKeywordEnabled("_ALPHATEST_ON"))
                result.SetFloat("_AlphaClip", 1f);

            string safeName = string.Join("_", source.name.Split(Path.GetInvalidFileNameChars()));
            string path = AssetDatabase.GenerateUniqueAssetPath($"{OutputFolder}/{safeName}_Snow.mat");
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static void CopyTexture(Material source, Material target, string sourceName, string targetName)
        {
            if (!source.HasProperty(sourceName) || !target.HasProperty(targetName)) return;
            target.SetTexture(targetName, source.GetTexture(sourceName));
            target.SetTextureScale(targetName, source.GetTextureScale(sourceName));
            target.SetTextureOffset(targetName, source.GetTextureOffset(sourceName));
        }

        private static void CopyColor(Material source, Material target, string sourceName, string targetName)
        {
            if (source.HasProperty(sourceName) && target.HasProperty(targetName))
                target.SetColor(targetName, source.GetColor(sourceName));
        }

        private static void CopyFloat(Material source, Material target, string sourceName, string targetName)
        {
            if (source.HasProperty(sourceName) && target.HasProperty(targetName))
                target.SetFloat(targetName, source.GetFloat(sourceName));
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
