using UnityEngine;
using System.Collections.Generic;
using PlotNRots.Farming;

namespace PlotNRots.Managers
{
    public enum SoilState { Dry, Watered }

    public class SoilData
    {
        public Vector2Int gridPosition; // Modellerin pozisyonunu hesaplarken Dictionary araması yapmamak için eklendi
        public SoilState State = SoilState.Dry;
        public float moistureTimer = 0f; // Toprağın ıslak kalma süresi

        // Ekin Verileri
        public CropData plantedCrop;
        public GameObject cropInstance; // Sahnede o an duran bitkinin 3D modeli
        public int currentStage = 0;
        public float growthTimer = 0f;
        public float rotTimer = 0f;
        public bool isRotten = false;

        public bool IsFullyGrown => plantedCrop != null && currentStage >= plantedCrop.growthStages.Length - 1;
    }

    public class FarmGridManager : MonoBehaviour
    {
        public static FarmGridManager Instance { get; private set; }

        [Header("Görsel Ayarlar")]
        public Mesh tilledDirtMesh;
        public Material dryDirtMaterial;
        public Material wetDirtMaterial;
        public float gridSize = 1f;
        public Vector3 customMeshScale = new Vector3(1f, 0.1f, 1f);

        [Header("Tarım Süreleri")]
        public float soilDryTime = 120f; // Sulanan toprağın tekrar kuruması için geçen süre

        // Her koordinatın detaylı verisini tutan ana haritamız
        public Dictionary<Vector2Int, SoilData> farmGrid = new Dictionary<Vector2Int, SoilData>();

        private RenderParams dryRenderParams;
        private RenderParams wetRenderParams;

        private Matrix4x4[] dryMatrices = new Matrix4x4[0];
        private Matrix4x4[] wetMatrices = new Matrix4x4[0];

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            dryRenderParams = new RenderParams(dryDirtMaterial);
            wetRenderParams = new RenderParams(wetDirtMaterial);

            // Büyüme ve kuruma hesaplamalarını Update yerine saniyede 1 kez yap (Performans için)
            InvokeRepeating(nameof(FarmTick), 1f, 1f);
        }

        private void FarmTick()
        {
            bool visualUpdateNeeded = false;

            foreach (var kvp in farmGrid)
            {
                SoilData soil = kvp.Value;

                // 1. TOPRAK KURUMA MEKANİĞİ
                if (soil.State == SoilState.Watered)
                {
                    soil.moistureTimer += 1f;
                    if (soil.moistureTimer >= soilDryTime)
                    {
                        soil.State = SoilState.Dry;
                        soil.moistureTimer = 0f;
                        visualUpdateNeeded = true;
                    }
                }

                // 2. BÜYÜME VE ÇÜRÜME MEKANİĞİ
                if (soil.plantedCrop != null && !soil.isRotten)
                {
                    if (soil.IsFullyGrown)
                    {
                        // Ekin son aşamada ve toprak kuruysa çürümeye başlar
                        if (soil.State == SoilState.Dry)
                        {
                            soil.rotTimer += 1f;
                            if (soil.rotTimer >= soil.plantedCrop.dryToRotTime)
                            {
                                soil.isRotten = true;
                                UpdateCropVisual(soil);
                            }
                        }
                        else
                        {
                            soil.rotTimer = 0f; // Sulanırsa çürüme sıfırlanır
                        }
                    }
                    else
                    {
                        // Ekin henüz tam büyümedi, toprak ıslaksa büyümeye devam eder
                        if (soil.State == SoilState.Watered)
                        {
                            soil.growthTimer += 1f;
                            if (soil.growthTimer >= soil.plantedCrop.timePerStage)
                            {
                                soil.growthTimer = 0f;
                                soil.currentStage++;
                                UpdateCropVisual(soil);
                            }
                        }
                    }
                }
            }

            if (visualUpdateNeeded) UpdateRenderMatrices();
        }

        // Çapa burayı çağıracak
        public void TillCell(Vector2Int gridPos)
        {
            if (!farmGrid.ContainsKey(gridPos))
            {
                farmGrid.Add(gridPos, new SoilData
                {
                    gridPosition = gridPos,
                    State = SoilState.Dry
                });
                UpdateRenderMatrices();
            }
        }

        // Sulama kabı burayı çağıracak
        public void WaterCell(Vector2Int gridPos)
        {
            if (farmGrid.TryGetValue(gridPos, out SoilData soil))
            {
                if (soil.State == SoilState.Dry)
                {
                    soil.State = SoilState.Watered;
                    UpdateRenderMatrices();
                }
            }
        }

