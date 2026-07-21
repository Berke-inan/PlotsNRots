using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace PlotNRots.SaveSystem
{
    public class SaveableEntity : MonoBehaviour
    {
        [Tooltip("Bunu boş bırakın, Inspector'dan sağ tıklayıp ID Oluştur deyin.")]
        [SerializeField] private string id = string.Empty;
        public string Id => id;

        // Tasarımcılar Unity arayüzünden sağ tıklayıp ID üretebilsin diye
        [ContextMenu("Create ID")]
        private void GenerateId()
        {
            if (string.IsNullOrEmpty(id))
            {
                id = System.Guid.NewGuid().ToString();
            }
        }

        private void OnEnable()
        {
            // Obje sahneye geldiğinde (Örn: Yeni bir kule inşa edildiğinde) kendini listeye ekle
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.RegisterEntity(this);
            }
        }

        private void OnDisable()
        {
            // Obje silindiğinde (Örn: Duvar yıkıldığında) kendini listeden çıkar
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.UnregisterEntity(this);
            }
        }

        // Üzerindeki tüm ISaveable (lego) scriptlerini bul ve verilerini topla
        public string CaptureState()
        {
            var state = new Dictionary<string, object>();
            foreach (var saveable in GetComponents<ISaveable>())
            {
                state[saveable.GetType().ToString()] = saveable.SaveState();
            }

            // Verileri TypeNameHandling ayarıyla JSON'a çevir ki tipler (float, Vector3 vb.) kaybolmasın
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            return JsonConvert.SerializeObject(state, settings);
        }

        // Gelen JSON verisini çöz ve üzerindeki legolara dağıt
        public void RestoreState(string stateJson)
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            var state = JsonConvert.DeserializeObject<Dictionary<string, object>>(stateJson, settings);

            if (state == null) return;

            foreach (var saveable in GetComponents<ISaveable>())
            {
                string typeName = saveable.GetType().ToString();
                if (state.ContainsKey(typeName))
                {
                    saveable.LoadState(state[typeName]);
                }
            }
        }
    }
}