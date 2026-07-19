using UnityEngine;
using UnityEngine.UIElements;

public class VehicleUI : MonoBehaviour
{
    public VehicleController vehicleController;
    public UIDocument uiDocument;

    private VisualElement speedometerContainer;
    private VisualElement needle;
    private Label speedText;

    private void OnEnable()
    {
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            // Ana Yuvarlak Kasa (Boyutu 200x200)
            speedometerContainer = new VisualElement();
            speedometerContainer.style.position = Position.Absolute;
            speedometerContainer.style.bottom = 40;
            speedometerContainer.style.left = 40;
            speedometerContainer.style.width = 200;
            speedometerContainer.style.height = 200;
            speedometerContainer.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.9f);

            // Köþe Yuvarlatma (4 köþe ayrý ayrý yazýlmak zorundadýr)
            speedometerContainer.style.borderTopLeftRadius = 100;
            speedometerContainer.style.borderTopRightRadius = 100;
            speedometerContainer.style.borderBottomLeftRadius = 100;
            speedometerContainer.style.borderBottomRightRadius = 100;

            // Çerçeve Kalýnlýðý (4 kenar ayrý ayrý)
            speedometerContainer.style.borderTopWidth = 3;
            speedometerContainer.style.borderBottomWidth = 3;
            speedometerContainer.style.borderLeftWidth = 3;
            speedometerContainer.style.borderRightWidth = 3;

            // Çerçeve Rengi (4 kenar ayrý ayrý)
            speedometerContainer.style.borderTopColor = Color.white;
            speedometerContainer.style.borderBottomColor = Color.white;
            speedometerContainer.style.borderLeftColor = Color.white;
            speedometerContainer.style.borderRightColor = Color.white;

            speedometerContainer.style.display = DisplayStyle.None;

            // Kadrandaki Sayýlarý ve Çizgileri Oluþturma Matematikleri
            int maxDialSpeed = vehicleController != null ? Mathf.RoundToInt(vehicleController.maxSpeed) : 120;
            int step = 20; // 0, 20, 40, 60 diye artacak

            for (int i = 0; i <= maxDialSpeed; i += step)
            {
                float ratio = (float)i / maxDialSpeed;
                float angleDeg = Mathf.Lerp(-130f, 130f, ratio);
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // Kenardaki Beyaz Çizgiler (Tiks)
                VisualElement tick = new VisualElement();
                tick.style.position = Position.Absolute;
                tick.style.width = 4;
                tick.style.height = 12;
                tick.style.backgroundColor = Color.white;

                float tickRadius = 90f;
                tick.style.left = 100f + Mathf.Sin(angleRad) * tickRadius - 2f;
                tick.style.top = 100f - Mathf.Cos(angleRad) * tickRadius - 6f;
                tick.style.rotate = new Rotate(Angle.Degrees(angleDeg));
                speedometerContainer.Add(tick);

                // Kenardaki Sayýlar (10, 20, 30..)
                Label numLabel = new Label(i.ToString());
                numLabel.style.position = Position.Absolute;
                numLabel.style.fontSize = 14;
                numLabel.style.color = Color.white;
                numLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                numLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                float numRadius = 65f;
                numLabel.style.left = 100f + Mathf.Sin(angleRad) * numRadius - 15f;
                numLabel.style.top = 100f - Mathf.Cos(angleRad) * numRadius - 10f;
                numLabel.style.width = 30;
                speedometerContainer.Add(numLabel);
            }

            // Dijital Gösterge (Merkezin Altýna Kaydýrýldý)
            speedText = new Label("0");
            speedText.style.position = Position.Absolute;
            speedText.style.width = 200;
            speedText.style.top = 135;
            speedText.style.unityTextAlign = TextAnchor.MiddleCenter;
            speedText.style.fontSize = 28;
            speedText.style.color = Color.white;
            speedText.style.unityFontStyleAndWeight = FontStyle.Bold;

            Label unitText = new Label("KM/H");
            unitText.style.position = Position.Absolute;
            unitText.style.width = 200;
            unitText.style.top = 165;
            unitText.style.unityTextAlign = TextAnchor.MiddleCenter;
            unitText.style.fontSize = 12;
            unitText.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);

            // Kýrmýzý Ýbre
            needle = new VisualElement();
            needle.style.position = Position.Absolute;
            needle.style.width = 4;
            needle.style.height = 85;
            needle.style.backgroundColor = Color.red;
            needle.style.left = 98;
            needle.style.top = 15;
            needle.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));

            // Ýbrenin Merkez Noktasý (Göbek)
            VisualElement centerDot = new VisualElement();
            centerDot.style.position = Position.Absolute;
            centerDot.style.width = 20;
            centerDot.style.height = 20;
            centerDot.style.left = 90;
            centerDot.style.top = 90;
            centerDot.style.backgroundColor = new Color(0.8f, 0.1f, 0.1f, 1f);

            // Göbek yuvarlatma (4 köþe ayrý ayrý)
            centerDot.style.borderTopLeftRadius = 10;
            centerDot.style.borderTopRightRadius = 10;
            centerDot.style.borderBottomLeftRadius = 10;
            centerDot.style.borderBottomRightRadius = 10;

            speedometerContainer.Add(speedText);
            speedometerContainer.Add(unitText);
            speedometerContainer.Add(needle);
            speedometerContainer.Add(centerDot);

            uiDocument.rootVisualElement.Add(speedometerContainer);
        }
    }

    private void Update()
    {
        if (vehicleController == null || speedometerContainer == null) return;

        if (vehicleController.isPlayerInside)
        {
            speedometerContainer.style.display = DisplayStyle.Flex;

            float currentSpeed = vehicleController.currentSpeed;
            float maxSpeed = vehicleController.maxSpeed;

            int displaySpeed = Mathf.RoundToInt(currentSpeed);
            speedText.text = displaySpeed.ToString();

            float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed);
            float angle = Mathf.Lerp(-130f, 130f, speedRatio);

            needle.style.rotate = new Rotate(Angle.Degrees(angle));
        }
        else
        {
            speedometerContainer.style.display = DisplayStyle.None;
        }
    }
}