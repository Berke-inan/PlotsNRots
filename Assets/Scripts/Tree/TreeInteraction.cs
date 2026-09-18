using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class TreeInteraction : MonoBehaviour
{
    [Header("Objects")]
    public Transform fallPivot;
    public GameObject wholeTree;
    public GameObject leaves;
    public GameObject splitLogs;

    [Header("Hit Settings")]
    public int hitsToFall = 3;
    public int hitsToRemoveLeaves = 1;
    public int hitsToSplit = 1;

    [Header("Fall Settings")]
    public float fallDuration = 1.5f;
    public float fallAngle = 90f;

    private int hitCount = 0;
    private int stage = 0;
    private bool falling = false;

    private void Start()
    {
        // İkiye ayrılmış parçalar başlangıçta görünmesin
        if (splitLogs != null)
            splitLogs.SetActive(false);
    }

    private void Update()
    {
        // NEW INPUT SYSTEM
        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            Hit();
        }
    }

    public void Hit()
    {
        if (falling)
            return;

        // =========================
        // 0 - AĞAÇ AYAKTA
        // =========================
        if (stage == 0)
        {
            hitCount++;

            Debug.Log(
                "Ağaca vuruldu: " +
                hitCount + "/" +
                hitsToFall
            );

            if (hitCount >= hitsToFall)
            {
                hitCount = 0;
                StartCoroutine(FallTree());
            }

            return;
        }

        // =========================
        // 1 - AĞAÇ YERDE + YAPRAKLI
        // =========================
        if (stage == 1)
        {
            hitCount++;

            Debug.Log(
                "Yaprak aşaması: " +
                hitCount + "/" +
                hitsToRemoveLeaves
            );

            if (hitCount >= hitsToRemoveLeaves)
            {
                hitCount = 0;

                if (leaves != null)
                    leaves.SetActive(false);

                stage = 2;

                Debug.Log("Yapraklar kaldırıldı.");
            }

            return;
        }

        // =========================
        // 2 - YERDE ÇIPLAK GÖVDE
        // =========================
        if (stage == 2)
        {
            hitCount++;

            Debug.Log(
                "Gövde aşaması: " +
                hitCount + "/" +
                hitsToSplit
            );

            if (hitCount >= hitsToSplit)
            {
                hitCount = 0;

                if (wholeTree != null)
                    wholeTree.SetActive(false);

                if (splitLogs != null)
                    splitLogs.SetActive(true);

                stage = 3;

                Debug.Log("Gövde iki parçaya ayrıldı.");
            }

            return;
        }
    }

    private IEnumerator FallTree()
    {
        if (fallPivot == null)
        {
            Debug.LogError(
                "FallPivot atanmamış!"
            );

            yield break;
        }

        falling = true;

        Quaternion startRotation =
            fallPivot.localRotation;

        Quaternion targetRotation =
            startRotation *
            Quaternion.Euler(
                0f,
                0f,
                fallAngle
            );

        float timer = 0f;

        while (timer < fallDuration)
        {
            timer += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    timer / fallDuration
                );

            // Başlangıçta yavaş,
            // sonra hızlanan düşüş
            float fallT = t * t;

            fallPivot.localRotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    fallT
                );

            yield return null;
        }

        fallPivot.localRotation =
            targetRotation;

        stage = 1;
        falling = false;

        Debug.Log("Ağaç tamamen düştü.");
    }
}