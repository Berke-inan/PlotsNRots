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
            // UI elemanlarını bul
            var root = GetComponent<UIDocument>().rootVisualElement;
            hudContainer = root.Q<VisualElement>("HUDContainer");
            progressFill = root.Q<VisualElement>("ProgressFill");
            percentageText = root.Q<Label>("PercentageText");
        }

        public void UpdateFuelUI(float normalizedFuel)
        {
            if (progressFill == null || percentageText == null) return;

            // ÇARPMA İŞLEMLERİ DÜZELTİLDİ (*)
            int percent = Mathf.RoundToInt(normalizedFuel * 100f);
            percentageText.text = $"%{percent}";
            progressFill.style.width = new Length(normalizedFuel * 100f, LengthUnit.Percent);

            // %20 altındaysa kırmızı yap, üstündeyse turuncuya dön
            if (normalizedFuel <= 0.2f)
            {
                progressFill.AddToClassList("progress-fill-low");
                if (hudContainer != null) hudContainer.AddToClassList("hud-container-low");
            }
            else
            {
                progressFill.RemoveFromClassList("progress-fill-low");
                if (hudContainer != null) hudContainer.RemoveFromClassList("hud-container-low");
            }
        }
    }
}