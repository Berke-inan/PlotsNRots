using System.Collections.Generic;
using UnityEngine;

namespace PlotNRots.World.Weather
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class WeatherShelterVolume : MonoBehaviour
    {
        private static readonly List<WeatherShelterVolume> ActiveVolumes = new List<WeatherShelterVolume>(32);

        [SerializeField] private Collider volume;
        [SerializeField, Range(0f, 1f), Tooltip("1 = outdoor loudness, 0 = fully muffled.")]
        private float audioExposure = .25f;
        [SerializeField, Range(0f, 1f), Tooltip("1 = full precipitation around camera, 0 = none.")]
        private float precipitationExposure = .05f;

        public float AudioExposure => audioExposure;
        public float PrecipitationExposure => precipitationExposure;

        private void Reset() => volume = GetComponent<Collider>();

        private void OnEnable()
        {
            if (volume == null) volume = GetComponent<Collider>();
            if (!ActiveVolumes.Contains(this)) ActiveVolumes.Add(this);
        }

        private void OnDisable() => ActiveVolumes.Remove(this);

        public bool Contains(Vector3 worldPoint)
        {
            if (volume == null || !volume.enabled || !gameObject.activeInHierarchy) return false;
            if (!volume.bounds.Contains(worldPoint)) return false;

            Vector3 closest = volume.ClosestPoint(worldPoint);
            return (closest - worldPoint).sqrMagnitude <= .0004f;
        }

        public static void Evaluate(Vector3 worldPoint, ref float audio, ref float precipitation)
        {
            for (int i = ActiveVolumes.Count - 1; i >= 0; i--)
            {
                WeatherShelterVolume candidate = ActiveVolumes[i];
                if (candidate == null)
                {
                    ActiveVolumes.RemoveAt(i);
                    continue;
                }

                if (!candidate.Contains(worldPoint)) continue;
                audio = Mathf.Min(audio, candidate.audioExposure);
                precipitation = Mathf.Min(precipitation, candidate.precipitationExposure);
            }
        }
    }
}
