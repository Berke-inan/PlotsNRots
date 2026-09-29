using UnityEngine;

namespace PlotNRots.World.Weather
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)]
    [AddComponentMenu("Plots & Rots/Weather/Snow Interaction Manager")]
    public sealed class SnowInteractionManager : MonoBehaviour
    {
        public static SnowInteractionManager Instance { get; private set; }

        [Header("Required")]
        [SerializeField]
        private ComputeShader interactionCompute;

        [SerializeField]
        private Transform trackingTarget;

        [SerializeField]
        private global::SnowAccumulationManager accumulation;

        [Header("Local Footprint Clipmap")]
        [SerializeField, Range(256, 2048)]
        private int textureResolution = 1024;

        [SerializeField, Min(8f)]
        private float worldSize = 48f;

        [SerializeField, Range(.02f, .5f)]
        private float updateInterval = .10f;

        [Header("Footprint Appearance")]
        [Tooltip("1 = footprint can remove all visible snow. Lower values leave compressed snow in the print.")]
        [SerializeField, Range(0f, 1f)]
        private float snowCoverageCutStrength = .82f;

        [Tooltip("Visual-only depression used to bend the terrain snow normal. This does not deform the TerrainCollider.")]
        [SerializeField, Range(0f, .08f)]
        private float visualDepressionDepth = .035f;

        [Header("Footprint Shape")]
        [Tooltip("Relative heel width. Smaller values make the heel narrower than the toe.")]
        [SerializeField, Range(.45f, .9f)]
        private float footprintHeelWidth = .68f;

        [Tooltip("Relative width through the arch / middle of the sole.")]
        [SerializeField, Range(.5f, 1f)]
        private float footprintMidWidth = .76f;

        [Tooltip("Relative toe/forefoot width. Values just over 1 create a natural wider toe box.")]
        [SerializeField, Range(.85f, 1.2f)]
        private float footprintToeWidth = 1.05f;

        [Tooltip("Cuts a small notch from the inner mid-foot edge so the print is not a simple oval.")]
        [SerializeField, Range(0f, .45f)]
        private float footprintArchInset = .22f;

        [Tooltip("Makes heel and forefoot compress a little deeper than the arch.")]
        [SerializeField, Range(0f, .45f)]
        private float footprintPressureContrast = .20f;

        [Header("Wheel Track Tread")]
        [Tooltip("Master switch for procedural tire tread. Turn this off to get the old smooth rectangular rut.")]
        [SerializeField]
        private bool patternedWheelTracks = true;

        [Tooltip("Distance in metres between repeating tread rows. Around 0.16-0.22 works well for car/tractor tires.")]
        [SerializeField, Range(.08f, .35f)]
        private float defaultTreadRepeat = .18f;

        [Tooltip("How strongly the tread blocks/grooves differ from the base rut. 0 = smooth rut, 1 = very clear tread.")]
        [SerializeField, Range(0f, 1f)]
        private float defaultTreadContrast = .80f;

        [Tooltip("Normalized width of the shallow centre groove. This is intentionally broad enough to survive the interaction texture resolution.")]
        [SerializeField, Range(0f, .28f)]
        private float defaultCenterGrooveWidth = .10f;

        [Tooltip("0 = straight cross blocks, 1 = strong V/chevron tread. Generic all-terrain default is intentionally moderate.")]
        [SerializeField, Range(0f, 1f)]
        private float defaultChevronAmount = .38f;

        [Header("Recovery")]
        [Tooltip("Very slow recovery even while it is not snowing. 0 disables passive recovery.")]
        [SerializeField, Range(0f, .02f)]
        private float passiveRecoveryPerSecond = .0005f;

        [Tooltip("How quickly fresh snowfall fills an existing footprint at weather intensity 1.")]
        [SerializeField, Range(0f, .15f)]
        private float freshSnowFillPerSecond = .025f;

        [SerializeField, Range(1f, 3f)]
        private float snowStormFillMultiplier = 1.6f;

        private RenderTexture maskA;
        private RenderTexture maskB;
        private RenderTexture currentMask;
        private RenderTexture scratchMask;

        private int clearKernel = -1;
        private int scrollKernel = -1;
        private int stampKernel = -1;
        private int trackStampKernel = -1;

        private Vector2 worldOrigin;
        private float texelWorldSize;
        private float nextUpdateTime;
        private bool initialized;
        private bool clearedForNoSnow;

        private static readonly int InteractionTexId =
            Shader.PropertyToID("_GlobalSnowInteractionTex");

        private static readonly int InteractionWorldRectId =
            Shader.PropertyToID("_GlobalSnowInteractionWorldRect");

        private static readonly int InteractionParamsId =
            Shader.PropertyToID("_GlobalSnowInteractionParams");

        public bool IsReady =>
            initialized
            && currentMask != null;

        public bool CanStamp =>
            IsReady
            && accumulation != null
            && accumulation.HasVisibleSnow;

        public float TexelWorldSize =>
            texelWorldSize;

        private void Awake()
        {
            if (Instance != null
                && Instance != this)
            {
                Debug.LogWarning(
                    "Only one SnowInteractionManager should be active in a scene.",
                    this);

                enabled = false;
                return;
            }

            Instance = this;

            ResolveReferences();
        }

        private void OnEnable()
        {
            if (Instance == null)
                Instance = this;

            ResolveReferences();
            Build();
        }

        private void OnDisable()
        {
            PublishDisabled();
            Release();

            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            Release();
        }

        private void Update()
        {
            if (!initialized)
                return;

            ResolveReferences();

            bool visibleSnow =
                accumulation != null
                && accumulation.HasVisibleSnow;

            if (!visibleSnow)
            {
                if (!clearedForNoSnow)
                {
                    ClearFootprints();
                    clearedForNoSnow = true;
                }

                Publish();
                return;
            }

            clearedForNoSnow = false;

            if (Time.unscaledTime < nextUpdateTime)
            {
                Publish();
                return;
            }

            float interval =
                Mathf.Max(
                    .02f,
                    updateInterval);

            nextUpdateTime =
                Time.unscaledTime
                + interval;

            Vector2 newOrigin =
                CalculateDesiredOrigin();

            int shiftX =
                Mathf.RoundToInt(
                    (newOrigin.x - worldOrigin.x)
                    /
                    texelWorldSize);

            int shiftY =
                Mathf.RoundToInt(
                    (newOrigin.y - worldOrigin.y)
                    /
                    texelWorldSize);

            float recovery =
                CalculateRecoveryPerSecond()
                * interval;

            if (Mathf.Abs(shiftX) >= textureResolution
                || Mathf.Abs(shiftY) >= textureResolution)
            {
                worldOrigin = newOrigin;
                ClearFootprints();
            }
            else
            {
                DispatchScrollAndRecover(
                    shiftX,
                    shiftY,
                    recovery);

                worldOrigin +=
                    new Vector2(
                        shiftX,
                        shiftY)
                    * texelWorldSize;
            }

            Publish();
        }

        public bool StampFootprint(
            Vector3 worldPosition,
            Vector3 forwardWS,
            Vector2 footprintSize,
            float strength = 1f,
            float edgeSoftness = .22f,
            bool leftFoot = false,
            float shapeVariation = 0f)
        {
            if (!CanStamp)
                return false;

            Vector2 uv =
                (new Vector2(
                    worldPosition.x,
                    worldPosition.z)
                 - worldOrigin)
                /
                Mathf.Max(
                    .001f,
                    worldSize);

            if (uv.x <= 0f
                || uv.y <= 0f
                || uv.x >= 1f
                || uv.y >= 1f)
            {
                return false;
            }

            Vector2 forward =
                new Vector2(
                    forwardWS.x,
                    forwardWS.z);

            if (forward.sqrMagnitude <= .0001f)
                forward = Vector2.up;
            else
                forward.Normalize();

            Vector2 right =
                new Vector2(
                    forward.y,
                    -forward.x);

            Vector2 centerPixel =
                uv
                * textureResolution;

            Vector2 halfSizePixels =
                new Vector2(
                    Mathf.Max(.04f, footprintSize.x),
                    Mathf.Max(.06f, footprintSize.y))
                * .5f
                /
                texelWorldSize;

            float radius =
                Mathf.Ceil(
                    Mathf.Max(
                        halfSizePixels.x,
                        halfSizePixels.y)
                    * 1.50f
                    + 2f);

            int minX =
                Mathf.FloorToInt(
                    centerPixel.x - radius);

            int minY =
                Mathf.FloorToInt(
                    centerPixel.y - radius);

            int maxX =
                Mathf.CeilToInt(
                    centerPixel.x + radius);

            int maxY =
                Mathf.CeilToInt(
                    centerPixel.y + radius);

            int width =
                Mathf.Max(
                    1,
                    maxX - minX + 1);

            int height =
                Mathf.Max(
                    1,
                    maxY - minY + 1);

            interactionCompute.SetInt(
                "_Resolution",
                textureResolution);

            interactionCompute.SetInts(
                "_StampMinPixel",
                minX,
                minY);

            interactionCompute.SetInts(
                "_StampPixelSize",
                width,
                height);

            interactionCompute.SetVector(
                "_StampCenterPixel",
                new Vector4(
                    centerPixel.x,
                    centerPixel.y,
                    0f,
                    0f));

            interactionCompute.SetVector(
                "_StampRightForward",
                new Vector4(
                    right.x,
                    right.y,
                    forward.x,
                    forward.y));

            interactionCompute.SetVector(
                "_StampHalfSizePixels",
                new Vector4(
                    halfSizePixels.x,
                    halfSizePixels.y,
                    0f,
                    0f));

            interactionCompute.SetFloat(
                "_StampStrength",
                Mathf.Clamp01(strength));

            interactionCompute.SetFloat(
                "_StampEdgeSoftness",
                Mathf.Clamp(
                    edgeSoftness,
                    .01f,
                    .45f));

            interactionCompute.SetFloat(
                "_FootprintSide",
                leftFoot ? -1f : 1f);

            interactionCompute.SetVector(
                "_FootprintShapeParams",
                new Vector4(
                    footprintHeelWidth,
                    footprintMidWidth,
                    footprintToeWidth,
                    footprintArchInset));

            interactionCompute.SetVector(
                "_FootprintPressureParams",
                new Vector4(
                    footprintPressureContrast,
                    Mathf.Clamp(shapeVariation, -1f, 1f),
                    0f,
                    0f));

            interactionCompute.SetTexture(
                stampKernel,
                "_Destination",
                currentMask);

            interactionCompute.Dispatch(
                stampKernel,
                Mathf.CeilToInt(width / 8f),
                Mathf.CeilToInt(height / 8f),
                1);

            return true;
        }

        public bool StampTrack(
            Vector3 worldPosition,
            Vector3 forwardWS,
            Vector2 trackSize,
            float strength = 1f,
            float edgeSoftness = .16f,
            bool usePatternedTread = true,
            float treadRepeat = -1f,
            float treadContrast = -1f,
            float centerGrooveWidth = -1f,
            float chevronAmount = -1f)
        {
            if (!CanStamp)
                return false;

            Vector2 uv =
                (new Vector2(
                    worldPosition.x,
                    worldPosition.z)
                 - worldOrigin)
                /
                Mathf.Max(
                    .001f,
                    worldSize);

            if (uv.x <= 0f
                || uv.y <= 0f
                || uv.x >= 1f
                || uv.y >= 1f)
            {
                return false;
            }

            Vector2 forward =
                new Vector2(
                    forwardWS.x,
                    forwardWS.z);

            if (forward.sqrMagnitude <= .0001f)
                forward = Vector2.up;
            else
                forward.Normalize();

            Vector2 right =
                new Vector2(
                    forward.y,
                    -forward.x);

            Vector2 centerPixel =
                uv
                * textureResolution;

            Vector2 halfSizePixels =
                new Vector2(
                    Mathf.Max(.04f, trackSize.x),
                    Mathf.Max(.06f, trackSize.y))
                * .5f
                /
                texelWorldSize;

            float radius =
                Mathf.Ceil(
                    Mathf.Max(
                        halfSizePixels.x,
                        halfSizePixels.y)
                    * 1.35f
                    + 2f);

            int minX =
                Mathf.FloorToInt(
                    centerPixel.x - radius);

            int minY =
                Mathf.FloorToInt(
                    centerPixel.y - radius);

            int maxX =
                Mathf.CeilToInt(
                    centerPixel.x + radius);

            int maxY =
                Mathf.CeilToInt(
                    centerPixel.y + radius);

            int width =
                Mathf.Max(
                    1,
                    maxX - minX + 1);

            int height =
                Mathf.Max(
                    1,
                    maxY - minY + 1);

            interactionCompute.SetInt(
                "_Resolution",
                textureResolution);

            interactionCompute.SetInts(
                "_StampMinPixel",
                minX,
                minY);

            interactionCompute.SetInts(
                "_StampPixelSize",
                width,
                height);

            interactionCompute.SetVector(
                "_StampCenterPixel",
                new Vector4(
                    centerPixel.x,
                    centerPixel.y,
                    0f,
                    0f));

            interactionCompute.SetVector(
                "_StampRightForward",
                new Vector4(
                    right.x,
                    right.y,
                    forward.x,
                    forward.y));

            interactionCompute.SetVector(
                "_StampHalfSizePixels",
                new Vector4(
                    halfSizePixels.x,
                    halfSizePixels.y,
                    0f,
                    0f));

            interactionCompute.SetFloat(
                "_StampStrength",
                Mathf.Clamp01(strength));

            interactionCompute.SetFloat(
                "_StampEdgeSoftness",
                Mathf.Clamp(
                    edgeSoftness,
                    .01f,
                    .45f));


            float resolvedRepeat =
                treadRepeat > 0f
                    ? treadRepeat
                    : defaultTreadRepeat;

            float resolvedContrast =
                treadContrast >= 0f
                    ? treadContrast
                    : defaultTreadContrast;

            float resolvedCenterGroove =
                centerGrooveWidth >= 0f
                    ? centerGrooveWidth
                    : defaultCenterGrooveWidth;

            float resolvedChevron =
                chevronAmount >= 0f
                    ? chevronAmount
                    : defaultChevronAmount;

            float treadPeriodPixels =
                Mathf.Max(
                    2.5f,
                    Mathf.Max(.04f, resolvedRepeat)
                    / Mathf.Max(.0001f, texelWorldSize));

            // The phase is anchored in world space. Overlapping stamps therefore line up
            // instead of filling each other's grooves, which keeps the tread readable.
            Vector2 worldXZ =
                new Vector2(
                    worldPosition.x,
                    worldPosition.z);

            float treadPhasePixels =
                Mathf.Repeat(
                    Vector2.Dot(worldXZ, forward)
                    / Mathf.Max(.0001f, texelWorldSize),
                    treadPeriodPixels);

            interactionCompute.SetFloat(
                "_TrackPatternEnabled",
                patternedWheelTracks && usePatternedTread
                    ? 1f
                    : 0f);

            interactionCompute.SetVector(
                "_TrackTreadParams",
                new Vector4(
                    treadPeriodPixels,
                    Mathf.Clamp01(resolvedContrast),
                    Mathf.Clamp(resolvedCenterGroove, 0f, .28f),
                    Mathf.Clamp01(resolvedChevron)));

            interactionCompute.SetFloat(
                "_TrackTreadPhasePixels",
                treadPhasePixels);
            interactionCompute.SetTexture(
                trackStampKernel,
                "_Destination",
                currentMask);

            interactionCompute.Dispatch(
                trackStampKernel,
                Mathf.CeilToInt(width / 8f),
                Mathf.CeilToInt(height / 8f),
                1);

            return true;
        }

        public void ClearFootprints()
        {
            if (!initialized)
                return;

            DispatchClear(maskA);
            DispatchClear(maskB);

            currentMask = maskA;
            scratchMask = maskB;

            Publish();
        }

        private void ResolveReferences()
        {
            if (accumulation == null)
            {
                accumulation =
                    GetComponent<global::SnowAccumulationManager>();

                if (accumulation == null
                    && global::SeasonManager.Instance != null)
                {
                    accumulation =
                        global::SeasonManager.Instance.GetComponent<
                            global::SnowAccumulationManager>();
                }
            }

            if (trackingTarget == null)
            {
                Camera camera =
                    Camera.main;

                if (camera != null)
                {
                    CharacterController controller =
                        camera.GetComponentInParent<CharacterController>();

                    trackingTarget =
                        controller != null
                            ? controller.transform
                            : camera.transform;
                }
            }
        }

        private void Build()
        {
            if (initialized)
                return;

            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogError(
                    "SnowInteractionManager requires compute shader support.",
                    this);

                enabled = false;
                return;
            }

            if (interactionCompute == null)
            {
                Debug.LogError(
                    "SnowInteractionManager: Interaction Compute is not assigned.",
                    this);

                enabled = false;
                return;
            }

            textureResolution =
                Mathf.Clamp(
                    textureResolution,
                    256,
                    2048);

            // Keep clean power-of-two sizes for predictable memory and sampling.
            textureResolution =
                Mathf.ClosestPowerOfTwo(
                    textureResolution);

            worldSize =
                Mathf.Max(
                    8f,
                    worldSize);

            texelWorldSize =
                worldSize
                /
                textureResolution;

            clearKernel =
                interactionCompute.FindKernel(
                    "ClearMask");

            scrollKernel =
                interactionCompute.FindKernel(
                    "ScrollAndRecover");

            stampKernel =
                interactionCompute.FindKernel(
                    "StampFootprint");

            trackStampKernel =
                interactionCompute.FindKernel(
                    "StampTrack");

            maskA =
                CreateMask(
                    "Snow Interaction A");

            maskB =
                CreateMask(
                    "Snow Interaction B");

            currentMask = maskA;
            scratchMask = maskB;

            worldOrigin =
                CalculateDesiredOrigin();

            initialized = true;

            DispatchClear(maskA);
            DispatchClear(maskB);

            nextUpdateTime = 0f;

            Publish();
        }

        private RenderTexture CreateMask(
            string textureName)
        {
            RenderTexture texture =
                new RenderTexture(
                    textureResolution,
                    textureResolution,
                    0,
                    RenderTextureFormat.RHalf,
                    RenderTextureReadWrite.Linear)
                {
                    name = textureName,
                    enableRandomWrite = true,
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

            texture.Create();

            return texture;
        }

        private Vector2 CalculateDesiredOrigin()
        {
            Vector3 centre =
                trackingTarget != null
                    ? trackingTarget.position
                    : transform.position;

            float snappedX =
                Mathf.Round(
                    centre.x / texelWorldSize)
                * texelWorldSize;

            float snappedZ =
                Mathf.Round(
                    centre.z / texelWorldSize)
                * texelWorldSize;

            float half =
                worldSize * .5f;

            return
                new Vector2(
                    snappedX - half,
                    snappedZ - half);
        }

        private float CalculateRecoveryPerSecond()
        {
            float rate =
                Mathf.Max(
                    0f,
                    passiveRecoveryPerSecond);

            global::SeasonManager season =
                global::SeasonManager.Instance;

            if (season != null
                && global::WeatherRules.IsSnow(
                    season.currentWeather))
            {
                float multiplier =
                    season.currentWeather
                        == global::WeatherType.SnowStorm

                            ? snowStormFillMultiplier
                            : 1f;

                rate +=
                    Mathf.Max(
                        0f,
                        freshSnowFillPerSecond)
                    * Mathf.Clamp01(
                        season.WeatherIntensity)
                    * Mathf.Max(
                        1f,
                        multiplier);
            }

            return rate;
        }

        private void DispatchScrollAndRecover(
            int shiftX,
            int shiftY,
            float recoveryAmount)
        {
            interactionCompute.SetInt(
                "_Resolution",
                textureResolution);

            interactionCompute.SetInts(
                "_ShiftPixels",
                shiftX,
                shiftY);

            interactionCompute.SetFloat(
                "_RecoveryAmount",
                Mathf.Max(
                    0f,
                    recoveryAmount));

            interactionCompute.SetTexture(
                scrollKernel,
                "_Source",
                currentMask);

            interactionCompute.SetTexture(
                scrollKernel,
                "_Destination",
                scratchMask);

            int groups =
                Mathf.CeilToInt(
                    textureResolution / 8f);

            interactionCompute.Dispatch(
                scrollKernel,
                groups,
                groups,
                1);

            RenderTexture temporary =
                currentMask;

            currentMask =
                scratchMask;

            scratchMask =
                temporary;
        }

        private void DispatchClear(
            RenderTexture target)
        {
            if (target == null)
                return;

            interactionCompute.SetInt(
                "_Resolution",
                textureResolution);

            interactionCompute.SetTexture(
                clearKernel,
                "_Destination",
                target);

            int groups =
                Mathf.CeilToInt(
                    textureResolution / 8f);

            interactionCompute.Dispatch(
                clearKernel,
                groups,
                groups,
                1);
        }

        private void Publish()
        {
            if (!initialized
                || currentMask == null)
            {
                PublishDisabled();
                return;
            }

            Shader.SetGlobalTexture(
                InteractionTexId,
                currentMask);

            Shader.SetGlobalVector(
                InteractionWorldRectId,
                new Vector4(
                    worldOrigin.x,
                    worldOrigin.y,
                    1f / Mathf.Max(.001f, worldSize),
                    worldSize));

            Shader.SetGlobalVector(
                InteractionParamsId,
                new Vector4(
                    1f,
                    texelWorldSize,
                    Mathf.Clamp01(
                        snowCoverageCutStrength),
                    Mathf.Max(
                        0f,
                        visualDepressionDepth)));
        }

        private static void PublishDisabled()
        {
            Shader.SetGlobalTexture(
                InteractionTexId,
                Texture2D.blackTexture);

            Shader.SetGlobalVector(
                InteractionWorldRectId,
                Vector4.zero);

            Shader.SetGlobalVector(
                InteractionParamsId,
                Vector4.zero);
        }

        private void Release()
        {
            initialized = false;

            ReleaseTexture(ref maskA);
            ReleaseTexture(ref maskB);

            currentMask = null;
            scratchMask = null;

            clearKernel = -1;
            scrollKernel = -1;
            stampKernel = -1;
            trackStampKernel = -1;
        }

        private static void ReleaseTexture(
            ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();

            if (Application.isPlaying)
                Destroy(texture);
            else
                DestroyImmediate(texture);

            texture = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            textureResolution =
                Mathf.Clamp(
                    Mathf.ClosestPowerOfTwo(
                        Mathf.Max(
                            256,
                            textureResolution)),
                    256,
                    2048);

            worldSize =
                Mathf.Max(
                    8f,
                    worldSize);

            updateInterval =
                Mathf.Clamp(
                    updateInterval,
                    .02f,
                    .5f);


            defaultTreadRepeat =
                Mathf.Clamp(
                    defaultTreadRepeat,
                    .08f,
                    .35f);

            defaultTreadContrast =
                Mathf.Clamp01(
                    defaultTreadContrast);

            defaultCenterGrooveWidth =
                Mathf.Clamp(
                    defaultCenterGrooveWidth,
                    0f,
                    .28f);

            defaultChevronAmount =
                Mathf.Clamp01(
                    defaultChevronAmount);
        }
#endif
    }
}
