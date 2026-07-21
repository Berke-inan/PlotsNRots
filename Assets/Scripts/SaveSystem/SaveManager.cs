using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace PlotNRots.SaveSystem
{
    public struct SaveFileInfo
    {
        public string FileName;
        public string FullPath;
        public DateTime LastModified;
    }

    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Header("Otomatik Kayıt Ayarları")]
        [Tooltip("Dakika cinsinden otomatik kayıt süresi. Test için 0.16 (10 saniye) yapabilirsiniz.")]
        [SerializeField] private float autoSaveIntervalMinutes = 5f;

        [Tooltip("Klasörde tutulacak maksimum otomatik kayıt sayısı.")]
        [SerializeField] private int maxAutoSaves = 5;

        private float _autoSaveTimer;
        private GameData _gameData;
        private string _saveDirectory;
        private HashSet<SaveableEntity> _registeredEntities = new HashSet<SaveableEntity>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this.gameObject);

            _saveDirectory = Path.Combine(Application.persistentDataPath, "Saves");
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
            }

            _gameData = new GameData();
            _autoSaveTimer = autoSaveIntervalMinutes * 60f;
        }

        private void Update()
        {
            if (autoSaveIntervalMinutes > 0)
            {
                _autoSaveTimer -= Time.deltaTime;
                if (_autoSaveTimer <= 0)
                {
                    PerformAutoSave();
                    _autoSaveTimer = autoSaveIntervalMinutes * 60f;
                }
            }
        }

        private void PerformAutoSave()
        {
            // İsmine o anın saatini ekleyerek benzersiz bir dosya oluştur
            string autoSaveName = "AutoSave_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            SaveGame(autoSaveName);

            // Kayıt bittikten hemen sonra limit kontrolü yap
            CleanupOldAutoSaves();
        }

        private void CleanupOldAutoSaves()
        {
            if (maxAutoSaves <= 0) return;

            DirectoryInfo dir = new DirectoryInfo(_saveDirectory);
            // Sadece "AutoSave_" ile başlayan dosyaları bul ve tarihe göre yeniden eskiye sırala
            FileInfo[] autoSaveFiles = dir.GetFiles("AutoSave_*.json")
                                          .OrderByDescending(f => f.LastWriteTime)
                                          .ToArray();

            // Eğer limit aşıldıysa, kuyrukta kalan eski dosyaları sil
            if (autoSaveFiles.Length > maxAutoSaves)
            {
                for (int i = maxAutoSaves; i < autoSaveFiles.Length; i++)
                {
                    try
                    {
                        autoSaveFiles[i].Delete();
                        Debug.Log("Eski otomatik kayıt silindi: " + autoSaveFiles[i].Name);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Eski otomatik kayıt silinirken hata: " + e.Message);
                    }
                }
            }
        }

        public void RegisterEntity(SaveableEntity entity) { _registeredEntities.Add(entity); }
        public void UnregisterEntity(SaveableEntity entity) { _registeredEntities.Remove(entity); }

        public void SaveGame(string saveName)
        {
            foreach (var entity in _registeredEntities)
            {
                if (string.IsNullOrEmpty(entity.Id)) continue;
                _gameData.savedEntities[entity.Id] = entity.CaptureState();
            }

            string safeName = string.Join("_", saveName.Split(Path.GetInvalidFileNameChars()));
            string filePath = Path.Combine(_saveDirectory, safeName + ".json");

            string json = JsonConvert.SerializeObject(_gameData, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, json);
            Debug.Log($"Oyun kaydedildi: {safeName}");
        }

        public void LoadGame(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning("Yüklenecek kayıt dosyası bulunamadı.");
                return;
            }

            string json = File.ReadAllText(filePath);
            _gameData = JsonConvert.DeserializeObject<GameData>(json);

            foreach (var entity in _registeredEntities)
            {
                if (string.IsNullOrEmpty(entity.Id)) continue;

                if (_gameData.savedEntities.ContainsKey(entity.Id))
                {
                    entity.RestoreState(_gameData.savedEntities[entity.Id]);
                }
            }
            Debug.Log("Oyun yüklendi: " + Path.GetFileNameWithoutExtension(filePath));
        }

        public List<SaveFileInfo> GetAvailableSaves()
        {
            DirectoryInfo dir = new DirectoryInfo(_saveDirectory);
            FileInfo[] files = dir.GetFiles("*.json");

            return files.Select(f => new SaveFileInfo
            {
                FileName = Path.GetFileNameWithoutExtension(f.Name),
                FullPath = f.FullName,
                LastModified = f.LastWriteTime
            })
            .OrderByDescending(s => s.LastModified)
            .ToList();
        }
    }
}