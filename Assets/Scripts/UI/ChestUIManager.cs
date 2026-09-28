using UnityEngine;
using UnityEngine.UIElements;
using PlotNRots.Items;

namespace PlotNRots.UI
{
    public class ChestUIManager : MonoBehaviour
    {
        [Header("Referanslar")]
        [Tooltip("Karakterin üzerindeki input/hareket scriptini buraya sürükle")]
        public MonoBehaviour playerInputHandler; // Her türü kabul eder, tip uyuşmazlığına son!

        private UIDocument uiDocument;
        private VisualElement chestWindow;
        private Button closeButton;
        private VisualElement slotContainer;

        private ChestDrop currentChest;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            var root = uiDocument.rootVisualElement;
            chestWindow = root.Q<VisualElement>("ChestWindow");
            closeButton = root.Q<Button>("CloseButton");
            slotContainer = root.Q<VisualElement>("SlotContainer");

            if (closeButton != null)
            {
                closeButton.clicked += CloseChest;
            }

            if (chestWindow != null)
            {
                chestWindow.style.display = DisplayStyle.None;
            }
        }

        public void OpenChest(ChestDrop chest)
        {
            currentChest = chest;

            if (chestWindow != null)
                chestWindow.style.display = DisplayStyle.Flex;

            // Oyuncu inputlarını güvenli bir şekilde kitle
            SetInputActive(false);

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public void CloseChest()
        {
            currentChest = null;

            if (chestWindow != null)
                chestWindow.style.display = DisplayStyle.None;

            // Oyuncu inputlarını geri aç
            SetInputActive(true);

            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        private void SetInputActive(bool active)
        {
            if (playerInputHandler == null) return;

            // Scriptin içindeki IsInputActive değişkenini otomatik bulup değiştirir
            var type = playerInputHandler.GetType();
            var prop = type.GetProperty("IsInputActive");
            if (prop != null)
            {
                prop.SetValue(playerInputHandler, active);
                return;
            }

            var field = type.GetField("IsInputActive");
            if (field != null)
            {
                field.SetValue(playerInputHandler, active);
            }
        }
    }
}