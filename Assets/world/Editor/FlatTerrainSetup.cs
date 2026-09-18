// Put this file in an Editor folder. No runtime component is required.
// Creates local derivatives of the project's installed URP Terrain shaders.
// Original Shader Graph, TerrainData, and installed packages are never edited.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SelcukTools
{
    public sealed class FlatTerrainSetup : EditorWindow
    {
        private const string PackagePath = "Packages/com.unity.render-pipelines.universal/";
        private const string TerrainPath = PackagePath + "Shaders/Terrain/";
        private const string BrightnessProperty = "_FlatTerrainBrightness";
        [SerializeField] private Terrain terrain;
        [SerializeField] private float brightness = 1f;
        [SerializeField] private string status = "";

        [MenuItem("Tools/Low Poly Terrain/Kurulum")]
        public static void Open()
        {
            var window = GetWindow<FlatTerrainSetup>("Low Poly Terrain");
            window.minSize = new Vector2(410, 260);
            if (Selection.activeGameObject != null)
                window.SetTerrain(Selection.activeGameObject.GetComponent<Terrain>());
            window.Show();
        }

        private void SetTerrain(Terrain value)
        {
            terrain = value;
            var material = terrain != null ? terrain.materialTemplate : null;
            brightness = material != null && material.HasProperty(BrightnessProperty)
                ? material.GetFloat(BrightnessProperty)
                : material != null && material.HasProperty("_Brightness")
                    ? material.GetFloat("_Brightness") : 1f;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Boyanabilir, köşeli Terrain", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Terrain'i seç, ardından materyali oluştur. Boyama için Terrain > Paint Texture kullan. " +
                "Zeminin yüksekliği ve Terrain Layer verileri değiştirilmez.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            var selected = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
            if (EditorGUI.EndChangeCheck()) SetTerrain(selected);
            brightness = EditorGUILayout.Slider("Parlaklık", brightness, 0f, 5f);
            using (new EditorGUI.DisabledScope(terrain == null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Materyali oluştur ve Terrain'e uygula", GUILayout.Height(32)))
                    GenerateAndApply();
                var material = terrain != null ? terrain.materialTemplate : null;
                using (new EditorGUI.DisabledScope(material == null || !material.HasProperty(BrightnessProperty)))
                {
                    if (GUILayout.Button("Parlaklığı mevcut materyale uygula"))
                    {
                        Undo.RecordObject(material, "Terrain parlaklığı");
                        material.SetFloat(BrightnessProperty, brightness);
                        EditorUtility.SetDirty(material);
                        AssetDatabase.SaveAssets();
                        SceneView.RepaintAll();
                    }
                }
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Geri alma: Terrain materyal atamasından sonra Ctrl+Z.", EditorStyles.wordWrappedLabel);
        }

        private void GenerateAndApply()
        {
            string generatedFolder = null;
            try
            {
                if (terrain == null || terrain.terrainData == null)
                    throw new InvalidOperationException("Sahneden geçerli bir Terrain seç.");
                if (EditorUtility.IsPersistent(terrain))
                    throw new InvalidOperationException("Project penceresindeki asset yerine sahnedeki Terrain'i seç.");
                var pipeline = GraphicsSettings.currentRenderPipeline;
                if (pipeline == null || pipeline.GetType().FullName != "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")
                    throw new InvalidOperationException("Bu araç etkin URP render pipeline'ı gerektirir.");

                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(TerrainPath + "TerrainLit.shader");
                if (package == null || !Directory.Exists(package.resolvedPath))
                    throw new InvalidOperationException("Kurulu URP shader kaynakları bulunamadı.");
                string sourceFolder = Path.Combine(package.resolvedPath, "Shaders", "Terrain");
                var sources = ReadSources(sourceFolder);
                string token = Guid.NewGuid().ToString("N").Substring(0, 10);
                string shaderName = "Selcuk/Flat Terrain/" + token;
                string folder = "Assets/LowPolyTerrain/Generated_" + token;
                // Validate every patch in memory before creating project files.
                var output = BuildSources(sources, folder, shaderName);

                if (!AssetDatabase.IsValidFolder("Assets/LowPolyTerrain"))
                    AssetDatabase.CreateFolder("Assets", "LowPolyTerrain");
                AssetDatabase.CreateFolder("Assets/LowPolyTerrain", "Generated_" + token);
                generatedFolder = folder;
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                foreach (var pair in output)
                    File.WriteAllText(Path.Combine(projectRoot, folder, pair.Key), pair.Value, new UTF8Encoding(false));
                string packageLicense = Path.Combine(package.resolvedPath, "LICENSE.md");
                if (File.Exists(packageLicense))
                    File.Copy(packageLicense, Path.Combine(projectRoot, folder, "URP_LICENSE.md"));
                File.WriteAllText(Path.Combine(projectRoot, folder, "SourceVersion.txt"),
                    "Generated from installed " + package.name + " " + package.version + "\n" +
                    "Unity " + Application.unityVersion + "\n" +
                    "Regenerate after a URP upgrade. Source package files were not changed.\n");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                // Do not assign a material when import reports errors.
                foreach (string name in new[] { "TerrainLit.shader", "TerrainLitAdd.shader", "TerrainLitBase.shader" })
                {
                    var imported = AssetDatabase.LoadAssetAtPath<Shader>(folder + "/" + name);
                    if (imported == null)
                        throw new InvalidOperationException(name + " içe aktarılamadı.");
                    var errors = ShaderUtil.GetShaderMessages(imported)
                        .Where(message => message.severity.ToString() == "Error").ToArray();
                    if (errors.Length > 0)
                        throw new InvalidOperationException(name + ": " + errors[0].message);
                }
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(folder + "/TerrainLit.shader");
                var material = new Material(shader) { name = "FlatTerrain_Material", enableInstancing = true };
                material.SetFloat(BrightnessProperty, brightness);
                // Geometric normals replace per-pixel heightmap normals in our lighting pass.
                if (material.HasProperty("_EnableInstancedPerPixelNormal"))
                    material.SetFloat("_EnableInstancedPerPixelNormal", 0f);
                material.DisableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
                AssetDatabase.CreateAsset(material, folder + "/FlatTerrain_Material.mat");
                Undo.RecordObject(terrain, "Low poly Terrain materyali");
                terrain.materialTemplate = material;
                PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
                EditorUtility.SetDirty(terrain);
                EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
                AssetDatabase.SaveAssets();
                SceneView.RepaintAll();
                EditorGUIUtility.PingObject(material);
                status = "Materyal atandı. Terrain > Paint Texture bölümünde çim/toprak/kaya katmanını seçip boya. " +
                    "Sahneyi kaydet. Oluşturulan dosyalar: " + folder;
                Debug.Log("Flat Terrain: URP " + package.version + ". " + status, terrain);
            }
            catch (Exception exception)
            {
                status = "Kurulum tamamlanamadı: " + exception.Message;
                if (generatedFolder != null)
                    status += " İnceleme dosyaları: " + generatedFolder;
                Debug.LogError("Flat Terrain: " + status);
            }
        }

        private static Dictionary<string, string> ReadSources(string folder)
        {
            var result = new Dictionary<string, string>();
            foreach (string name in new[] { "TerrainLit.shader", "TerrainLitAdd.shader", "TerrainLitBase.shader",
                "TerrainLitInput.hlsl", "TerrainLitPasses.hlsl", "TerrainLitDepthNormalsPass.hlsl" })
            {
                string path = Path.Combine(folder, name);
                if (!File.Exists(path)) throw new InvalidOperationException("URP dosyası bulunamadı: " + name);
                result.Add(name, File.ReadAllText(path).Replace("\r\n", "\n"));
            }
            return result;
        }

        // Public for offline source-patch verification; no Unity assets are touched here.
        public static Dictionary<string, string> BuildSources(Dictionary<string, string> source, string folder, string shaderName)
        {
            var result = new Dictionary<string, string>(source);
            string passes = result["TerrainLitPasses.hlsl"];
            passes = ReplaceOnce(passes, "void InitializeInputData(Varyings IN, half3 normalTS, out InputData inputData)",
                FlatNormalFunction + "\nvoid InitializeInputData(Varyings IN, half3 normalTS, out InputData inputData)");
            passes = ReplaceOnce(passes, "inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);",
                "inputData.normalWS = SelcukFlatNormal(IN.positionWS, IN.normal.xyz);");
            passes = ReplaceOnce(passes, "InitializeInputData(IN, normalTS, inputData);",
                "albedo *= _FlatTerrainBrightness;\n    InitializeInputData(IN, normalTS, inputData);");
            result["TerrainLitPasses.hlsl"] = passes;
            result["TerrainLitInput.hlsl"] = ReplaceOnce(result["TerrainLitInput.hlsl"],
                "CBUFFER_START(UnityPerMaterial)", "CBUFFER_START(UnityPerMaterial)\n    float _FlatTerrainBrightness;");

            // Make the depth/normal texture agree with visible faceted lighting (e.g. SSAO).
            string depth = result["TerrainLitDepthNormalsPass.hlsl"];
            if (Regex.IsMatch(depth, @"\bTEXCOORD6\b"))
                throw new InvalidOperationException("DepthNormals yapısı bu URP sürümünde farklı; uyarlama gerekli.");
            depth = ReplaceOnce(depth, "float4 clipPos", "float3 flatPositionWS : TEXCOORD6;\n    float4 clipPos");
            depth = ReplaceOnce(depth, "o.clipPos = attributes.positionCS;",
                "o.flatPositionWS = attributes.positionWS;\n    o.clipPos = attributes.positionCS;");
            depth = RegexOnce(depth,
                @"float2 splatUV\s*=[\s\S]*?outNormalWS\s*=\s*half4\(normalWS,\s*0\.0\);",
                "half3 normalWS = SelcukFlatNormal(IN.flatPositionWS, IN.normal.xyz);\n    outNormalWS = half4(normalWS, 0.0);");
            result["TerrainLitDepthNormalsPass.hlsl"] = depth;

            string oldMain = "Universal Render Pipeline/Terrain/Lit";
            string oldAdd = "Hidden/Universal Render Pipeline/Terrain/Lit (Add Pass)";
            string oldBase = "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)";
            foreach (string name in source.Keys.ToArray())
            {
                string text = result[name];
                // Only our six local copies are redirected. Other URP includes stay version-matched.
                foreach (string include in new[] { "TerrainLitInput.hlsl", "TerrainLitPasses.hlsl", "TerrainLitDepthNormalsPass.hlsl" })
                    text = text.Replace("\"" + TerrainPath + include + "\"", "\"" + folder + "/" + include + "\"");
                if (name.EndsWith(".shader", StringComparison.Ordinal))
                {
                    // Far basemap and depth passes can otherwise target a profile without derivatives.
                    text = Regex.Replace(text, @"#pragma target 2\.[05]\b", "#pragma target 3.0");
                    text = RegexOnce(text, @"\bProperties\s*\{",
                        "Properties\n    {\n        _FlatTerrainBrightness(\"Flat Terrain Brightness\", Range(0,5)) = 1");
                    text = text.Replace("\"" + oldMain + "\"", "\"" + shaderName + "\"")
                        .Replace("\"" + oldAdd + "\"", "\"Hidden/" + shaderName + "/Add\"")
                        .Replace("\"" + oldBase + "\"", "\"Hidden/" + shaderName + "/Base\"")
                        .Replace("\"" + oldMain + "/SceneSelectionPass\"", "\"" + shaderName + "/SceneSelectionPass\"");
                }
                result[name] = text;
            }
            // Preserve native basemap generation: brightness is applied once when rendering.
            Require(result["TerrainLit.shader"].Contains("\"TerrainCompatible\" = \"True\""), "TerrainCompatible etiketi");
            Require(result["TerrainLit.shader"].Contains("\"Hidden/" + shaderName + "/Add\""), "AddPass bağımlılığı");
            Require(result["TerrainLit.shader"].Contains("\"Hidden/" + shaderName + "/Base\""), "BaseMap bağımlılığı");
            Require(result["TerrainLit.shader"].Contains("Dependency \"BaseMapGenShader\""), "Basemap üretim bağımlılığı");
            Require(result["TerrainLit.shader"].Contains("_Control("), "Terrain kontrol haritası");
            return result;
        }

        private static string ReplaceOnce(string text, string before, string after)
        {
            int index = text.IndexOf(before, StringComparison.Ordinal);
            if (index < 0 || text.IndexOf(before, index + before.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("URP kaynağı beklenen yapıda değil: " + before);
            return text.Substring(0, index) + after + text.Substring(index + before.Length);
        }

        private static string RegexOnce(string text, string pattern, string replacement)
        {
            var regex = new Regex(pattern);
            if (regex.Matches(text).Count != 1)
                throw new InvalidOperationException("URP kaynağı bu uyarlamayla eşleşmiyor: " + pattern);
            return regex.Replace(text, match => replacement, 1);
        }

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Kaynak doğrulaması başarısız: " + description);
        }

        private const string FlatNormalFunction = @"
// Derivatives use float world positions. Align the result to the terrain's
// geometric normal so different graphics APIs cannot flip the lighting.
half3 SelcukFlatNormal(float3 positionWS, half3 referenceNormalWS)
{
    float3 faceNormal = cross(ddx(positionWS), ddy(positionWS));
    float squaredLength = dot(faceNormal, faceNormal);
    float3 reference = normalize((float3)referenceNormalWS);
    if (squaredLength < 1e-20)
        return (half3)reference;
    faceNormal *= rsqrt(squaredLength);
    faceNormal *= dot(faceNormal, reference) < 0.0 ? -1.0 : 1.0;
    return (half3)faceNormal;
}
";
    }
}
#endif