        // Tohum ekme aleti burayı çağıracak
        public bool PlantSeed(Vector2Int gridPos, CropData seedData)
        {
            if (farmGrid.TryGetValue(gridPos, out SoilData soil))
            {
                // Toprak doluysa veya kuruysa (sulanmamışsa) ekilemez
                if (soil.plantedCrop != null || soil.State == SoilState.Dry) return false;

                soil.plantedCrop = seedData;
                soil.currentStage = 0;
                soil.growthTimer = 0f;
                soil.rotTimer = 0f;
                soil.isRotten = false;

                UpdateCropVisual(soil);
                return true;
            }
            return false;
        }


        // Orak burayı çağıracak
        public void HarvestCell(Vector2Int gridPos)
        {
            if (farmGrid.TryGetValue(gridPos, out SoilData soil))
            {
                if (soil.plantedCrop != null)
                {
                    if (soil.IsFullyGrown && !soil.isRotten)
                    {
                        // 1 ile 100 arasında rastgele bir sayı tut ve şansla karşılaştır
                        int roll = Random.Range(1, 101);
                        if (roll <= soil.plantedCrop.dropChance)
                        {
                            if (soil.plantedCrop.harvestItem != null && soil.plantedCrop.harvestItem.worldPrefab != null)
                            {
                                for (int i = 0; i < soil.plantedCrop.harvestAmount; i++)
                                {
                                    Vector3 spawnPos = new Vector3(gridPos.x * gridSize, 0.5f, gridPos.y * gridSize);
                                    spawnPos += new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.2f, 0.2f));
                                    Instantiate(soil.plantedCrop.harvestItem.worldPrefab, spawnPos, Quaternion.identity);
                                }
                            }
                        }
                    }

                    if (soil.cropInstance != null)
                    {
                        Destroy(soil.cropInstance);
                    }

                    soil.plantedCrop = null;
                    soil.currentStage = 0;
                    soil.growthTimer = 0f;
                    soil.rotTimer = 0f;
                    soil.isRotten = false;
                }
            }
        }

        // Ekinin evresine göre 3D modelini günceller
        private void UpdateCropVisual(SoilData soil)
        {
            if (soil.cropInstance != null) Destroy(soil.cropInstance);

            Vector3 worldPos = new Vector3(soil.gridPosition.x * gridSize, 0.05f, soil.gridPosition.y * gridSize);
            GameObject prefabToSpawn = null;

            if (soil.isRotten)
            {
                prefabToSpawn = soil.plantedCrop.rottenPrefab;
            }
            else if (soil.plantedCrop.growthStages != null && soil.plantedCrop.growthStages.Length > 0)
            {
                prefabToSpawn = soil.plantedCrop.growthStages[soil.currentStage];
            }

            if (prefabToSpawn != null)
            {
                soil.cropInstance = Instantiate(prefabToSpawn, worldPos, Quaternion.identity);
                // Doğallık katmak için ekinleri Y ekseninde rastgele döndürme:
                // soil.cropInstance.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            }
        }

        // GPU Instancing için matrisleri günceller
        private void UpdateRenderMatrices()
        {
            List<Matrix4x4> tempDry = new List<Matrix4x4>();
            List<Matrix4x4> tempWet = new List<Matrix4x4>();

            foreach (var kvp in farmGrid)
            {
                Vector2Int cell = kvp.Key;
                SoilData soil = kvp.Value;

                Vector3 worldPos = new Vector3(cell.x * gridSize, 0.01f, cell.y * gridSize);
                Matrix4x4 matrix = Matrix4x4.TRS(worldPos, Quaternion.identity, customMeshScale);

                if (soil.State == SoilState.Dry) tempDry.Add(matrix);
                else tempWet.Add(matrix);
            }

            dryMatrices = tempDry.ToArray();
            wetMatrices = tempWet.ToArray();
        }

        private void Update()
        {
            if (tilledDirtMesh == null) return;

            // Kuru toprakları çiz
            if (dryMatrices.Length > 0 && dryDirtMaterial != null)
                Graphics.RenderMeshInstanced(dryRenderParams, tilledDirtMesh, 0, dryMatrices);

            // Islak toprakları ayrı çiz
            if (wetMatrices.Length > 0 && wetDirtMaterial != null)
                Graphics.RenderMeshInstanced(wetRenderParams, tilledDirtMesh, 0, wetMatrices);
        }
    }
}