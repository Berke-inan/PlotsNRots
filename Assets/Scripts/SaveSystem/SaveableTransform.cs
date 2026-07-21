using UnityEngine;

namespace PlotNRots.SaveSystem
{
    public class SaveableTransform : MonoBehaviour, ISaveable
    {
        [System.Serializable]
        public struct TransformData
        {
            public float[] position;
            public float[] rotation; // Arabalar için dönüş açısı eklendi
        }

        public object SaveState()
        {
            return new TransformData
            {
                position = new float[] { transform.position.x, transform.position.y, transform.position.z },
                rotation = new float[] { transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, transform.rotation.eulerAngles.z }
            };
        }

        public void LoadState(object state)
        {
            var data = (TransformData)state;
            Vector3 newPos = new Vector3(data.position[0], data.position[1], data.position[2]);
            Quaternion newRot = Quaternion.Euler(data.rotation[0], data.rotation[1], data.rotation[2]);

            // 1. Oyuncu için CharacterController kontrolü
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // 2. Araba vb. fiziksel objeler için Rigidbody kontrolü
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // Fizik hesaplamasını geçici olarak durdur

                // Unity 6 standardında hızı sıfırla (momentum kaynaklı geri fırlamayı önler)
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Pozisyon ve rotasyonu uygula
            transform.position = newPos;
            transform.rotation = newRot;

            // Bileşenleri geri aktif et
            if (cc != null) cc.enabled = true;
            if (rb != null) rb.isKinematic = false;
        }
    }
}