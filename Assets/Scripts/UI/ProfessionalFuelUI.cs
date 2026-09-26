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

        // Traktör (Hedef) UI Elemanları
        private VisualElement targetContainer;
        private VisualElement targetProgressFill;
        private Label targetPercentageText;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            hudContainer = root.Q<VisualElement>("HUDContainer");
            progressFill = root.Q<VisualElement>("ProgressFill");
            percentageText = root.Q<Label>("PercentageText");

            // Yeni Traktör elemanlarını bul
            targetContainer = root.Q<VisualElement>("TargetContainer");
            targetProgressFill = root.Q<VisualElement>("TargetProgressFill");
            targetPercentageText = root.Q<Label>("TargetPercentageText");
        }

        void Start()
        {
            ToggleUIVisibility(false); // Başlangıçta ana paneli gizle
            ToggleTargetUIVisibility(false); // Traktör barını garanti gizle
        }

        // --- BİDON (ANA) KONTROLLERİ ---
        public void UpdateFuelUI(float normalizedFuel)
        {
            if (progressFill == null || percentageText == null) return;

            // 1. Yüzdeyi tam sayıya çevir (ÇARPMA İŞLEMİ DÜZELTİLDİ)
            int percent = Mathf.RoundToInt(normalizedFuel * 100f);
            percentageText.text = $"%{percent}";

            // 2. Barın boyutunu ayarla (ÇARPMA İŞLEMİ DÜZELTİLDİ)
            progressFill.style.width = new Length(normalizedFuel * 100f, LengthUnit.Percent);

            if (normalizedFuel <= 0.2f)
            {
                progressFill.AddToClassList("progress-fill-low");
                hudContainer.AddToClassList("hud-container-low");
            }
            else
            {
                progressFill.RemoveFromClassList("progress-fill-low");
                hudContainer.RemoveFromClassList("hud-container-low");
            }
        }

        public void ToggleUIVisibility(bool isVisible)
        {
            if (hudContainer != null)
                hudContainer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // --- TRAKTÖR (HEDEF) KONTROLLERİ ---
        public void UpdateTargetFuelUI(float normalizedFuel)
        {
            if (targetProgressFill == null || targetPercentageText == null) return;

            // ÇARPMA İŞLEMLERİ EKLENDİ
            int percent = Mathf.RoundToInt(normalizedFuel * 100f);
            targetPercentageText.text = $"%{percent}";
            targetProgressFill.style.width = new Length(normalizedFuel * 100f, LengthUnit.Percent);
        }

        public void ToggleTargetUIVisibility(bool isVisible)
        {
            if (targetContainer != null)
                targetContainer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}