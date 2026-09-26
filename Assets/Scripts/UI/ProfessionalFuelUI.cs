using UnityEngine;
using UnityEngine.UIElements;

namespace GasSystem
{
    [RequireComponent(typeof(UIDocument))]
    public class ProfessionalFuelUI : MonoBehaviour
    {
        private VisualElement hudContainer;

        // Bidon Elemanları
        private VisualElement canContainer;
        private VisualElement progressFill;
        private Label percentageText;

        // Traktör Elemanları
        private VisualElement targetContainer;
        private VisualElement targetProgressFill;
        private Label targetPercentageText;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            hudContainer = root.Q<VisualElement>("HUDContainer");

            // Bidon grubunu bul
            canContainer = root.Q<VisualElement>("CanContainer");
            progressFill = root.Q<VisualElement>("ProgressFill");
            percentageText = root.Q<Label>("PercentageText");

            // Traktör grubunu bul
            targetContainer = root.Q<VisualElement>("TargetContainer");
            targetProgressFill = root.Q<VisualElement>("TargetProgressFill");
            targetPercentageText = root.Q<Label>("TargetPercentageText");
        }

        void Start()
        {
            ToggleUIVisibility(false);
            ToggleTargetUIVisibility(false);
        }

        // --- BİDON KONTROLLERİ ---
        public void UpdateFuelUI(float normalizedFuel)
        {
            if (progressFill == null || percentageText == null) return;
            int percent = Mathf.RoundToInt(normalizedFuel * 100f);
            percentageText.text = $"%{percent}";
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
            if (canContainer != null) canContainer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            UpdatePanelBackground();
        }

        // --- TRAKTÖR KONTROLLERİ ---
        public void UpdateTargetFuelUI(float normalizedFuel)
        {
            if (targetProgressFill == null || targetPercentageText == null) return;
            int percent = Mathf.RoundToInt(normalizedFuel * 100f);
            targetPercentageText.text = $"%{percent}";
            targetProgressFill.style.width = new Length(normalizedFuel * 100f, LengthUnit.Percent);
        }

        public void ToggleTargetUIVisibility(bool isVisible)
        {
            if (targetContainer != null)
            {
                targetContainer.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;

                // Eğer sadece traktör barı görünüyorsa üst boşluğu (margin) sil ki kutuya ortalansın
                if (isVisible && (canContainer == null || canContainer.style.display == DisplayStyle.None))
                    targetContainer.style.marginTop = 0;
                else
                    targetContainer.style.marginTop = 15;
            }
            UpdatePanelBackground();
        }

        // --- ZEKİ ARKA PLAN (KUTU) KONTROLÜ ---
        private void UpdatePanelBackground()
        {
            if (hudContainer == null) return;

            bool isCanVisible = canContainer != null && canContainer.style.display == DisplayStyle.Flex;
            bool isTargetVisible = targetContainer != null && targetContainer.style.display == DisplayStyle.Flex;

            // İkisinden biri bile açıksa siyah kutuyu göster
            hudContainer.style.display = (isCanVisible || isTargetVisible) ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}