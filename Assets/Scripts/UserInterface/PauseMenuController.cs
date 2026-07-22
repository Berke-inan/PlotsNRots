using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using PlotNRots.Player;
using PlotNRots.SaveSystem;

namespace PlotNRots.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private PlayerInputHandler playerInputHandler;

        private UIDocument _uiDocument;
        private VisualElement _root;

        // Paneller
        private VisualElement _mainMenuPanel;
        private VisualElement _savePanel;
        private VisualElement _loadPanel;

        // Butonlar ve Girdiler
        private Button _resumeBtn;
        private Button _openSavePanelBtn;
        private Button _openLoadPanelBtn;

        private TextField _saveNameInput;
        private Button _confirmSaveBtn;
        private Button _backFromSaveBtn;

        private ScrollView _saveListScrollView;
        private Button _backFromLoadBtn;

        private bool _isPaused = false;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            _root = _uiDocument.rootVisualElement;

            // Panelleri Bul
            _mainMenuPanel = _root.Q<VisualElement>("MainMenuPanel");
            _savePanel = _root.Q<VisualElement>("SavePanel");
            _loadPanel = _root.Q<VisualElement>("LoadPanel");

            // Ana Menü Butonları
            _resumeBtn = _root.Q<Button>("ResumeBtn");
            _openSavePanelBtn = _root.Q<Button>("OpenSavePanelBtn");
            _openLoadPanelBtn = _root.Q<Button>("OpenLoadPanelBtn");

            // Save Menüsü Elemanları
            _saveNameInput = _root.Q<TextField>("SaveNameInput");
            _confirmSaveBtn = _root.Q<Button>("ConfirmSaveBtn");
            _backFromSaveBtn = _root.Q<Button>("BackFromSaveBtn");

            // Load Menüsü Elemanları
            _saveListScrollView = _root.Q<ScrollView>("SaveListScrollView");
            _backFromLoadBtn = _root.Q<Button>("BackFromLoadBtn");

            // Tıklama Olaylarını Bağla
            _resumeBtn.clicked += ResumeGame;

            _openSavePanelBtn.clicked += ShowSavePanel;
            _backFromSaveBtn.clicked += ShowMainMenuPanel;
            _confirmSaveBtn.clicked += HandleSaveConfirmation;

            _openLoadPanelBtn.clicked += ShowLoadPanel;
            _backFromLoadBtn.clicked += ShowMainMenuPanel;

            if (playerInputHandler != null)
            {
                playerInputHandler.OnPauseToggle += TogglePauseState;
            }

            _root.style.display = DisplayStyle.None;
            LockCursor();
        }

        private void OnDisable()
        {
            _resumeBtn.clicked -= ResumeGame;
            _openSavePanelBtn.clicked -= ShowSavePanel;
            _backFromSaveBtn.clicked -= ShowMainMenuPanel;
            _confirmSaveBtn.clicked -= HandleSaveConfirmation;
            _openLoadPanelBtn.clicked -= ShowLoadPanel;
            _backFromLoadBtn.clicked -= ShowMainMenuPanel;

            if (playerInputHandler != null)
            {
                playerInputHandler.OnPauseToggle -= TogglePauseState;
            }
        }

        #region Zaman ve Menü Durum Yönetimi
        private void TogglePauseState()
        {
            if (_isPaused) ResumeGame();
            else PauseGame();
        }

        private void PauseGame()
        {
            _isPaused = true;
            Time.timeScale = 0f;
            if (playerInputHandler != null) playerInputHandler.IsInputActive = false;

            ShowMainMenuPanel(); // Pause tuşuna basıldığında her zaman ana menüden başlasın
            _root.style.display = DisplayStyle.Flex;
            UnlockCursor();
        }

        private void ResumeGame()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            if (playerInputHandler != null) playerInputHandler.IsInputActive = true;

            _root.style.display = DisplayStyle.None;
            LockCursor();
        }
        #endregion

        #region Panel Geçişleri
        private void ShowMainMenuPanel()
        {
            _mainMenuPanel.style.display = DisplayStyle.Flex;
            _savePanel.style.display = DisplayStyle.None;
            _loadPanel.style.display = DisplayStyle.None;
        }

        private void ShowSavePanel()
        {
            _mainMenuPanel.style.display = DisplayStyle.None;
            _savePanel.style.display = DisplayStyle.Flex;

            // Otomatik isim oluştur (Örn: Save_11_45_30)
            _saveNameInput.value = "Save_" + System.DateTime.Now.ToString("HH_mm_ss");
        }

        private void ShowLoadPanel()
        {
            _mainMenuPanel.style.display = DisplayStyle.None;
            _loadPanel.style.display = DisplayStyle.Flex;
            PopulateSaveList(); // Klasördeki save'leri bul ve UI'a diz
        }
        #endregion

        #region Save / Load Mantığı
        private void HandleSaveConfirmation()
        {
            string saveName = _saveNameInput.value;
            if (string.IsNullOrWhiteSpace(saveName)) return;

            SaveManager.Instance.SaveGame(saveName);
            ResumeGame();
        }

        // C# içinden dinamik olarak UI Elementleri üretir ve ScrollView içine ekler
        private void PopulateSaveList()
        {
            _saveListScrollView.Clear(); // Önceki listeyi temizle

            List<SaveFileInfo> saves = SaveManager.Instance.GetAvailableSaves();

            foreach (var save in saves)
            {
                // Satırın ana kutusu
                VisualElement row = new VisualElement();
                row.AddToClassList("save-item-row");

                // Bilgi kısmı (İsim ve Tarih)
                VisualElement infoContainer = new VisualElement();
                infoContainer.AddToClassList("save-item-info");

                Label nameLbl = new Label(save.FileName);
                nameLbl.AddToClassList("save-item-name");

                Label dateLbl = new Label(save.LastModified.ToString("dd/MM/yyyy HH:mm"));
                dateLbl.AddToClassList("save-item-date");

                infoContainer.Add(nameLbl);
                infoContainer.Add(dateLbl);

                // Yükle Butonu
                Button loadBtn = new Button();
                loadBtn.text = "YÜKLE";
                loadBtn.AddToClassList("save-item-load-btn");
                loadBtn.clicked += () =>
                {
                    SaveManager.Instance.LoadGame(save.FullPath);
                    ResumeGame();
                };

                // Elemanları satıra, satırı da ScrollView'a ekle
                row.Add(infoContainer);
                row.Add(loadBtn);
                _saveListScrollView.Add(row);
            }
        }
        #endregion

        private void LockCursor()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
        }

        private void UnlockCursor()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }
    }
}