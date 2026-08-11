using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class MainHUDManager : MonoBehaviour
{
    public static MainHUDManager Instance;

    public UIDocument uiDocument;
    public PlayerStats playerStats;
    public DayNightCycleManager timeManager;

    [Header("Ýkonlar")]
    public Sprite sunSprite;
    public Sprite moonSprite;

    // Arayüz Elementleri
    private VisualElement _healthBar, _maxEnergyBar, _currentEnergyBar;
    private Label _healthText, _currentEnergyText, _maxEnergyText, _dayText, _clockText;
    private VisualElement _timeIcon;
    private VisualElement _topEyelid, _bottomEyelid, _dailyStatsContainer;

    private int _dayCount = 1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnEnable()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;
        var root = uiDocument.rootVisualElement;

        // UI Elementlerini Bulma
        _healthBar = root.Q<VisualElement>("HealthBar");
        _healthText = root.Q<Label>("HealthText");

        _maxEnergyBar = root.Q<VisualElement>("MaxEnergyBar");
        _currentEnergyBar = root.Q<VisualElement>("CurrentEnergyBar");
        _currentEnergyText = root.Q<Label>("CurrentEnergyText");

        // Paint çiziminde olmadýðý için UXML'den sildiðimiz deðer (Artýk null kalsa bile çökmeyecek)
        _maxEnergyText = root.Q<Label>("MaxEnergyText");

        _dayText = root.Q<Label>("DayText");
        _clockText = root.Q<Label>("ClockText");
        _timeIcon = root.Q<VisualElement>("TimeIcon");

        _topEyelid = root.Q<VisualElement>("TopEyelid");
        _bottomEyelid = root.Q<VisualElement>("BottomEyelid");
        _dailyStatsContainer = root.Q<VisualElement>("DailyStatsContainer");

        if (playerStats != null)
        {
            playerStats.OnHealthChanged += UpdateHealthUI;
            playerStats.OnEnergyChanged += UpdateEnergyUI;
        }

        DayNightCycleManager.YeniGunBasladiSinyali += OnNewDay;
    }

    private void OnDisable()
    {
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= UpdateHealthUI;
            playerStats.OnEnergyChanged -= UpdateEnergyUI;
        }
        DayNightCycleManager.YeniGunBasladiSinyali -= OnNewDay;
    }

    private void Update()
    {
        if (timeManager != null)
        {
            int hours = Mathf.FloorToInt(timeManager.currentTime);
            int mins = Mathf.FloorToInt((timeManager.currentTime - hours) * 60);
            UpdateClockUI(hours, mins);
        }
    }

    private void UpdateHealthUI(float current, float max)
    {
        if (_healthBar != null) _healthBar.style.width = new Length((current / max) * 100f, LengthUnit.Percent);
        if (_healthText != null) _healthText.text = Mathf.RoundToInt(current).ToString();
    }

    private void UpdateEnergyUI(float current, float currentMax, float absoluteMax)
    {
        // GÜVENLÝK KONTROLLERÝ (Eðer UXML'de yoklarsa hata vermeden atlar)
        if (_maxEnergyBar != null)
            _maxEnergyBar.style.width = new Length((currentMax / absoluteMax) * 100f, LengthUnit.Percent);

        if (_currentEnergyBar != null)
            _currentEnergyBar.style.width = new Length((current / currentMax) * 100f, LengthUnit.Percent);

        if (_currentEnergyText != null)
            _currentEnergyText.text = Mathf.RoundToInt(current).ToString();

        if (_maxEnergyText != null)
            _maxEnergyText.text = "/ " + Mathf.RoundToInt(currentMax).ToString();
    }

    private void UpdateClockUI(int hours, int minutes)
    {
        if (_clockText != null) _clockText.text = string.Format("{0:00}:{1:00}", hours, minutes);

        if (_timeIcon != null && timeManager != null)
        {
            if (timeManager.IsNight())
                _timeIcon.style.backgroundImage = new StyleBackground(moonSprite);
            else
                _timeIcon.style.backgroundImage = new StyleBackground(sunSprite);
        }
    }

    private void OnNewDay()
    {
        _dayCount++;
        if (_dayText != null) _dayText.text = "GÜN " + _dayCount;
    }

    // ===============================================
    // UYKU EFEKTÝ KONTROLÜ
    // ===============================================
    public void StartSleepSequence(System.Action onMidnightAction)
    {
        StartCoroutine(SleepRoutine(onMidnightAction));
    }

    private IEnumerator SleepRoutine(System.Action onMidnightAction)
    {
        float duration = 1.5f;
        float elapsed = 0f;

        // 1. Gözler kapanýyor
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / duration;
            if (_topEyelid != null) _topEyelid.style.translate = new Translate(0, new Length(Mathf.Lerp(-100, 0, percent), LengthUnit.Percent));
            if (_bottomEyelid != null) _bottomEyelid.style.translate = new Translate(0, new Length(Mathf.Lerp(100, 0, percent), LengthUnit.Percent));
            yield return null;
        }

        if (_dailyStatsContainer != null) _dailyStatsContainer.style.opacity = 1f;

        // 2. Arka planda zamaný atlat ve enerjiyi fulle
        onMidnightAction?.Invoke();

        yield return new WaitForSeconds(3f);
        if (_dailyStatsContainer != null) _dailyStatsContainer.style.opacity = 0f;

        // 3. Gözler açýlýyor
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / duration;
            if (_topEyelid != null) _topEyelid.style.translate = new Translate(0, new Length(Mathf.Lerp(0, -100, percent), LengthUnit.Percent));
            if (_bottomEyelid != null) _bottomEyelid.style.translate = new Translate(0, new Length(Mathf.Lerp(0, 100, percent), LengthUnit.Percent));
            yield return null;
        }
    }
}