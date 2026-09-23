using System;
using UnityEngine;

// Kept only so old scene/prefab references and editor validation remain compatible.
// The final GPU precipitation system is camera-centred and does not require this
// component for runtime precipitation rendering.
[Obsolete("GpuPrecipitationSystem is camera-centred and no longer requires a precipitation rig/follower.")]
[DisallowMultipleComponent]
public sealed class PrecipitationFollower : MonoBehaviour
{
    [SerializeField] private Transform gameplayCamera;

    private void OnEnable()
    {
        FollowNow();
    }

    private void LateUpdate()
    {
        FollowNow();
    }

    // Kept public for legacy editor validations/tools that call this method directly.
    public void FollowNow()
    {
        if (gameplayCamera == null)
        {
            Camera camera = Camera.main;
            if (camera != null)
                gameplayCamera = camera.transform;
        }

        if (gameplayCamera != null)
            transform.position = gameplayCamera.position;
    }
}
