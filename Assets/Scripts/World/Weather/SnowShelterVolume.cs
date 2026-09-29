using System.Collections.Generic;
using UnityEngine;

namespace PlotNRots.World.Weather
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Plots & Rots/Weather/Snow Shelter Volume")]
    public sealed class SnowShelterVolume : MonoBehaviour
    {
        private const int MaxShelters = 32;

        private static readonly int ShelterCountId =
            Shader.PropertyToID("_GlobalSnowShelterCount");

        private static readonly int ShelterWorldToLocalId =
            Shader.PropertyToID("_GlobalSnowShelterWorldToLocal");

        private static readonly int ShelterParamsId =
            Shader.PropertyToID("_GlobalSnowShelterParams");

        private static readonly List<SnowShelterVolume> Active = new();
        private static readonly Matrix4x4[] WorldToLocal = new Matrix4x4[MaxShelters];
        private static readonly Vector4[] Parameters = new Vector4[MaxShelters];

        [Header("Shelter Box")]
        [SerializeField]
        private Vector3 center = new Vector3(0f, 1.5f, 0f);

        [SerializeField]
        private Vector3 size = new Vector3(8f, 3f, 8f);

        [Header("Edges")]
        [SerializeField, Range(0f, .25f)]
        [Tooltip("0 = hard edge. Higher values make the snow transition softer near the X/Z edges of the shelter box.")]
        private float horizontalEdgeSoftness = .06f;

        [Header("State")]
        [SerializeField]
        private bool blocksSnow = true;

        private Matrix4x4 lastLocalToWorld;
        private Vector3 lastCenter;
        private Vector3 lastSize;
        private float lastSoftness;
        private bool lastBlocksSnow;
        private bool cacheInitialized;

        public Vector3 Center => center;
        public Vector3 Size => size;
        public bool BlocksSnow => blocksSnow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
            ClearShaderGlobals();
        }

        private void OnEnable()
        {
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }

            cacheInitialized = false;
            PublishAll();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            PublishAll();
        }

        private void OnDestroy()
        {
            Active.Remove(this);
            PublishAll();
        }

        private void OnValidate()
        {
            size.x = Mathf.Max(.01f, Mathf.Abs(size.x));
            size.y = Mathf.Max(.01f, Mathf.Abs(size.y));
            size.z = Mathf.Max(.01f, Mathf.Abs(size.z));
            horizontalEdgeSoftness = Mathf.Clamp(horizontalEdgeSoftness, 0f, .25f);

            cacheInitialized = false;

            if (isActiveAndEnabled)
            {
                PublishAll();
            }
        }

        private void LateUpdate()
        {
            Matrix4x4 currentMatrix = transform.localToWorldMatrix;

            bool changed =
                !cacheInitialized
                || currentMatrix != lastLocalToWorld
                || center != lastCenter
                || size != lastSize
                || !Mathf.Approximately(horizontalEdgeSoftness, lastSoftness)
                || blocksSnow != lastBlocksSnow;

            if (!changed)
            {
                return;
            }

            CacheState(currentMatrix);
            PublishAll();
        }

        private void CacheState(Matrix4x4 currentMatrix)
        {
            lastLocalToWorld = currentMatrix;
            lastCenter = center;
            lastSize = size;
            lastSoftness = horizontalEdgeSoftness;
            lastBlocksSnow = blocksSnow;
            cacheInitialized = true;
        }

        private Matrix4x4 BuildUnitBoxWorldToLocal()
        {
            Vector3 safeSize = new Vector3(
                Mathf.Max(.01f, Mathf.Abs(size.x)),
                Mathf.Max(.01f, Mathf.Abs(size.y)),
                Mathf.Max(.01f, Mathf.Abs(size.z)));

            Matrix4x4 localBox = Matrix4x4.TRS(
                center,
                Quaternion.identity,
                safeSize);

            Matrix4x4 boxLocalToWorld =
                transform.localToWorldMatrix
                * localBox;

            return boxLocalToWorld.inverse;
        }

        private static void PublishAll()
        {
            int count = 0;

            for (int i = Active.Count - 1; i >= 0; i--)
            {
                SnowShelterVolume shelter = Active[i];

                if (shelter == null)
                {
                    Active.RemoveAt(i);
                    continue;
                }

                if (!shelter.isActiveAndEnabled
                    || !shelter.gameObject.activeInHierarchy
                    || !shelter.blocksSnow)
                {
                    continue;
                }

                if (count >= MaxShelters)
                {
                    break;
                }

                WorldToLocal[count] = shelter.BuildUnitBoxWorldToLocal();
                Parameters[count] = new Vector4(
                    shelter.horizontalEdgeSoftness,
                    0f,
                    0f,
                    0f);

                count++;
            }

            Shader.SetGlobalFloat(ShelterCountId, count);

            if (count > 0)
            {
                Shader.SetGlobalMatrixArray(ShelterWorldToLocalId, WorldToLocal);
                Shader.SetGlobalVectorArray(ShelterParamsId, Parameters);
            }
        }

        private static void ClearShaderGlobals()
        {
            Shader.SetGlobalFloat(ShelterCountId, 0f);
        }

        private void OnDrawGizmosSelected()
        {
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = previous;
        }
    }
}
