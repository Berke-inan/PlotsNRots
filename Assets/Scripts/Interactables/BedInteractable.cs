using UnityEngine;

public class BedInteractable : MonoBehaviour
{
    [Header("Uyku Zamaný Ayarlarý")]
    public float earliestSleepHour = 22f; // 22:00
    public float wakeUpHour = 7f;         // 07:00

    public Transform sleepPosition;
    public Transform wakeUpPosition;

    private bool _isSleeping = false;

    public void InteractWithBed(GameObject player)
    {
        if (_isSleeping) return;

        float time = DayNightCycleManager.Instance.currentTime;
        if (time >= earliestSleepHour || time < wakeUpHour)
        {
            _isSleeping = true;

            // Oyuncuyu yatýr
            player.transform.position = sleepPosition.position;
            player.transform.rotation = sleepPosition.rotation;

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            // HUD Manager'a animasyonu baþlatmasýný ve ekran karardýðýnda ne yapacaðýný söylüyoruz
            MainHUDManager.Instance.StartSleepSequence(() =>
            {
                // Ekran Siyahken Yapýlacaklar:
                DayNightCycleManager.Instance.Sleep(); // Senin kodundaki zamaný atlatma
                player.GetComponent<PlayerStats>().ReplenishStatsOnSleep();

                // Oyuncuyu ayaða kaldýr
                player.transform.position = wakeUpPosition.position;
                player.transform.rotation = wakeUpPosition.rotation;

                if (controller != null) controller.enabled = true;
                _isSleeping = false;
            });
        }
        else
        {
            Debug.Log("Sadece geceleri uyuyabilirsin!");
        }
    }
}