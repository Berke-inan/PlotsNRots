using UnityEngine;
using PlotNRots.Items;
using PlotNRots.Managers;

namespace PlotNRots.Tools
{
    public class HoeTool : MonoBehaviour, IUsable
    {
        public LayerMask terrainLayer;
        public float interactRange = 5f;

        public void Use()
        {
            Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, interactRange, terrainLayer))
            {
                float gridSize = FarmGridManager.Instance.gridSize;
                int gridX = Mathf.RoundToInt(hit.point.x / gridSize);
                int gridZ = Mathf.RoundToInt(hit.point.z / gridSize);

                Vector2Int gridPos = new Vector2Int(gridX, gridZ);

                // Ağ olmadan, anında çapalama emri
                FarmGridManager.Instance.TillCell(gridPos);

                Debug.Log($"Çapalandı: {gridX}, {gridZ}");
            }
        }
    }
}