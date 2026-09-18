using UnityEngine;
using PlotNRots.Items;

namespace PlotNRots.Farming
{
    [CreateAssetMenu(fileName = "New Crop", menuName = "Farming/Crop Data")]
    public class CropData : ScriptableObject
    {
        [Header("Temel Ayarlar")]
        public string cropName;
        public ItemData seedItem;
        public ItemData harvestItem;
        public int harvestAmount = 1;

        [Header("Hasat Şansı")]
        [Range(1, 100)]
        public int dropChance = 100; 

        [Header("Büyüme Evreleri")]
        public GameObject[] growthStages;
        public GameObject rottenPrefab;

        [Header("Süreler (Saniye)")]
        public float timePerStage = 30f;
        public float dryToRotTime = 60f;
    }
}