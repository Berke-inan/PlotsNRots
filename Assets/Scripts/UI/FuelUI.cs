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

        void OnEnable()
        {
            // 1. UI elemanlarını bul
            rootContainer = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("FuelContainer");
            fuelFill = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("FuelFill");

            // 2. Traktörün üzerindeki sistemleri bul
            fuelSystem = GetComponent<FuelSystem>();
            vehicleController = GetComponent<VehicleController>(); // Senin araç scriptin

            // 3. Inspector'dan elle bağlamana gerek kalmadan KOD İLE otomatik bağla!
            if (fuelSystem != null)
            {
                fuelSystem.OnFuelPercentageChanged.AddListener(UpdateFuelBar);
            }
        }

        void OnDisable()
        {
            // Obje kapanırsa hata vermemesi için bağlantıyı kopar
            if (fuelSystem != null)
            {
                fuelSystem.OnFuelPercentageChanged.RemoveListener(UpdateFuelBar);
            }
        }

        void Update()
        {
            // Oyuncu aracın içinde mi diye kontrol et
            if (vehicleController != null && rootContainer != null)
            {
                if (vehicleController.isPlayerInside)
                {
                    // Oyuncu içerideyse UI'ı göster
                    rootContainer.style.display = DisplayStyle.Flex;
                }
                else
                {
                    // Oyuncu dışarıdaysa UI'ı tamamen gizle
                    rootContainer.style.display = DisplayStyle.None;
                }
            }
        }

        // Yakıt azaldığında (veya dolduğunda) otomatik çalışır
        public void UpdateFuelBar(float percentage)
        {
            if (fuelFill != null)
            {
                // Barı daralt/genişlet
                fuelFill.style.width = new Length(percentage * 100f, LengthUnit.Percent);

                // Yakıt %20'nin altındaysa Kırmızı, değilse Turuncu yap
                if (percentage <= 0.2f)
                    fuelFill.style.backgroundColor = new StyleColor(new Color32(220, 20, 20, 255));
                else
                    fuelFill.style.backgroundColor = new StyleColor(new Color32(255, 150, 0, 255));
            }
        }
    }
}