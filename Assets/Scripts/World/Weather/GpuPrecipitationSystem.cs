using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlotNRots.World.Weather
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class GpuPrecipitationSystem : MonoBehaviour
    {
        // ============================================================
        // GPU PARTICLE DATA
        // ============================================================

        [StructLayout(LayoutKind.Sequential)]
        private struct ParticleGpu
        {
            public Vector3 position;
            public Vector3 velocity;

            public float seed;
            public float age;
        }

        // ============================================================
        // GPU FIELD
        // ============================================================

        private sealed class Field : IDisposable
        {
            public readonly int maxCount;
            public readonly bool snow;

            public ComputeBuffer particles;
            public ComputeBuffer args;

            public Material material;

            public readonly uint[] argsData =
                new uint[5];

            public bool forceRespawn = true;
            public bool wasVisible;

            public Field(
                int count,
                bool isSnow)
            {
                maxCount = count;
                snow = isSnow;
            }

            public void Dispose()
            {
                if (particles != null)
                {
                    particles.Release();
                    particles = null;
                }

                if (args != null)
                {
                    args.Release();
                    args = null;
                }

                if (material != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(
                            material);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(
                            material);
                    }

                    material = null;
                }
            }
        }


        // ============================================================
        // REQUIRED ASSETS
        // ============================================================

        [Header("Required Assets")]

        [SerializeField]
        private ComputeShader simulationShader;

        [SerializeField]
        private Shader precipitationShader;

        [SerializeField]
        private Camera gameplayCamera;


        // ============================================================
        // PARTICLE BUDGETS
        // ============================================================

        [Header("PC Particle Budgets")]

        [SerializeField]
        [Range(2000, 30000)]
        private int maxRainParticles = 14000;

        [SerializeField]
        [Range(1000, 25000)]
        private int maxSnowParticles = 9000;


        // ============================================================
        // PRECIPITATION VOLUME
        // ============================================================

        [Header("Camera-Centred World Volumes")]

        [SerializeField]
        private Vector2 rainHorizontalRadius =
            new Vector2(
                36f,
                36f);

        [SerializeField]
        [Min(2f)]
        private float rainAboveCamera = 24f;

        [SerializeField]
        [Min(1f)]
        private float rainBelowCamera = 9f;

        [SerializeField]
        private Vector2 snowHorizontalRadius =
            new Vector2(
                44f,
                44f);

        [SerializeField]
        [Min(2f)]
        private float snowAboveCamera = 22f;

        [SerializeField]
        [Min(1f)]
        private float snowBelowCamera = 11f;


        // ============================================================
        // FAST MOVEMENT
        // ============================================================

        [Header("Fast Movement Prediction")]

        [SerializeField]
        [Min(0f)]
        private float movementLeadTime = .65f;

        [SerializeField]
        [Min(0f)]
        private float maximumLeadDistance = 32f;

        [SerializeField]
        [Min(.1f)]
        private float velocitySmoothing = 10f;

        [SerializeField]
        [Min(1f)]
        private float teleportResetDistance = 45f;

        [SerializeField]
        [Range(.25f, 2f)]
        private float rainLeadMultiplier = .85f;

        [SerializeField]
        [Range(.25f, 2f)]
        private float snowLeadMultiplier = 1.20f;


        // ============================================================
        // WORLD COLLISION
        // ============================================================

        [Header("World Collision Field")]

        [SerializeField]
        private bool enableWorldCollision = true;

        [SerializeField]
        private LayerMask precipitationCollisionMask = -1;

        [Tooltip(
            "Player capsule/body root. " +
            "Player collider is ignored so it does not behave like an umbrella.")]
        [SerializeField]
        private Transform collisionIgnoreRoot;

        [SerializeField]
        [Range(16, 64)]
        private int collisionGridResolution = 32;

        [SerializeField]
        [Min(6f)]
        private float collisionHalfSize = 16f;

        [Tooltip(
            "Collision refresh when standing/walking slowly.")]
        [SerializeField]
        [Min(.02f)]
        private float collisionUpdateInterval = .10f;

        [Tooltip(
            "Collision refresh while moving quickly in vehicles.")]
        [SerializeField]
        [Min(.02f)]
        private float fastCollisionUpdateInterval = .045f;

        [SerializeField]
        [Min(1f)]
        private float fastMovementThreshold = 10f;

        [SerializeField]
        [Min(4f)]
        private float collisionScanAboveCamera = 35f;

        [SerializeField]
        [Min(4f)]
        private float collisionScanBelowCamera = 20f;

        [SerializeField]
        [Range(0f, .25f)]
        private float collisionSurfacePadding = .04f;


        // ============================================================
        // NORMAL PRECIPITATION
        // ============================================================

        [Header("Normal Precipitation")]

        [SerializeField]
        [Min(.1f)]
        private float rainFallSpeed = 26f;

        [SerializeField]
        [Min(.1f)]
        private float snowFallSpeed = 3.6f;

        [SerializeField]
        [Min(0f)]
        private float normalSnowTurbulence = 1.4f;


        // ============================================================
        // STORM
        // ============================================================

        [Header("Storm Precipitation")]

        [SerializeField]
        [Min(1f)]
        private float stormRainSpeedMultiplier = 1.55f;

        [SerializeField]
        [Min(1f)]
        private float stormRainDensityMultiplier = 1.35f;

        [SerializeField]
        [Min(1f)]
        private float blizzardSnowSpeedMultiplier = 2.15f;

        [SerializeField]
        [Min(1f)]
        private float blizzardSnowDensityMultiplier = 1.65f;

        [SerializeField]
        [Min(0f)]
        private float blizzardTurbulence = 6.5f;

        [SerializeField]
        [Min(1f)]
        private float blizzardWindMultiplier = 1.65f;


        // ============================================================
        // MAIN PARTICLE VISUALS
        // ============================================================

        [Header("Visual Size")]

        [SerializeField]
        [Min(.001f)]
        private float rainWidth = .035f;

        [SerializeField]
        [Min(.05f)]
        private float rainLength = 1.7f;

        [SerializeField]
        private Vector2 snowSizeRange =
            new Vector2(
                .075f,
                .22f);

        [SerializeField]
        private Color rainColor =
            new Color(
                .72f,
                .82f,
                .92f,
                .72f);

        [SerializeField]
        private Color snowColor =
            new Color(
                .96f,
                .985f,
                1f,
                .92f);

        [SerializeField]
        [Range(.01f, 2f)]
        private float softParticleDistance = .28f;


        // ============================================================
        // RAIN SPLASHES
        // ============================================================

        [Header("Rain Ground Splashes")]

        [SerializeField]
        private bool enableRainSplashes = true;

        [SerializeField]
        private Shader rainSplashShader;

        [SerializeField]
        [Range(0f, 200f)]
        private float normalSplashRate = 48f;

        [SerializeField]
        [Range(0f, 300f)]
        private float stormSplashRate = 105f;

        [SerializeField]
        [Min(2f)]
        private float splashRadius = 13f;

        [SerializeField]
        [Range(.01f, .2f)]
        private float splashSizeMin = .025f;

        [SerializeField]
        [Range(.01f, .25f)]
        private float splashSizeMax = .055f;

        [SerializeField]
        private Vector2 splashLifetime =
            new Vector2(
                .18f,
                .34f);

        [SerializeField]
        private Vector2 splashUpSpeed =
            new Vector2(
                .65f,
                1.35f);

        [SerializeField]
        private Vector2 splashSideSpeed =
            new Vector2(
                .22f,
                .85f);

        [SerializeField]
        [Range(0f, 10f)]
        private float splashGravity = 4.5f;

        [SerializeField]
        [Range(128, 4096)]
        private int splashMaxParticles = 1200;

        [SerializeField]
        [Range(1, 6)]
        private int splashDropletsPerImpact = 3;

        [SerializeField]
        [Range(0f, 1f)]
        private float splashMinimumUpNormal = .32f;


        // ============================================================
        // RUNTIME PRECIPITATION
        // ============================================================

        private Mesh quad;

        private Field rainField;
        private Field snowField;

        private int kernel = -1;

        private uint frameSeed;

        private WeatherType weather =
            WeatherType.Sunny;

        private float rainAmount;
        private float snowAmount;

        private Vector3 windVelocity;


        // ============================================================
        // CAMERA MOVEMENT
        // ============================================================

        private bool movementInitialized;

        private Vector3 previousCameraPosition;

        private Vector3 smoothedCameraVelocity;


        // ============================================================
        // COLLISION FIELD
        // ============================================================

        private ComputeBuffer collisionBuffer;

        private Vector4[] collisionCells;

        private int collisionResolutionRuntime;

        private float collisionCellSize;

        private Vector3 collisionOrigin;

        private float nextCollisionUpdateTime;

        private int validCollisionCellCount;

        private readonly RaycastHit[] collisionHits =
            new RaycastHit[16];


        // ============================================================
        // SPLASH SYSTEM
        // ============================================================

        private ParticleSystem splashParticles;

        private ParticleSystemRenderer splashRenderer;

        private Material splashMaterial;

        private float splashAccumulator;


        // ============================================================
        // SHADER IDS
        // ============================================================

        private static readonly int ParticlesId =
            Shader.PropertyToID(
                "_Particles");

        private static readonly int ModeId =
            Shader.PropertyToID(
                "_Mode");

        private static readonly int RainWidthId =
            Shader.PropertyToID(
                "_RainWidth");

        private static readonly int RainLengthId =
            Shader.PropertyToID(
                "_RainLength");

        private static readonly int SnowSizeId =
            Shader.PropertyToID(
                "_SnowSizeRange");

        private static readonly int RainColorId =
            Shader.PropertyToID(
                "_RainColor");

        private static readonly int SnowColorId =
            Shader.PropertyToID(
                "_SnowColor");

        private static readonly int SoftDistanceId =
            Shader.PropertyToID(
                "_SoftParticleDistance");


        // ============================================================
        // PUBLIC
        // ============================================================

        public bool IsReady =>
            kernel >= 0
            &&
            rainField != null
            &&
            snowField != null;

        public int RainVisibleCount
        {
            get;
            private set;
        }

        public int SnowVisibleCount
        {
            get;
            private set;
        }

        public int ValidCollisionCellCount =>
            validCollisionCellCount;

        public float CollisionCellSize =>
            collisionCellSize;


        // ============================================================
        // UNITY
        // ============================================================

        private void OnEnable()
        {
            ResolveCamera();

            ResolveCollisionIgnoreRoot();

            movementInitialized = false;

            previousCameraPosition =
                Vector3.zero;

            smoothedCameraVelocity =
                Vector3.zero;

            Build();
        }

        private void OnDisable()
        {
            Release();
        }

        private void OnDestroy()
        {
            Release();
        }


        // ============================================================
        // WEATHER INPUT
        // ============================================================

        public void SetState(
            WeatherType currentWeather,
            float rain,
            float snow,
            Vector3 wind)
        {
            rain =
                Mathf.Clamp01(
                    rain);

            snow =
                Mathf.Clamp01(
                    snow);

            bool rainStarting =
                rainAmount <= .001f
                &&
                rain > .001f;

            bool snowStarting =
                snowAmount <= .001f
                &&
                snow > .001f;

            weather =
                currentWeather;

            rainAmount =
                rain;

            snowAmount =
                snow;

            windVelocity =
                wind;

            if (rainStarting
                &&
                rainField != null)
            {
                rainField.forceRespawn =
                    true;

                nextCollisionUpdateTime =
                    0f;
            }

            if (snowStarting
                &&
                snowField != null)
            {
                snowField.forceRespawn =
                    true;

                nextCollisionUpdateTime =
                    0f;
            }
        }


        // ============================================================
        // MAIN UPDATE
        // ============================================================

        private void LateUpdate()
        {
            ResolveCamera();

            if (gameplayCamera == null
                ||
                !IsReady)
            {
                return;
            }

            Vector3 cameraPosition =
                gameplayCamera.transform.position;

            Vector3 movementLead =
                CalculateMovementLead(
                    cameraPosition);

            UpdateCollisionField(
                cameraPosition,
                smoothedCameraVelocity.magnitude);

            Vector3 rainAnchor =
                cameraPosition
                +
                movementLead
                *
                rainLeadMultiplier;

            Vector3 snowAnchor =
                cameraPosition
                +
                movementLead
                *
                snowLeadMultiplier;

            bool storm =
                weather
                ==
                WeatherType.Storm;

            bool blizzard =
                weather
                ==
                WeatherType.SnowStorm;


            // --------------------------------------------------------
            // RAIN
            // --------------------------------------------------------

            Simulate(
                rainField,
                rainAnchor,
                rainHorizontalRadius,
                rainAboveCamera,
                rainBelowCamera,

                rainFallSpeed
                *
                (
                    storm
                        ?
                        stormRainSpeedMultiplier
                        :
                        1f
                ),

                0f,

                windVelocity,

                rainAmount);


            // --------------------------------------------------------
            // SNOW
            // --------------------------------------------------------

            Simulate(
                snowField,
                snowAnchor,
                snowHorizontalRadius,
                snowAboveCamera,
                snowBelowCamera,

                snowFallSpeed
                *
                (
                    blizzard
                        ?
                        blizzardSnowSpeedMultiplier
                        :
                        1f
                ),

                blizzard
                    ?
                    blizzardTurbulence
                    :
                    normalSnowTurbulence,

                windVelocity
                *
                (
                    blizzard
                        ?
                        blizzardWindMultiplier
                        :
                        1f
                ),

                snowAmount);


            // --------------------------------------------------------
            // ACTIVE PARTICLES
            // --------------------------------------------------------

            RainVisibleCount =
                ActiveCount(
                    rainField.maxCount,
                    rainAmount,

                    storm
                        ?
                        stormRainDensityMultiplier
                        :
                        1f,

                    .13f,
                    .72f);

            SnowVisibleCount =
                ActiveCount(
                    snowField.maxCount,
                    snowAmount,

                    blizzard
                        ?
                        blizzardSnowDensityMultiplier
                        :
                        1f,

                    .12f,
                    .74f);


            // --------------------------------------------------------
            // DRAW
            // --------------------------------------------------------

            Draw(
                rainField,
                RainVisibleCount,
                rainAnchor,
                rainHorizontalRadius,
                rainAboveCamera,
                rainBelowCamera);

            Draw(
                snowField,
                SnowVisibleCount,
                snowAnchor,
                snowHorizontalRadius,
                snowAboveCamera,
                snowBelowCamera);


            // --------------------------------------------------------
            // SPLASH
            // --------------------------------------------------------

            UpdateRainSplashes(
                cameraPosition,
                storm);
        }


        // ============================================================
        // MOVEMENT PREDICTION
        // ============================================================

        private Vector3 CalculateMovementLead(
            Vector3 cameraPosition)
        {
            float dt =
                Time.deltaTime;

            if (!movementInitialized
                ||
                dt <= 0f)
            {
                movementInitialized =
                    true;

                previousCameraPosition =
                    cameraPosition;

                smoothedCameraVelocity =
                    Vector3.zero;

                return Vector3.zero;
            }

            Vector3 delta =
                cameraPosition
                -
                previousCameraPosition;

            previousCameraPosition =
                cameraPosition;

            if (delta.sqrMagnitude
                >
                teleportResetDistance
                *
                teleportResetDistance)
            {
                smoothedCameraVelocity =
                    Vector3.zero;

                if (rainField != null)
                    rainField.forceRespawn =
                        true;

                if (snowField != null)
                    snowField.forceRespawn =
                        true;

                nextCollisionUpdateTime =
                    0f;

                return Vector3.zero;
            }

            Vector3 rawVelocity =
                delta
                /
                Mathf.Max(
                    .0001f,
                    dt);

            rawVelocity.y =
                0f;

            float blend =
                1f
                -
                Mathf.Exp(
                    -velocitySmoothing
                    *
                    dt);

            smoothedCameraVelocity =
                Vector3.Lerp(
                    smoothedCameraVelocity,
                    rawVelocity,
                    blend);

            Vector3 lead =
                smoothedCameraVelocity
                *
                movementLeadTime;

            float maxLeadSqr =
                maximumLeadDistance
                *
                maximumLeadDistance;

            if (lead.sqrMagnitude
                >
                maxLeadSqr)
            {
                lead =
                    lead.normalized
                    *
                    maximumLeadDistance;
            }

            return lead;
        }


        // ============================================================
        // BUILD
        // ============================================================

        private void Build()
        {
            if (rainField != null
                ||
                snowField != null)
            {
                return;
            }

            if (!SystemInfo.supportsComputeShaders)
            {
                Debug.LogError(
                    "GPU precipitation requires compute-shader support.",
                    this);

                enabled = false;

                return;
            }

            if (simulationShader == null)
            {
                Debug.LogError(
                    "GpuPrecipitationSystem: Simulation Shader is not assigned.",
                    this);

                enabled = false;

                return;
            }

            if (precipitationShader == null)
            {
                precipitationShader =
                    Shader.Find(
                        "Plots & Rots/GPU Precipitation");
            }

            if (precipitationShader == null)
            {
                Debug.LogError(
                    "GpuPrecipitationSystem: GPU Precipitation shader not found.",
                    this);

                enabled = false;

                return;
            }

            kernel =
                simulationShader.FindKernel(
                    "CSMain");

            quad =
                BuildQuad();

            rainField =
                BuildField(
                    Mathf.Max(
                        1,
                        maxRainParticles),
                    false);

            snowField =
                BuildField(
                    Mathf.Max(
                        1,
                        maxSnowParticles),
                    true);

            BuildCollisionField();

            BuildSplashSystem();
        }


        // ============================================================
        // FIELD CREATION
        // ============================================================

        private Field BuildField(
            int count,
            bool snow)
        {
            Field field =
                new Field(
                    count,
                    snow);

            field.particles =
                new ComputeBuffer(
                    count,
                    Marshal.SizeOf<ParticleGpu>(),
                    ComputeBufferType.Structured);

            field.args =
                new ComputeBuffer(
                    1,
                    sizeof(uint) * 5,
                    ComputeBufferType.IndirectArguments);

            field.material =
                new Material(
                    precipitationShader)
                {
                    name =
                        snow
                            ?
                            "GPU Snow (Runtime)"
                            :
                            "GPU Rain (Runtime)",

                    enableInstancing =
                        true
                };

            ParticleGpu[] initial =
                new ParticleGpu[count];

            System.Random random =
                new System.Random(
                    snow
                        ?
                        93173
                        :
                        41771);

            for (int i = 0;
                 i < initial.Length;
                 i++)
            {
                initial[i].position =
                    Vector3.zero;

                initial[i].velocity =
                    Vector3.zero;

                initial[i].seed =
                    (float)random.NextDouble();

                initial[i].age =
                    -1f;
            }

            field.particles.SetData(
                initial);

            field.argsData[0] =
                quad.GetIndexCount(0);

            field.argsData[1] =
                0;

            field.argsData[2] =
                quad.GetIndexStart(0);

            field.argsData[3] =
                (uint)quad.GetBaseVertex(0);

            field.argsData[4] =
                0;

            field.args.SetData(
                field.argsData);

            ConfigureMaterial(
                field);

            return field;
        }


        // ============================================================
        // MATERIAL
        // ============================================================

        private void ConfigureMaterial(
            Field field)
        {
            field.material.SetInt(
                ModeId,
                field.snow
                    ?
                    1
                    :
                    0);

            field.material.SetFloat(
                RainWidthId,
                rainWidth);

            field.material.SetFloat(
                RainLengthId,
                rainLength);

            float snowMin =
                Mathf.Min(
                    snowSizeRange.x,
                    snowSizeRange.y);

            float snowMax =
                Mathf.Max(
                    snowSizeRange.x,
                    snowSizeRange.y);

            field.material.SetVector(
                SnowSizeId,
                new Vector4(
                    snowMin,
                    snowMax,
                    0f,
                    0f));

            field.material.SetColor(
                RainColorId,
                rainColor);

            field.material.SetColor(
                SnowColorId,
                snowColor);

            field.material.SetFloat(
                SoftDistanceId,
                softParticleDistance);

            field.material.SetBuffer(
                ParticlesId,
                field.particles);
        }


        // ============================================================
        // COLLISION FIELD CREATION
        // ============================================================

        private void BuildCollisionField()
        {
            collisionResolutionRuntime =
                Mathf.Clamp(
                    collisionGridResolution,
                    16,
                    64);

            int count =
                collisionResolutionRuntime
                *
                collisionResolutionRuntime;

            collisionCells =
                new Vector4[count];

            for (int i = 0;
                 i < collisionCells.Length;
                 i++)
            {
                collisionCells[i] =
                    InvalidCollisionCell();
            }

            collisionBuffer =
                new ComputeBuffer(
                    count,
                    sizeof(float) * 4,
                    ComputeBufferType.Structured);

            collisionBuffer.SetData(
                collisionCells);

            collisionCellSize =
                (
                    collisionHalfSize
                    *
                    2f
                )
                /
                collisionResolutionRuntime;

            validCollisionCellCount =
                0;

            nextCollisionUpdateTime =
                0f;
        }


        // ============================================================
        // COLLISION FIELD UPDATE
        // ============================================================

        private void UpdateCollisionField(
            Vector3 cameraPosition,
            float horizontalSpeed)
        {
            if (!enableWorldCollision
                ||
                collisionBuffer == null
                ||
                collisionCells == null)
            {
                return;
            }

            if (rainAmount <= .001f
                &&
                snowAmount <= .001f)
            {
                return;
            }

            float speedT =
                Mathf.Clamp01(
                    horizontalSpeed
                    /
                    Mathf.Max(
                        1f,
                        fastMovementThreshold));

            float interval =
                Mathf.Lerp(
                    collisionUpdateInterval,
                    fastCollisionUpdateInterval,
                    speedT);

            if (Time.unscaledTime
                <
                nextCollisionUpdateTime)
            {
                return;
            }

            nextCollisionUpdateTime =
                Time.unscaledTime
                +
                Mathf.Max(
                    .02f,
                    interval);

            collisionCellSize =
                (
                    collisionHalfSize
                    *
                    2f
                )
                /
                collisionResolutionRuntime;

            collisionOrigin =
                new Vector3(
                    cameraPosition.x
                        -
                        collisionHalfSize,

                    cameraPosition.y,

                    cameraPosition.z
                        -
                        collisionHalfSize);

            float rayStartY =
                cameraPosition.y
                +
                collisionScanAboveCamera;

            float rayDistance =
                collisionScanAboveCamera
                +
                collisionScanBelowCamera;

            validCollisionCellCount =
                0;


            for (int z = 0;
                 z < collisionResolutionRuntime;
                 z++)
            {
                float worldZ =
                    collisionOrigin.z
                    +
                    (
                        z
                        +
                        .5f
                    )
                    *
                    collisionCellSize;


                for (int x = 0;
                     x < collisionResolutionRuntime;
                     x++)
                {
                    float worldX =
                        collisionOrigin.x
                        +
                        (
                            x
                            +
                            .5f
                        )
                        *
                        collisionCellSize;

                    Vector3 origin =
                        new Vector3(
                            worldX,
                            rayStartY,
                            worldZ);

                    int hitCount =
                        Physics.RaycastNonAlloc(
                            origin,
                            Vector3.down,
                            collisionHits,
                            rayDistance,
                            precipitationCollisionMask,
                            QueryTriggerInteraction.Ignore);

                    bool found =
                        false;

                    float bestDistance =
                        float.PositiveInfinity;

                    RaycastHit bestHit =
                        default;


                    for (int i = 0;
                         i < hitCount;
                         i++)
                    {
                        RaycastHit hit =
                            collisionHits[i];

                        if (hit.collider == null)
                            continue;

                        if (ShouldIgnoreCollision(
                                hit.collider.transform))
                        {
                            continue;
                        }

                        if (hit.distance
                            <
                            bestDistance)
                        {
                            bestDistance =
                                hit.distance;

                            bestHit =
                                hit;

                            found =
                                true;
                        }
                    }


                    int index =
                        z
                        *
                        collisionResolutionRuntime
                        +
                        x;


                    if (found)
                    {
                        Vector3 normal =
                            bestHit.normal.sqrMagnitude
                            >
                            .0001f

                                ?
                                bestHit.normal.normalized

                                :
                                Vector3.up;

                        collisionCells[index] =
                            new Vector4(
                                bestHit.point.y,
                                normal.x,
                                normal.y,
                                normal.z);

                        validCollisionCellCount++;
                    }
                    else
                    {
                        collisionCells[index] =
                            InvalidCollisionCell();
                    }
                }
            }

            collisionBuffer.SetData(
                collisionCells);
        }


        private bool ShouldIgnoreCollision(
            Transform hitTransform)
        {
            if (collisionIgnoreRoot == null
                ||
                hitTransform == null)
            {
                return false;
            }

            return
                hitTransform
                    ==
                    collisionIgnoreRoot
                ||
                hitTransform.IsChildOf(
                    collisionIgnoreRoot);
        }


        private static Vector4 InvalidCollisionCell()
        {
            return
                new Vector4(
                    -100000f,
                    0f,
                    1f,
                    0f);
        }


        // ============================================================
        // GPU SIMULATION
        // ============================================================

        private void Simulate(
            Field field,
            Vector3 anchor,
            Vector2 horizontalRadius,
            float above,
            float below,
            float fallSpeed,
            float turbulence,
            Vector3 wind,
            float amount)
        {
            if (field == null)
                return;

            bool visible =
                amount > .001f;

            if (!visible)
            {
                field.wasVisible =
                    false;

                return;
            }

            if (!field.wasVisible)
            {
                field.forceRespawn =
                    true;
            }

            field.wasVisible =
                true;


            simulationShader.SetInt(
                "_ParticleCount",
                field.maxCount);

            simulationShader.SetVector(
                "_Anchor",
                anchor);

            simulationShader.SetVector(
                "_HorizontalRadius",
                new Vector4(
                    Mathf.Max(
                        1f,
                        horizontalRadius.x),

                    Mathf.Max(
                        1f,
                        horizontalRadius.y),

                    0f,
                    0f));

            simulationShader.SetFloat(
                "_Above",
                Mathf.Max(
                    1f,
                    above));

            simulationShader.SetFloat(
                "_Below",
                Mathf.Max(
                    1f,
                    below));

            simulationShader.SetVector(
                "_Wind",
                wind);

            simulationShader.SetFloat(
                "_FallSpeed",
                Mathf.Max(
                    .1f,
                    fallSpeed));

            simulationShader.SetFloat(
                "_Turbulence",
                Mathf.Max(
                    0f,
                    turbulence));

            simulationShader.SetFloat(
                "_DeltaTime",
                Mathf.Min(
                    Time.deltaTime,
                    .05f));

            simulationShader.SetFloat(
                "_TimeValue",
                Time.time);

            simulationShader.SetInt(
                "_Mode",
                field.snow
                    ?
                    1
                    :
                    0);

            simulationShader.SetInt(
                "_RespawnAll",
                field.forceRespawn
                    ?
                    1
                    :
                    0);

            simulationShader.SetInt(
                "_FrameSeed",
                unchecked(
                    (int)++frameSeed));


            // --------------------------------------------------------
            // COLLISION DATA
            // --------------------------------------------------------

            bool collisionActive =
                enableWorldCollision
                &&
                collisionBuffer != null
                &&
                validCollisionCellCount > 0;

            simulationShader.SetInt(
                "_CollisionEnabled",
                collisionActive
                    ?
                    1
                    :
                    0);

            simulationShader.SetInt(
                "_CollisionGridResolution",
                collisionResolutionRuntime);

            simulationShader.SetVector(
                "_CollisionOrigin",
                new Vector4(
                    collisionOrigin.x,
                    collisionOrigin.y,
                    collisionOrigin.z,
                    0f));

            simulationShader.SetFloat(
                "_CollisionCellSize",
                Mathf.Max(
                    .01f,
                    collisionCellSize));

            simulationShader.SetFloat(
                "_CollisionSurfacePadding",
                collisionSurfacePadding);

            if (collisionBuffer != null)
            {
                simulationShader.SetBuffer(
                    kernel,
                    "_CollisionCells",
                    collisionBuffer);
            }


            simulationShader.SetBuffer(
                kernel,
                "_Particles",
                field.particles);


            int groups =
                Mathf.CeilToInt(
                    field.maxCount
                    /
                    256f);

            simulationShader.Dispatch(
                kernel,
                groups,
                1,
                1);

            field.forceRespawn =
                false;
        }


        // ============================================================
        // DRAW
        // ============================================================

        private void Draw(
            Field field,
            int visibleCount,
            Vector3 anchor,
            Vector2 radius,
            float above,
            float below)
        {
            if (field == null
                ||
                visibleCount <= 0
                ||
                field.material == null
                ||
                field.args == null)
            {
                return;
            }

            field.argsData[1] =
                (uint)Mathf.Clamp(
                    visibleCount,
                    0,
                    field.maxCount);

            field.args.SetData(
                field.argsData);

            float x =
                Mathf.Max(
                    1f,
                    radius.x)
                *
                2f
                +
                12f;

            float z =
                Mathf.Max(
                    1f,
                    radius.y)
                *
                2f
                +
                12f;

            float y =
                Mathf.Max(
                    1f,
                    above + below)
                +
                12f;

            Bounds bounds =
                new Bounds(
                    anchor
                    +
                    Vector3.up
                    *
                    (
                        (above - below)
                        *
                        .5f
                    ),

                    new Vector3(
                        x,
                        y,
                        z));

            Graphics.DrawMeshInstancedIndirect(
                quad,
                0,
                field.material,
                bounds,
                field.args,
                0,
                null,
                ShadowCastingMode.Off,
                false,
                gameObject.layer,
                gameplayCamera,
                LightProbeUsage.Off,
                null);
        }


        // ============================================================
        // SPLASH SYSTEM CREATION
        // ============================================================

        private void BuildSplashSystem()
        {
            if (!enableRainSplashes)
                return;

            if (rainSplashShader == null)
            {
                rainSplashShader =
                    Shader.Find(
                        "Plots & Rots/Rain Splash");
            }

            if (rainSplashShader == null)
            {
                Debug.LogWarning(
                    "Rain Splash shader not found. Collision works but splash is disabled.",
                    this);

                return;
            }


            GameObject splashObject =
                new GameObject(
                    "Rain Splash Particles");

            splashObject.transform.SetParent(
                transform,
                false);


            splashParticles =
                splashObject.AddComponent<
                    ParticleSystem>();

            splashRenderer =
                splashObject.GetComponent<
                    ParticleSystemRenderer>();


            ParticleSystem.MainModule main =
                splashParticles.main;

            main.loop =
                false;

            main.playOnAwake =
                false;

            main.simulationSpace =
                ParticleSystemSimulationSpace.World;

            main.maxParticles =
                splashMaxParticles;

            main.startSpeed =
                0f;

            main.startLifetime =
                new ParticleSystem.MinMaxCurve(
                    Mathf.Min(
                        splashLifetime.x,
                        splashLifetime.y),

                    Mathf.Max(
                        splashLifetime.x,
                        splashLifetime.y));

            main.startSize =
                new ParticleSystem.MinMaxCurve(
                    Mathf.Min(
                        splashSizeMin,
                        splashSizeMax),

                    Mathf.Max(
                        splashSizeMin,
                        splashSizeMax));

            main.gravityModifier =
                splashGravity;


            ParticleSystem.EmissionModule emission =
                splashParticles.emission;

            emission.enabled =
                false;


            ParticleSystem.ShapeModule shape =
                splashParticles.shape;

            shape.enabled =
                false;


            ParticleSystem.CollisionModule collision =
                splashParticles.collision;

            collision.enabled =
                false;


            splashMaterial =
                new Material(
                    rainSplashShader)
                {
                    name =
                        "Rain Splash (Runtime)"
                };


            splashRenderer.sharedMaterial =
                splashMaterial;

            splashRenderer.renderMode =
                ParticleSystemRenderMode.Billboard;

            splashRenderer.shadowCastingMode =
                ShadowCastingMode.Off;

            splashRenderer.receiveShadows =
                false;


            // Particle System keeps simulating;
            // emission itself is manual.
            splashParticles.Play(
                false);
        }


        // ============================================================
        // SPLASH UPDATE
        // ============================================================

        private void UpdateRainSplashes(
            Vector3 cameraPosition,
            bool storm)
        {
            if (!enableRainSplashes
                ||
                splashParticles == null
                ||
                rainAmount <= .015f
                ||
                validCollisionCellCount <= 0)
            {
                splashAccumulator =
                    0f;

                return;
            }

            float rate =
                storm
                    ?
                    stormSplashRate
                    :
                    normalSplashRate;

            splashAccumulator +=
                rate
                *
                rainAmount
                *
                Time.deltaTime;


            int impactEventsThisFrame =
                0;

            const int maximumImpactEventsPerFrame =
                14;


            while (splashAccumulator >= 1f
                &&
                impactEventsThisFrame
                    <
                    maximumImpactEventsPerFrame)
            {
                splashAccumulator -=
                    1f;

                impactEventsThisFrame++;


                if (!TryGetRandomSplashSurface(
                        cameraPosition,
                        out Vector3 position,
                        out Vector3 normal))
                {
                    continue;
                }

                EmitSplash(
                    position,
                    normal,
                    storm);
            }
        }


        // ============================================================
        // RANDOM SPLASH SURFACE
        // ============================================================

        private bool TryGetRandomSplashSurface(
            Vector3 cameraPosition,
            out Vector3 position,
            out Vector3 normal)
        {
            position =
                default;

            normal =
                Vector3.up;


            if (collisionCells == null
                ||
                collisionCells.Length == 0)
            {
                return false;
            }


            float maxDistanceSqr =
                splashRadius
                *
                splashRadius;


            for (int attempt = 0;
                 attempt < 12;
                 attempt++)
            {
                int x =
                    UnityEngine.Random.Range(
                        0,
                        collisionResolutionRuntime);

                int z =
                    UnityEngine.Random.Range(
                        0,
                        collisionResolutionRuntime);

                int index =
                    z
                    *
                    collisionResolutionRuntime
                    +
                    x;


                Vector4 cell =
                    collisionCells[index];

                if (cell.x < -9999f)
                    continue;


                Vector3 candidate =
                    new Vector3(
                        collisionOrigin.x
                        +
                        (
                            x + .5f
                        )
                        *
                        collisionCellSize,

                        cell.x
                        +
                        collisionSurfacePadding
                        +
                        .018f,

                        collisionOrigin.z
                        +
                        (
                            z + .5f
                        )
                        *
                        collisionCellSize);


                Vector3 flatDelta =
                    candidate
                    -
                    cameraPosition;

                flatDelta.y =
                    0f;


                if (flatDelta.sqrMagnitude
                    >
                    maxDistanceSqr)
                {
                    continue;
                }


                Vector3 candidateNormal =
                    new Vector3(
                        cell.y,
                        cell.z,
                        cell.w);


                if (candidateNormal.sqrMagnitude
                    <
                    .0001f)
                {
                    candidateNormal =
                        Vector3.up;
                }
                else
                {
                    candidateNormal.Normalize();
                }


                if (candidateNormal.y
                    <
                    splashMinimumUpNormal)
                {
                    continue;
                }


                position =
                    candidate;

                normal =
                    candidateNormal;

                return true;
            }


            return false;
        }


        // ============================================================
        // EMIT SPLASH
        // ============================================================

        private void EmitSplash(
            Vector3 position,
            Vector3 normal,
            bool storm)
        {
            if (splashParticles == null)
                return;


            float stormMultiplier =
                storm
                    ?
                    1.28f
                    :
                    1f;


            int dropletCount =
                Mathf.Clamp(
                    splashDropletsPerImpact
                    +
                    (
                        storm
                            ?
                            1
                            :
                            0
                    ),
                    1,
                    6);


            // Small impact spot.
            ParticleSystem.EmitParams impact =
                new ParticleSystem.EmitParams
                {
                    position =
                        position
                        +
                        normal * .012f,

                    velocity =
                        normal * .05f,

                    startLifetime =
                        .10f,

                    startSize =
                        Mathf.Lerp(
                            splashSizeMin,
                            splashSizeMax,
                            .8f)
                        *
                        1.45f,

                    startColor =
                        new Color(
                            rainColor.r,
                            rainColor.g,
                            rainColor.b,

                            Mathf.Min(
                                1f,
                                rainColor.a
                                *
                                .72f))
                };


            splashParticles.Emit(
                impact,
                1);


            // Actual little bouncing droplets.
            for (int i = 0;
                 i < dropletCount;
                 i++)
            {
                Vector3 randomDirection =
                    UnityEngine.Random.onUnitSphere;


                Vector3 tangent =
                    Vector3.ProjectOnPlane(
                        randomDirection,
                        normal);


                if (tangent.sqrMagnitude
                    <
                    .0001f)
                {
                    tangent =
                        Vector3.Cross(
                            normal,
                            Vector3.right);
                }


                if (tangent.sqrMagnitude
                    <
                    .0001f)
                {
                    tangent =
                        Vector3.Cross(
                            normal,
                            Vector3.forward);
                }


                tangent.Normalize();


                float upSpeed =
                    UnityEngine.Random.Range(
                        Mathf.Min(
                            splashUpSpeed.x,
                            splashUpSpeed.y),

                        Mathf.Max(
                            splashUpSpeed.x,
                            splashUpSpeed.y));


                float sideSpeed =
                    UnityEngine.Random.Range(
                        Mathf.Min(
                            splashSideSpeed.x,
                            splashSideSpeed.y),

                        Mathf.Max(
                            splashSideSpeed.x,
                            splashSideSpeed.y));


                ParticleSystem.EmitParams emit =
                    new ParticleSystem.EmitParams
                    {
                        position =
                            position
                            +
                            normal * .018f,

                        velocity =
                            (
                                normal * upSpeed
                                +
                                tangent * sideSpeed
                            )
                            *
                            stormMultiplier,

                        startLifetime =
                            UnityEngine.Random.Range(
                                Mathf.Min(
                                    splashLifetime.x,
                                    splashLifetime.y),

                                Mathf.Max(
                                    splashLifetime.x,
                                    splashLifetime.y)),

                        startSize =
                            UnityEngine.Random.Range(
                                Mathf.Min(
                                    splashSizeMin,
                                    splashSizeMax),

                                Mathf.Max(
                                    splashSizeMin,
                                    splashSizeMax))
                            *
                            stormMultiplier,

                        startColor =
                            new Color(
                                rainColor.r,
                                rainColor.g,
                                rainColor.b,

                                Mathf.Min(
                                    1f,
                                    rainColor.a
                                    *
                                    .92f))
                    };


                splashParticles.Emit(
                    emit,
                    1);
            }
        }


        // ============================================================
        // ACTIVE COUNT
        // ============================================================

        private static int ActiveCount(
            int maximum,
            float amount,
            float densityMultiplier,
            float minimumFraction,
            float maximumFraction)
        {
            if (amount <= .001f)
                return 0;


            float fraction =
                Mathf.Lerp(
                    minimumFraction,
                    maximumFraction,
                    Mathf.Clamp01(
                        amount));


            fraction =
                Mathf.Clamp01(
                    fraction
                    *
                    Mathf.Max(
                        1f,
                        densityMultiplier));


            return
                Mathf.Clamp(
                    Mathf.RoundToInt(
                        maximum
                        *
                        fraction),

                    1,
                    maximum);
        }


        // ============================================================
        // CAMERA
        // ============================================================

        private void ResolveCamera()
        {
            if (gameplayCamera != null)
                return;

            gameplayCamera =
                Camera.main;
        }


        private void ResolveCollisionIgnoreRoot()
        {
            if (collisionIgnoreRoot != null
                ||
                gameplayCamera == null)
            {
                return;
            }

            CharacterController controller =
                gameplayCamera.GetComponentInParent<
                    CharacterController>();

            if (controller != null)
            {
                collisionIgnoreRoot =
                    controller.transform;
            }
        }


        // ============================================================
        // QUAD
        // ============================================================

        private static Mesh BuildQuad()
        {
            Mesh mesh =
                new Mesh
                {
                    name =
                        "GPU Precipitation Quad"
                };


            mesh.vertices =
                new[]
                {
                    new Vector3(
                        -.5f,
                        -.5f,
                        0f),

                    new Vector3(
                        .5f,
                        -.5f,
                        0f),

                    new Vector3(
                        .5f,
                        .5f,
                        0f),

                    new Vector3(
                        -.5f,
                        .5f,
                        0f)
                };


            mesh.uv =
                new[]
                {
                    new Vector2(
                        0f,
                        0f),

                    new Vector2(
                        1f,
                        0f),

                    new Vector2(
                        1f,
                        1f),

                    new Vector2(
                        0f,
                        1f)
                };


            mesh.triangles =
                new[]
                {
                    0,
                    2,
                    1,

                    0,
                    3,
                    2
                };


            mesh.RecalculateBounds();

            mesh.UploadMeshData(
                true);

            return mesh;
        }


        // ============================================================
        // CLEANUP
        // ============================================================

        private void Release()
        {
            if (rainField != null)
            {
                rainField.Dispose();

                rainField =
                    null;
            }


            if (snowField != null)
            {
                snowField.Dispose();

                snowField =
                    null;
            }


            if (collisionBuffer != null)
            {
                collisionBuffer.Release();

                collisionBuffer =
                    null;
            }


            collisionCells =
                null;

            validCollisionCellCount =
                0;


            if (splashParticles != null)
            {
                GameObject splashObject =
                    splashParticles.gameObject;

                splashParticles =
                    null;

                splashRenderer =
                    null;


                if (Application.isPlaying)
                {
                    Destroy(
                        splashObject);
                }
                else
                {
                    DestroyImmediate(
                        splashObject);
                }
            }


            if (splashMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(
                        splashMaterial);
                }
                else
                {
                    DestroyImmediate(
                        splashMaterial);
                }

                splashMaterial =
                    null;
            }


            if (quad != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(
                        quad);
                }
                else
                {
                    DestroyImmediate(
                        quad);
                }

                quad =
                    null;
            }


            kernel =
                -1;

            RainVisibleCount =
                0;

            SnowVisibleCount =
                0;

            movementInitialized =
                false;

            smoothedCameraVelocity =
                Vector3.zero;

            splashAccumulator =
                0f;
        }
    }
}