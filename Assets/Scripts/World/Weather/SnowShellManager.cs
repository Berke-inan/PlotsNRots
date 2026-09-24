using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlotNRots.World.Weather
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class SnowShellManager : MonoBehaviour
    {
        // ============================================================
        // REFERENCES
        // ============================================================

        [Header("References")]

        [SerializeField]
        private SnowAccumulationManager accumulation;

        [SerializeField]
        private Shader snowShellShader;


        // ============================================================
        // RECEIVER FILTERING
        // ============================================================

        [Header("Receivers")]

        [Tooltip(
            "Generic snow shell oluþturulmasýna izin verilen layer'lar.")]
        [SerializeField]
        private LayerMask receiverLayers = -1;


        [Tooltip(
            "Transparent, glass, water, particle vb. material'larý dýþlar.")]
        [SerializeField]
        private bool skipTransparentMaterials = true;


        [Tooltip(
            "SkinnedMeshRenderer'lar varsayýlan olarak generic snow shell almaz. " +
            "Player, hayvan ve karakterlerde gereksiz ikinci skinning maliyetini de önler.")]
        [SerializeField]
        private bool allowSkinnedMeshRenderers = false;


        [Tooltip(
            "Renderer'ýn kendisinde veya parent/root zincirinde SnowExclusion varsa " +
            "generic snow shell oluþturulmaz.")]
        [SerializeField]
        private bool respectSnowExclusion = true;


        [SerializeField]
        private string[] excludedNames =
        {
            "glass",
            "window",
            "water",
            "particle",
            "vfx",
            "cloud",
            "sky",
            "ui"
        };


        // ============================================================
        // THICKNESS
        // ============================================================

        [Header("Thickness Multipliers")]

        [SerializeField, Range(0f, 1.5f)]
        private float defaultThickness = .72f;

        [SerializeField, Range(0f, 1.5f)]
        private float vehicleThickness = .48f;

        [SerializeField, Range(0f, 1.5f)]
        private float vegetationThickness = .32f;

        [SerializeField, Range(0f, 1.5f)]
        private float buildingThickness = .78f;


        // ============================================================
        // RUNTIME
        // ============================================================

        [Header("Runtime")]

        [Tooltip(
            "Yeni runtime objeleri yakalamak için renderer tarama aralýðý. " +
            "0 verilirse otomatik tekrar tarama kapanýr.")]
        [SerializeField, Min(0f)]
        private float rescanInterval = 8f;


        private const string ShellName =
            "__GlobalSnowShell";


        private readonly HashSet<int> registered =
            new();


        private readonly List<ShellBinding> shells =
            new();


        private Material shellMaterial;

        private float nextScan;

        private bool snowVisible;


        // ============================================================
        // INTERNAL BINDING
        // ============================================================

        private sealed class ShellBinding
        {
            public Renderer source;
            public Renderer shell;
        }


        // ============================================================
        // SHADER IDS
        // ============================================================

        private static readonly int ThicknessId =
            Shader.PropertyToID(
                "_SnowShellThicknessMultiplier");


        private static readonly int CoverageId =
            Shader.PropertyToID(
                "_SnowShellCoverageMultiplier");


        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            ResolveReferences();
        }


        private void OnEnable()
        {
            ResolveReferences();


            if (accumulation != null)
            {
                accumulation.OnSnowAmountChanged +=
                    OnSnowAmountChanged;
            }


            snowVisible =
                accumulation != null
                &&
                accumulation.Amount > .003f;


            RefreshNow();

            UpdateShellVisibility();
        }


        private void Update()
        {
            // Existing shell'lerin source renderer durumu deðiþtiyse
            // görünürlükleri de takip etsin.
            UpdateShellVisibility();


            if (rescanInterval <= 0f)
                return;


            if (Time.unscaledTime
                <
                nextScan)
            {
                return;
            }


            nextScan =
                Time.unscaledTime
                +
                rescanInterval;


            RefreshNow();
        }


        private void OnDisable()
        {
            if (accumulation != null)
            {
                accumulation.OnSnowAmountChanged -=
                    OnSnowAmountChanged;
            }


            snowVisible =
                false;


            UpdateShellVisibility();
        }


        private void OnDestroy()
        {
            if (shellMaterial == null)
                return;


            if (Application.isPlaying)
            {
                Destroy(
                    shellMaterial);
            }
            else
            {
                DestroyImmediate(
                    shellMaterial);
            }


            shellMaterial =
                null;
        }


        // ============================================================
        // REFERENCES
        // ============================================================

        private void ResolveReferences()
        {
            if (accumulation == null)
            {
                accumulation =
                    GetComponent<
                        SnowAccumulationManager>();
            }


            if (snowShellShader == null)
            {
                snowShellShader =
                    Shader.Find(
                        "Plots & Rots/Global Snow Shell");
            }


            if (snowShellShader == null)
            {
                Debug.LogError(
                    "SnowShellManager: 'Plots & Rots/Global Snow Shell' shader bulunamadý.",
                    this);

                enabled =
                    false;

                return;
            }


            if (shellMaterial != null)
                return;


            shellMaterial =
                new Material(
                    snowShellShader)
                {
                    name =
                        "Global Snow Shell (Runtime)",

                    enableInstancing =
                        true
                };
        }


        // ============================================================
        // SCENE SCAN
        // ============================================================

        public void RefreshNow()
        {
            if (shellMaterial == null)
                return;


            Renderer[] renderers =
                FindObjectsByType<Renderer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);


            foreach (Renderer source in renderers)
            {
                if (!CanReceiveSnow(
                        source))
                {
                    continue;
                }


                int id =
                    source.GetInstanceID();


                if (registered.Contains(
                        id))
                {
                    continue;
                }


                Renderer shell =
                    CreateShell(
                        source);


                if (shell == null)
                    continue;


                registered.Add(
                    id);


                shells.Add(
                    new ShellBinding
                    {
                        source =
                            source,

                        shell =
                            shell
                    });


                shell.enabled =
                    snowVisible
                    &&
                    source.enabled
                    &&
                    source.gameObject.activeInHierarchy;
            }
        }


        // ============================================================
        // RECEIVER FILTER
        // ============================================================

        private bool CanReceiveSnow(
            Renderer renderer)
        {
            if (renderer == null)
                return false;


            // Bizim oluþturduðumuz shell'ler tekrar shell üretmesin.
            if (renderer.gameObject.name
                .StartsWith(
                    ShellName,
                    StringComparison.Ordinal))
            {
                return false;
            }


            // Terrain Renderer bu sistemin iþi deðil.
            // Terrain kendi shader snow sistemini kullanacak.
            if (renderer is not MeshRenderer
                &&
                renderer is not SkinnedMeshRenderer)
            {
                return false;
            }


            // Karakter / hayvan gibi skinned objeler generic shell almaz.
            if (renderer is SkinnedMeshRenderer
                &&
                !allowSkinnedMeshRenderers)
            {
                return false;
            }


            // Root veya herhangi bir parent'ta SnowExclusion varsa
            // renderer kesinlikle generic snow receiver deðildir.
            if (respectSnowExclusion
                &&
                HasSnowExclusionInHierarchy(
                    renderer.transform))
            {
                return false;
            }


            // Layer filtresi.
            if ((receiverLayers.value
                 &
                 (1 << renderer.gameObject.layer))
                ==
                0)
            {
                return false;
            }


            // Renderer'ýn yalnýz kendi adý deðil,
            // root'a kadar bütün hiyerarþi isimleri kontrol edilir.
            if (HierarchyContainsExcludedName(
                    renderer.transform))
            {
                return false;
            }


            Material[] materials =
                renderer.sharedMaterials;


            if (materials == null
                ||
                materials.Length == 0)
            {
                return false;
            }


            // En az bir kullanýlabilir opaque material varsa
            // renderer generic shell alabilir.
            foreach (Material material in materials)
            {
                if (material == null)
                    continue;


                if (!skipTransparentMaterials
                    ||
                    !IsTransparentOrSpecial(
                        material))
                {
                    return true;
                }
            }


            return false;
        }


        // ============================================================
        // SNOW EXCLUSION
        // ============================================================

        private static bool HasSnowExclusionInHierarchy(
            Transform transform)
        {
            Transform current =
                transform;


            while (current != null)
            {
                if (current.TryGetComponent<
                        SnowExclusion>(
                        out _))
                {
                    return true;
                }


                current =
                    current.parent;
            }


            return false;
        }


        // ============================================================
        // NAME EXCLUSION
        // ============================================================

        private bool HierarchyContainsExcludedName(
            Transform transform)
        {
            Transform current =
                transform;


            while (current != null)
            {
                if (ContainsAny(
                        current.name,
                        excludedNames))
                {
                    return true;
                }


                current =
                    current.parent;
            }


            return false;
        }


        // ============================================================
        // MATERIAL FILTER
        // ============================================================

        private static bool IsTransparentOrSpecial(
            Material material)
        {
            if (material == null)
                return true;


            if (material.renderQueue >= 3000)
                return true;


            if (material.HasProperty(
                    "_Surface")
                &&
                material.GetFloat(
                    "_Surface")
                >
                .5f)
            {
                return true;
            }


            string shaderName =
                material.shader != null

                    ?
                    material.shader.name
                        .ToLowerInvariant()

                    :
                    string.Empty;


            return
                shaderName.Contains(
                    "water")
                ||
                shaderName.Contains(
                    "glass")
                ||
                shaderName.Contains(
                    "particle")
                ||
                shaderName.Contains(
                    "sky")
                ||
                shaderName.Contains(
                    "cloud");
        }


        // ============================================================
        // SHELL CREATION
        // ============================================================

        private Renderer CreateShell(
            Renderer source)
        {
            // --------------------------------------------------------
            // STATIC MESH
            // --------------------------------------------------------

            if (source is MeshRenderer)
            {
                MeshFilter sourceFilter =
                    source.GetComponent<
                        MeshFilter>();


                if (sourceFilter == null
                    ||
                    sourceFilter.sharedMesh == null)
                {
                    return null;
                }


                GameObject go =
                    CreateChild(
                        source.transform);


                MeshFilter shellFilter =
                    go.AddComponent<
                        MeshFilter>();


                shellFilter.sharedMesh =
                    sourceFilter.sharedMesh;


                MeshRenderer shellRenderer =
                    go.AddComponent<
                        MeshRenderer>();


                Configure(
                    source,
                    shellRenderer,
                    sourceFilter.sharedMesh.subMeshCount);


                return shellRenderer;
            }


            // --------------------------------------------------------
            // SKINNED MESH
            // --------------------------------------------------------
            //
            // Default olarak bu bölüme hiç girilmez.
            // allowSkinnedMeshRenderers yalnýz bilinçli olarak açýlýrsa
            // eski davranýþý desteklemek için tutuluyor.
            // --------------------------------------------------------

            if (allowSkinnedMeshRenderers
                &&
                source
                    is SkinnedMeshRenderer skinned
                &&
                skinned.sharedMesh != null)
            {
                GameObject go =
                    CreateChild(
                        source.transform);


                SkinnedMeshRenderer shell =
                    go.AddComponent<
                        SkinnedMeshRenderer>();


                shell.sharedMesh =
                    skinned.sharedMesh;


                shell.bones =
                    skinned.bones;


                shell.rootBone =
                    skinned.rootBone;


                shell.localBounds =
                    skinned.localBounds;


                shell.updateWhenOffscreen =
                    skinned.updateWhenOffscreen;


                shell.quality =
                    skinned.quality;


                Configure(
                    source,
                    shell,
                    skinned.sharedMesh.subMeshCount);


                return shell;
            }


            return null;
        }


        private static GameObject CreateChild(
            Transform parent)
        {
            GameObject go =
                new GameObject(
                    ShellName);


            go.layer =
                parent.gameObject.layer;


            go.transform.SetParent(
                parent,
                false);


            go.transform.localPosition =
                Vector3.zero;


            go.transform.localRotation =
                Quaternion.identity;


            go.transform.localScale =
                Vector3.one;


            return go;
        }


        // ============================================================
        // SHELL CONFIGURATION
        // ============================================================

        private void Configure(
            Renderer source,
            Renderer shell,
            int subMeshCount)
        {
            int count =
                Mathf.Max(
                    1,
                    subMeshCount);


            Material[] shellMaterials =
                new Material[count];


            for (int i = 0;
                 i < count;
                 i++)
            {
                shellMaterials[i] =
                    shellMaterial;
            }


            shell.sharedMaterials =
                shellMaterials;


            shell.shadowCastingMode =
                ShadowCastingMode.Off;


            shell.receiveShadows =
                true;


            shell.lightProbeUsage =
                source.lightProbeUsage;


            shell.reflectionProbeUsage =
                source.reflectionProbeUsage;


            shell.probeAnchor =
                source.probeAnchor;


            float thickness =
                ResolveThickness(
                    source);


            Material[] sourceMaterials =
                source.sharedMaterials;


            for (int i = 0;
                 i < count;
                 i++)
            {
                Material sourceMaterial =
                    sourceMaterials != null
                    &&
                    i < sourceMaterials.Length

                        ?
                        sourceMaterials[i]

                        :
                        null;


                bool usable =
                    sourceMaterial != null
                    &&
                    (
                        !skipTransparentMaterials
                        ||
                        !IsTransparentOrSpecial(
                            sourceMaterial)
                    );


                MaterialPropertyBlock block =
                    new MaterialPropertyBlock();


                block.SetFloat(
                    ThicknessId,
                    thickness);


                block.SetFloat(
                    CoverageId,
                    usable
                        ?
                        1f
                        :
                        0f);


                shell.SetPropertyBlock(
                    block,
                    i);
            }
        }


        // ============================================================
        // THICKNESS CLASSIFICATION
        // ============================================================

        private float ResolveThickness(
            Renderer renderer)
        {
            string hierarchyName =
                BuildHierarchyName(
                    renderer.transform);


            if (ContainsAny(
                    hierarchyName,
                    new[]
                    {
                        "car",
                        "vehicle",
                        "tractor",
                        "truck",
                        "araba",
                        "traktor",
                        "traktör"
                    }))
            {
                return
                    vehicleThickness;
            }


            if (ContainsAny(
                    hierarchyName,
                    new[]
                    {
                        "tree",
                        "pine",
                        "leaf",
                        "foliage",
                        "bush",
                        "grass",
                        "agac",
                        "aðaç",
                        "cam",
                        "çam"
                    }))
            {
                return
                    vegetationThickness;
            }


            if (ContainsAny(
                    hierarchyName,
                    new[]
                    {
                        "house",
                        "building",
                        "roof",
                        "barn",
                        "shed",
                        "ev",
                        "cati",
                        "çatý",
                        "ahir",
                        "ahýr"
                    }))
            {
                return
                    buildingThickness;
            }


            return
                defaultThickness;
        }


        private static string BuildHierarchyName(
            Transform transform)
        {
            if (transform == null)
                return string.Empty;


            System.Text.StringBuilder builder =
                new System.Text.StringBuilder();


            Transform current =
                transform;


            while (current != null)
            {
                builder.Append(
                    current.name);


                builder.Append(
                    ' ');


                current =
                    current.parent;
            }


            return
                builder.ToString();
        }


        // ============================================================
        // STRING HELPER
        // ============================================================

        private static bool ContainsAny(
            string value,
            string[] fragments)
        {
            if (string.IsNullOrEmpty(
                    value)
                ||
                fragments == null)
            {
                return false;
            }


            string lower =
                value.ToLowerInvariant();


            foreach (string fragment in fragments)
            {
                if (string.IsNullOrWhiteSpace(
                        fragment))
                {
                    continue;
                }


                if (lower.Contains(
                        fragment.ToLowerInvariant()))
                {
                    return true;
                }
            }


            return false;
        }


        // ============================================================
        // SNOW AMOUNT
        // ============================================================

        private void OnSnowAmountChanged(
            float value)
        {
            snowVisible =
                value > .003f;


            UpdateShellVisibility();
        }


        // ============================================================
        // VISIBILITY
        // ============================================================

        private void UpdateShellVisibility()
        {
            for (int i = shells.Count - 1;
                 i >= 0;
                 i--)
            {
                ShellBinding binding =
                    shells[i];


                if (binding == null
                    ||
                    binding.shell == null)
                {
                    shells.RemoveAt(
                        i);

                    continue;
                }


                if (binding.source == null)
                {
                    binding.shell.enabled =
                        false;

                    continue;
                }


                bool excludedNow =
                    respectSnowExclusion
                    &&
                    HasSnowExclusionInHierarchy(
                        binding.source.transform);


                bool canShow =
                    snowVisible
                    &&
                    !excludedNow
                    &&
                    binding.source.enabled
                    &&
                    binding.source.gameObject.activeInHierarchy;


                binding.shell.enabled =
                    canShow;
            }
        }


#if UNITY_EDITOR

        private void OnValidate()
        {
            defaultThickness =
                Mathf.Max(
                    0f,
                    defaultThickness);


            vehicleThickness =
                Mathf.Max(
                    0f,
                    vehicleThickness);


            vegetationThickness =
                Mathf.Max(
                    0f,
                    vegetationThickness);


            buildingThickness =
                Mathf.Max(
                    0f,
                    buildingThickness);


            rescanInterval =
                Mathf.Max(
                    0f,
                    rescanInterval);
        }

#endif
    }
}