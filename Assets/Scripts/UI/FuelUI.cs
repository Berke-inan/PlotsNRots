using UnityEngine;
using UnityEngine.UIElements;

namespace GasSystem
{
    [RequireComponent(typeof(UIDocument))]
    public class FuelUI : MonoBehaviour
    {
        private VisualElement rootContainer;
        private VisualElement fuelFill;

        private FuelSystem fuelSystem;
        private VehicleController vehicleController;

        // YENİ: Dışarıdan benzin doldurulurken UI'ı açık tutacak sayaç
        private float forceShowTimer = 0f;

        void OnEnable()
        {
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc != null && uiDoc.rootVisualElement != null)
            {
                rootContainer = uiDoc.rootVisualElement.Q<VisualElement>("FuelContainer");
                fuelFill = uiDoc.rootVisualElement.Q<VisualElement>("FuelFill");

                if (rootContainer != null) rootContainer.style.display = DisplayStyle.None;
            }

            fuelSystem = GetComponent<FuelSystem>();
            vehicleController = GetComponent<VehicleController>();

            if (fuelSystem != null)
            {
                fuelSystem.OnFuelPercentageChanged.AddListener(UpdateFuelBar);
            }
        }

        void OnDisable()
        {
            if (fuelSystem != null)
            {
                fuelSystem.OnFuelPercentageChanged.RemoveListener(UpdateFuelBar);
            }
        }

        void Update()
        {
            if (rootContainer != null)
            {
                // YENİ MANTIK: Dışarıdan biri benzin dolduruyorsa (Sayaç 0'dan büyükse) GÖSTER
                if (forceShowTimer > 0)
                {
                    forceShowTimer -= Time.deltaTime;
                    rootContainer.style.display = DisplayStyle.Flex;
                }
                // Doldurma yoksa, oyuncu aracın içinde mi diye bak
                else if (vehicleController != null && vehicleController.isPlayerInside)
                {
                    rootContainer.style.display = DisplayStyle.Flex;
                }
                else
                {
                    rootContainer.style.display = DisplayStyle.None;
                }
            }
        }

        public void UpdateFuelBar(float percentage)
        {
            if (fuelFill != null)
            {
                fuelFill.style.width = new Length(percentage * 100f, LengthUnit.Percent);

                if (percentage <= 0.2f)
                    fuelFill.style.backgroundColor = new StyleColor(new Color32(220, 20, 20, 255));
                else
                    fuelFill.style.backgroundColor = new StyleColor(new Color32(255, 150, 0, 255));
            }
        }

        // YENİ: Bidon tarafından çağrılacak "Kendini Göster" komutu
        public void ShowTemporarily(float time = 1.0f)
        {
            forceShowTimer = time;
        }
    }
}