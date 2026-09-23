using UnityEngine;

namespace PlotNRots.World.Weather
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(9000)]
    public sealed class WeatherExposureController
        :
        MonoBehaviour
    {
        [SerializeField]
        private Transform anchor;


        // ============================================================
        // ROOF DETECTION
        // ============================================================

        [Header("Automatic Roof Probe - Audio Only")]

        [SerializeField]
        private bool automaticRoofProbe = true;

        [SerializeField]
        private LayerMask shelterMask = -1;

        [SerializeField]
        [Min(1f)]
        private float roofProbeDistance = 10f;

        [SerializeField]
        [Min(0f)]
        private float roofProbeRadius = .16f;

        [SerializeField]
        [Range(0f, 1f)]
        private float roofAudioExposure = .34f;

        [SerializeField]
        [Min(.02f)]
        private float probeInterval = .10f;

        [SerializeField]
        [Min(.01f)]
        private float responseSpeed = 12f;


        // ============================================================
        // RUNTIME
        // ============================================================

        private readonly RaycastHit[] roofHits =
            new RaycastHit[16];

        private float targetAudio =
            1f;

        private float nextProbeTime;


        // ============================================================
        // PUBLIC
        // ============================================================

        public float AudioExposure
        {
            get;
            private set;
        }
        =
        1f;


        // IMPORTANT:
        // WeatherVisualsManager still reads this property.
        //
        // It now ALWAYS returns 1.
        //
        // Precipitation is never globally disabled indoors anymore.
        // Real world colliders stop the GPU precipitation instead.
        public float PrecipitationExposure =>
            1f;


        public Transform Anchor =>
            anchor;


        public bool IsSheltered =>
            AudioExposure < .75f;


        // ============================================================
        // UNITY
        // ============================================================

        private void OnEnable()
        {
            ResolveAnchor();

            ForceProbe();
        }


        private void Update()
        {
            ResolveAnchor();

            if (anchor == null)
                return;


            if (Time.unscaledTime
                >=
                nextProbeTime)
            {
                nextProbeTime =
                    Time.unscaledTime
                    +
                    probeInterval;

                Probe();
            }


            float t =
                1f
                -
                Mathf.Exp(
                    -responseSpeed
                    *
                    Time.unscaledDeltaTime);


            AudioExposure =
                Mathf.Lerp(
                    AudioExposure,
                    targetAudio,
                    t);
        }


        // ============================================================
        // FORCE
        // ============================================================

        public void ForceProbe()
        {
            ResolveAnchor();

            if (anchor == null)
                return;


            Probe();

            AudioExposure =
                targetAudio;
        }


        // ============================================================
        // PROBE
        // ============================================================

        private void Probe()
        {
            float audio =
                1f;


            // Kept because existing WeatherShelterVolume
            // API expects both values.
            //
            // Precipitation result is deliberately ignored.
            float ignoredPrecipitation =
                1f;


            Vector3 point =
                anchor.position;


            // Existing vehicle/house shelter volumes
            // remain useful for acoustics.
            WeatherShelterVolume.Evaluate(
                point,
                ref audio,
                ref ignoredPrecipitation);


            if (automaticRoofProbe
                &&
                shelterMask.value != 0
                &&
                HasRoofAbove(point))
            {
                audio =
                    Mathf.Min(
                        audio,
                        roofAudioExposure);
            }


            targetAudio =
                Mathf.Clamp01(
                    audio);
        }


        // ============================================================
        // ROOF CHECK
        // ============================================================

        private bool HasRoofAbove(
            Vector3 point)
        {
            Vector3 origin =
                point
                +
                Vector3.up
                *
                .05f;


            int hitCount;


            if (roofProbeRadius > .001f)
            {
                hitCount =
                    Physics.SphereCastNonAlloc(
                        origin,
                        roofProbeRadius,
                        Vector3.up,
                        roofHits,
                        roofProbeDistance,
                        shelterMask,
                        QueryTriggerInteraction.Ignore);
            }
            else
            {
                hitCount =
                    Physics.RaycastNonAlloc(
                        origin,
                        Vector3.up,
                        roofHits,
                        roofProbeDistance,
                        shelterMask,
                        QueryTriggerInteraction.Ignore);
            }


            for (int i = 0;
                 i < hitCount;
                 i++)
            {
                Collider hitCollider =
                    roofHits[i].collider;


                if (hitCollider == null)
                    continue;


                if (hitCollider.transform
                    ==
                    anchor)
                {
                    continue;
                }


                return true;
            }


            return false;
        }


        // ============================================================
        // ANCHOR
        // ============================================================

        private void ResolveAnchor()
        {
            if (anchor != null)
                return;


            Camera camera =
                Camera.main;


            if (camera != null)
            {
                anchor =
                    camera.transform;
            }
        }
    }
}