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
        [Header("References")]

        [SerializeField]
        private SnowAccumulationManager accumulation;

        [SerializeField]
        private Shader snowShellShader;


        [Header("Receivers")]

        [SerializeField]
        private LayerMask receiverLayers = -1;

        [SerializeField]
        private bool skipTransparentMaterials = true;

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


        [Header("Thickness Multipliers")]

        [SerializeField, Range(0f, 1.5f)]
        private float defaultThickness = .72f;

        [SerializeField, Range(0f, 1.5f)]
        private float vehicleThickness = .48f;

        [SerializeField, Range(0f, 1.5f)]
        private float vegetationThickness = .32f;

        [SerializeField, Range(0f, 1.5f)]
        private float buildingThickness = .78f;


        [Header("Runtime")]

        [SerializeField, Min(0f)]
        private float rescanInterval = 8f;


        private const string ShellName =
            "__GlobalSnowShell";


        private readonly HashSet<int>
            registered =
                new();


        private readonly List<Renderer>
            shells =
                new();


        private Material shellMaterial;

        private float nextScan;

        private bool snowVisible;


        private static readonly int
            ThicknessId =
                Shader.PropertyToID(
                    "_SnowShellThicknessMultiplier");


        private static readonly int
            CoverageId =
                Shader.PropertyToID(
                    "_SnowShellCoverageMultiplier");


        private void Awake()
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
                    "Global Snow Shell shader bulunamadý.",
                    this);

                enabled =
                    false;

                return;
            }


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


        private void OnEnable()
        {
            if (accumulation == null)
            {
                accumulation =
                    GetComponent<
                        SnowAccumulationManager>();
            }


            if (accumulation != null)
            {
                accumulation.OnSnowAmountChanged +=
                    OnSnowAmountChanged;
            }


            RefreshNow();


            SetVisible(
                accumulation != null
                &&
                accumulation.Amount > .003f);
        }


        private void Update()
        {
            if (rescanInterval <= 0f
                ||
                Time.unscaledTime < nextScan)
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


            SetVisible(
                false);
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
        }


        public void RefreshNow()
        {
            if (shellMaterial == null)
                return;


            Renderer[] renderers =
                FindObjectsByType<
                    Renderer>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);


            foreach (Renderer source
                     in renderers)
            {
                if (!CanReceiveSnow(
                        source))
                {
                    continue;
                }


                int id =
                    source.GetInstanceID();


                if (!registered.Add(
                        id))
                {
                    continue;
                }


                Renderer shell =
                    CreateShell(
                        source);


                if (shell == null)
                    continue;


                shells.Add(
                    shell);


                shell.enabled =
                    snowVisible;
            }
        }


        private bool CanReceiveSnow(
            Renderer renderer)
        {
            if (renderer == null)
                return false;


            if (renderer.gameObject.name
                .StartsWith(
                    ShellName,
                    StringComparison.Ordinal))
            {
                return false;
            }


            if (renderer is not MeshRenderer
                &&
                renderer is not SkinnedMeshRenderer)
            {
                return false;
            }


            if ((receiverLayers.value
                 &
                 (1 << renderer.gameObject.layer))
                ==
                0)
            {
                return false;
            }


            if (ContainsAny(
                    renderer.gameObject.name,
                    excludedNames))
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


            foreach (Material material
                     in materials)
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


            string shader =
                material.shader != null

                    ?
                    material.shader.name
                        .ToLowerInvariant()

                    :
                    string.Empty;


            return
                shader.Contains(
                    "water")
                ||
                shader.Contains(
                    "glass")
                ||
                shader.Contains(
                    "particle")
                ||
                shader.Contains(
                    "sky")
                ||
                shader.Contains(
                    "cloud");
        }


        private Renderer CreateShell(
            Renderer source)
        {
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


                go.AddComponent<
                    MeshFilter>()
                    .sharedMesh =
                    sourceFilter.sharedMesh;


                MeshRenderer shell =
                    go.AddComponent<
                        MeshRenderer>();


                Configure(
                    source,
                    shell,
                    sourceFilter.sharedMesh.subMeshCount);


                return shell;
            }


            if (source
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


            return go;
        }


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
                new Material[
                    count];


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
                    new();


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


        private float ResolveThickness(
            Renderer renderer)
        {
            string objectName =
                renderer.gameObject.name
                +
                " "
                +
                (
                    renderer.transform.parent
                    !=
                    null

                        ?
                        renderer.transform.parent.name

                        :
                        string.Empty
                );


            if (ContainsAny(
                    objectName,

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
                    objectName,

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
                    objectName,

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


            foreach (string fragment
                     in fragments)
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


        private void OnSnowAmountChanged(
            float value)
        {
            SetVisible(
                value > .003f);
        }


        private void SetVisible(
            bool visible)
        {
            snowVisible =
                visible;


            for (int i = shells.Count - 1;
                 i >= 0;
                 i--)
            {
                if (shells[i] == null)
                {
                    shells.RemoveAt(
                        i);

                    continue;
                }


                shells[i].enabled =
                    visible;
            }
        }
    }
}