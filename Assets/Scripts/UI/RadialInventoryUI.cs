using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class RadialInventoryUI : MonoBehaviour
{
    // Diðer kodlarýn (Kamera vb.) menünün açýk olup olmadýðýný anlamasý için Static deðiþken
    public static bool IsMenuOpen { get; private set; }

    public InventoryManager inventory;
    public UIDocument uiDocument;

    [Header("Radial Ayarlar")]
    public float radius = 200f;

    private VisualElement root;
    private VisualElement radialContainer;
    private List<VisualElement> slotElements = new List<VisualElement>();

    // Arayüz Elemanlarý (HUD ve Merkez)
    private VisualElement hudContainer;
    private Image hudIcon;
    private VisualElement centerSlot;
    private Image centerIcon;

    private void Start()
    {
        root = uiDocument.rootVisualElement;

        // --- 1. SAÐ ALT HUD OLUÞTURMA (SADECE ÞEFFAF ÝKON, ARKAPLAN YOK) ---
        hudContainer = new VisualElement();
        hudContainer.style.position = Position.Absolute;
        hudContainer.style.bottom = 40;
        hudContainer.style.right = 40;
        hudContainer.style.width = 100;
        hudContainer.style.height = 100;

        hudIcon = new Image();
        hudIcon.style.width = Length.Percent(100);
        hudIcon.style.height = Length.Percent(100);
        hudIcon.tintColor = new Color(1f, 1f, 1f, 0.7f); // Hafif þeffaf (hologram) efekti
        hudIcon.style.display = DisplayStyle.None;
        hudContainer.Add(hudIcon);
        root.Add(hudContainer);

        // --- 2. RADYAL MENÜ ANA KONTEYNERÝ ---
        radialContainer = new VisualElement();
        radialContainer.style.width = Length.Percent(100);
        radialContainer.style.height = Length.Percent(100);
        radialContainer.style.position = Position.Absolute;
        radialContainer.style.display = DisplayStyle.None;
        root.Add(radialContainer);

        // --- 3. MERKEZ BÜYÜK YUVA OLUÞTURMA ---
        centerSlot = new VisualElement();
        centerSlot.style.position = Position.Absolute;
        centerSlot.style.width = 120;
        centerSlot.style.height = 120;
        centerSlot.style.left = new StyleLength(new Length(50, LengthUnit.Percent));
        centerSlot.style.top = new StyleLength(new Length(50, LengthUnit.Percent));
        centerSlot.style.marginLeft = -60;
        centerSlot.style.marginTop = -60;
        centerSlot.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.95f);

        centerSlot.style.borderBottomWidth = 3;
        centerSlot.style.borderTopWidth = 3;
        centerSlot.style.borderLeftWidth = 3;
        centerSlot.style.borderRightWidth = 3;
        centerSlot.style.borderBottomColor = new Color(1f, 0.8f, 0f, 1f);
        centerSlot.style.borderTopColor = new Color(1f, 0.8f, 0f, 1f);
        centerSlot.style.borderLeftColor = new Color(1f, 0.8f, 0f, 1f);
        centerSlot.style.borderRightColor = new Color(1f, 0.8f, 0f, 1f);
        centerSlot.style.borderTopLeftRadius = 60;
        centerSlot.style.borderTopRightRadius = 60;
        centerSlot.style.borderBottomLeftRadius = 60;
        centerSlot.style.borderBottomRightRadius = 60;

        centerIcon = new Image();
        centerIcon.style.width = Length.Percent(70);
        centerIcon.style.height = Length.Percent(70);
        centerIcon.style.left = Length.Percent(15);
        centerIcon.style.top = Length.Percent(15);
        centerIcon.style.display = DisplayStyle.None;
        centerSlot.Add(centerIcon);
        radialContainer.Add(centerSlot);

        // Yuvalarý oluþtur ve eventleri dinle
        GenerateRadialSlots();

        inventory.OnInventoryChanged += UpdateUI;
        inventory.OnActiveItemChanged += HighlightActiveSlot;

        UpdateUI();
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        // TAB Tuþuna basýldýðýnda menü açýlýr, býrakýldýðýnda kapanýr
        if (Keyboard.current.tabKey.wasPressedThisFrame) OpenMenu();
        if (Keyboard.current.tabKey.wasReleasedThisFrame) CloseMenu();

        // Eðer menü açýksa farenin hareketini takip et
        if (IsMenuOpen) HandleMouseSelection();
    }

    private void GenerateRadialSlots()
    {
        float angleStep = 360f / inventory.maxSlots;

        for (int i = 0; i < inventory.maxSlots; i++)
        {
            VisualElement slot = new VisualElement();
            slot.style.width = 80;
            slot.style.height = 80;
            slot.style.position = Position.Absolute;
            slot.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            slot.style.borderTopLeftRadius = 10;
            slot.style.borderTopRightRadius = 10;
            slot.style.borderBottomLeftRadius = 10;
            slot.style.borderBottomRightRadius = 10;

            Image icon = new Image();
            icon.name = "Icon";
            icon.style.width = Length.Percent(80);
            icon.style.height = Length.Percent(80);
            icon.style.alignSelf = Align.Center;
            slot.Add(icon);

            float angle = i * angleStep * Mathf.Deg2Rad;
            slot.style.left = new StyleLength(new Length(50, LengthUnit.Percent));
            slot.style.top = new StyleLength(new Length(50, LengthUnit.Percent));

            slot.style.marginLeft = Mathf.Sin(angle) * radius - 40;
            slot.style.marginTop = -Mathf.Cos(angle) * radius - 40;

            slotElements.Add(slot);
            radialContainer.Add(slot);
        }
    }

    private void UpdateUI()
    {
        for (int i = 0; i < inventory.maxSlots; i++)
        {
            Image icon = slotElements[i].Q<Image>("Icon");
            if (!inventory.slots[i].IsEmpty)
            {
                icon.sprite = inventory.slots[i].item.uiIcon;
                icon.style.display = DisplayStyle.Flex;
            }
            else
            {
                icon.sprite = null;
                icon.style.display = DisplayStyle.None;
            }
        }

        HighlightActiveSlot(inventory.activeSlotIndex);
    }

    private void HandleMouseSelection()
    {
        Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector2 dir = mousePos - screenCenter;

        if (dir.magnitude > 50f)
        {
            float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360;

            float angleStep = 360f / inventory.maxSlots;
            int selectedIndex = Mathf.RoundToInt(angle / angleStep) % inventory.maxSlots;

            if (inventory.activeSlotIndex != selectedIndex)
            {
                inventory.SetActiveSlot(selectedIndex);
            }
        }
    }

    private void HighlightActiveSlot(int index)
    {
        // 1. Dýþtaki Küçük Yuvalarýn Rengini Ayarla
        for (int i = 0; i < slotElements.Count; i++)
        {
            if (i == index)
                slotElements[i].style.backgroundColor = new Color(1f, 0.8f, 0f, 0.8f);
            else
                slotElements[i].style.backgroundColor = new Color(0, 0, 0, 0.5f);
        }

        // 2. Sol Alt HUD ve Merkez Ýkon Güncellemesi
        if (index != -1 && !inventory.slots[index].IsEmpty)
        {
            Sprite activeSprite = inventory.slots[index].item.uiIcon;

            hudIcon.sprite = activeSprite;
            hudIcon.style.display = DisplayStyle.Flex;

            centerIcon.sprite = activeSprite;
            centerIcon.style.display = DisplayStyle.Flex;
        }
        else
        {
            hudIcon.style.display = DisplayStyle.None;
            centerIcon.style.display = DisplayStyle.None;
        }
    }

    private void OpenMenu()
    {
        IsMenuOpen = true;
        radialContainer.style.display = DisplayStyle.Flex;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;

        Time.timeScale = 0.1f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }

    private void CloseMenu()
    {
        IsMenuOpen = false;
        radialContainer.style.display = DisplayStyle.None;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }
}