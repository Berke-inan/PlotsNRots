#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class RingGenerator : MonoBehaviour
{
    // Bu kod Unity'nin üst menüsüne yeni bir tuş ekler
    [MenuItem("Tools/Sprinkler Halkası Üret")]
    public static void GenerateRing()
    {
        int size = 512;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = 250f;
        float innerRadius = 230f; // Halkanın kalınlığı

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(center, new Vector2(x, y));
                float alpha = 0f;

                // Halka sınırları içindeyse görünür (beyaz) yap, değilse şeffaf
                if (dist <= outerRadius && dist >= innerRadius)
                {
                    alpha = 1f;
                    // Kenarları yumuşatma (Anti-aliasing)
                    if (dist > outerRadius - 2f) alpha = (outerRadius - dist) / 2f;
                    if (dist < innerRadius + 2f) alpha = (dist - innerRadius) / 2f;
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(Application.dataPath + "/SprinklerHalka.png", bytes);
        AssetDatabase.Refresh();
        Debug.Log("SprinklerHalka.png başarıyla oluşturuldu! Project penceresini kontrol et.");
    }
}
#endif