using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class WaterBody : MonoBehaviour
{
    [Header("Su Yüzeyi")]
    [Tooltip("WaterSurfaceGrid objesini buraya bırak.")]
    [SerializeField] private Transform waterSurface;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private readonly Dictionary<Collider, float> nextDebugTimes = new();

    public float SurfaceHeight
    {
        get
        {
            if (waterSurface != null)
                return waterSurface.position.y;

            return transform.position.y;
        }
    }

    private void Awake()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    public float GetSubmergedDepth(Collider target)
    {
        if (target == null)
            return 0f;

        float objectBottomY = target.bounds.min.y;
        float objectHeight = target.bounds.size.y;

        float submergedDepth = SurfaceHeight - objectBottomY;

        return Mathf.Clamp(submergedDepth, 0f, objectHeight);
    }

    public float GetSubmergedFraction(Collider target)
    {
        if (target == null)
            return 0f;

        float objectHeight = Mathf.Max(target.bounds.size.y, 0.001f);

        return GetSubmergedDepth(target) / objectHeight;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger)
            return;

        nextDebugTimes[other] = 0f;

        if (showDebugLogs)
        {
            Debug.Log(other.name + " SUYA GİRDİ.");
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.isTrigger)
            return;

        if (!showDebugLogs)
            return;

        if (!nextDebugTimes.TryGetValue(other, out float nextTime))
            nextTime = 0f;

        if (Time.time < nextTime)
            return;

        float submergedDepth = GetSubmergedDepth(other);
        float submergedFraction = GetSubmergedFraction(other);

        Debug.Log(
            other.name +
            " | Su altında: " +
            submergedDepth.ToString("F2") +
            " m | %" +
            (submergedFraction * 100f).ToString("F0")
        );

        nextDebugTimes[other] = Time.time + 0.5f;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.isTrigger)
            return;

        nextDebugTimes.Remove(other);

        if (showDebugLogs)
        {
            Debug.Log(other.name + " SUDAN ÇIKTI.");
        }
    }

    private void OnValidate()
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box != null)
            box.isTrigger = true;
    }
}