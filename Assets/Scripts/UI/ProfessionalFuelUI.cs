using UnityEngine;
using UnityEngine.UIElements;

namespace GasSystem
{
    [RequireComponent(typeof(UIDocument))]
    public class ProfessionalFuelUI : MonoBehaviour
    {
        private VisualElement hudContainer;
        private VisualElement progressFill;
        private Label percentageText;

        void OnEnable()
        {
            // Ekranda elemanları bul
            var root = GetComponent<UIDocument>().rootVisualElement;
            hudContainer = root.Q<VisualElement>("HUDContainer");
            progressFill = root.Q<VisualElement>("ProgressFill");
            percentageText = root.Q<Label>("PercentageText");
        }

        void Start()
        {
            // YENİ EKLENDİ: Oyun başladığında UI'ı zorla gizle! 
            // Sen bidonu eline alana kadar ekranda görünmeyecek.
            ToggleUIVisibility(false);
        }

        // Yakıt değiştikçe Bidon veya Traktör bu fonksiyonu tetikler (0.0 ile 1.0 arası)
        public void UpdateFuelUI(float normalizedFuel)
        {
            if (hudContainer == null || progressFill == null || percentageText == null) return;

            // 1. Yüzdeyi tam sayıya çevir ve yazdır (* İŞARETİ DÜZELTİLDİ)
            int percent = Mathf.RoundToInt(normalizedFuel * 100f);
            percentageText.text = $"%{percent}";

            // 2. CSS'teki Transition sayesinde bar yumuşakça hareket eder (* İŞARETİ DÜZELTİLDİ)
            progressFill.style.width = new Length(normalizedFuel * 100f, LengthUnit.Percent);

            // 3. Yakıt %20'nin altına inerse uyarı CSS sınıflarını (Kırmızı temayı) ekle
            if (normalizedFuel <= 0.2f)
            {
                progressFill.AddToClassList("progress-fill-low");
                hudContainer.AddToClassList("hud-container-low");
            }
            else // %20'nin üstündeyse turuncuya geri dön
            {
                progressFill.RemoveFromClassList("progress-fill-low");
                hudContainer.RemoveFromClassList("hud-container-low");
            }
        }

        // Oyuncu bidonu eline alınca aç, bırakınca gizle fonksiyonu
        public void ToggleUIVisibility(bool isVisible)
        {
            if (hudContainer != null)
                hudContainer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}