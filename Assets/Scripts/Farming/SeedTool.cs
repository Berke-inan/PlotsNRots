using UnityEngine;
using PlotNRots.Items;
using PlotNRots.Managers;
using PlotNRots.Farming;

namespace PlotNRots.Tools
{
    public class SeedTool : MonoBehaviour, IUsable
    {
        public LayerMask terrainLayer;
        public float interactRange = 5f;
        public CropData cropToPlant; // Bu tohum paketi ne ekiyor?

        public void Use()
        {
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, terrainLayer))
            {
                float gridSize = FarmGridManager.Instance.gridSize;
                int gridX = Mathf.RoundToInt(hit.point.x / gridSize);
                int gridZ = Mathf.RoundToInt(hit.point.z / gridSize);

                Vector2Int gridPos = new Vector2Int(gridX, gridZ);

                bool success = FarmGridManager.Instance.PlantSeed(gridPos, cropToPlant);
                if (success)
                {
                    Debug.Log($"{cropToPlant.cropName} ekildi!");
                    // Burada envanterden 1 tohum silme kodu çağrılacak
                }
                else
                {
                    Debug.Log("Buraya ekilemez! Toprak kuru veya zaten dolu.");
                }
            }
        }
    }
}