using UnityEngine;

namespace PlotNRots.SaveSystem
{
    public class SaveableTransform : MonoBehaviour, ISaveable
    {
        [System.Serializable]
        public struct TransformData
        {
            public float[] position; // Vector3 JSON'a bazen düzgün serileşmez, float array en güvenlisidir.
        }

        public object SaveState()
        {
            return new TransformData
            {
                position = new float[] { transform.position.x, transform.position.y, transform.position.z }
            };
        }

        public void LoadState(object state)
        {
            var data = (TransformData)state;

            // Unity'nin fizik motoruyla çakışmayı önlemek için CC kontrolü
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            transform.position = new Vector3(data.position[0], data.position[1], data.position[2]);

            if (cc != null) cc.enabled = true;
        }
    }
}