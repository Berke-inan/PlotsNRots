using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Engine fixtures plus on-disk integration assertions; not a Game View acceptance test.
public static class WeatherIntegrationValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);

    private static string ReadSource(string path) => File.ReadAllText(File.Exists("ValidationInput/" + path) ? "ValidationInput/" + path : path);

    [MenuItem("Tools/Plots & Rots/Weather/Run integration regression checks")]
    public static void Run()
    {
        SkyWeatherValidation.Run(); // The original 39 + 18 checks remain unchanged.
        checks = 0;
        GameObject world = null, player = null, camera = null, vehicle = null;
        WeatherVisualsManager visuals = null;
        var previousVehicleCamera = VehicleCameraManager.Instance;
        try
        {
            string scene = ReadSource("Assets/Scenes/SampleScene.unity").Replace("\r\n", "\n");
            Check(scene.Contains("lightningLight: {fileID: 900100032}"), "serialized lightning reference");
            Check(scene.Contains("--- !u!108 &900100032"), "serialized dedicated light exists");
            Check(!scene.Contains("sunLight: {fileID: 900100032}") && !scene.Contains("moonLight: {fileID: 900100032}"), "lightning is not a celestial light");
            Check(scene.Contains("--- !u!198 &890948984\n") && scene.Contains("--- !u!198 &627366671\n"), "both emitters are scene components, not stripped Player components");
            Check(!ReadSource("Assets/Prefabs/Character/Player.prefab").Contains("m_Name: WeatherEffects"), "Player has no duplicate rig");
            Check(scene.Contains("maxRainEmission: 100") && scene.Contains("maxSnowEmission: 100"), "scene emission limits retained");
            Check(scene.Contains("gameplayCamera: {fileID: 330585546}"), "follower uses output camera");

            world = new GameObject("Integration fixture");
            var manager = world.AddComponent<SeasonManager>(); Call(manager, "Awake");
            var rig = new GameObject("Rig"); rig.transform.SetParent(world.transform);
            var rain = new GameObject("Rain"); rain.transform.SetParent(rig.transform);
            var snow = new GameObject("Snow"); snow.transform.SetParent(rig.transform);
            var rainParticles = rain.AddComponent<ParticleSystem>(); var snowParticles = snow.AddComponent<ParticleSystem>();
            var rainMain = rainParticles.main; rainMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var snowMain = snowParticles.main; snowMain.simulationSpace = ParticleSystemSimulationSpace.World;
            player = new GameObject("Player fixture"); camera = new GameObject("Output camera fixture");
            var follower = rig.AddComponent<PrecipitationFollower>(); Set(follower, "gameplayCamera", camera.transform);
            player.SetActive(false); camera.transform.position = new Vector3(10, 3, 20); follower.FollowNow();
            Check(rain.activeInHierarchy && snow.activeInHierarchy, "inactive Player cannot disable emitters");
            Check(rig.transform.position == camera.transform.position, "rig follows output camera with Player disabled");
            rainParticles.Simulate(0, false, true);
            rainParticles.SetParticles(new[] { new ParticleSystem.Particle { position = Vector3.one, startLifetime = 10, remainingLifetime = 10, startSize = 1 } }, 1);
            var beforeParticles = new ParticleSystem.Particle[1];
            int beforeCount = rainParticles.GetParticles(beforeParticles);
            Check(beforeCount == 1, "world particle fixture initialized");
            camera.transform.position += Vector3.right * 10; camera.transform.rotation = Quaternion.Euler(30, 80, 0); follower.FollowNow();
            var particles = new ParticleSystem.Particle[1]; int afterCount = rainParticles.GetParticles(particles);
            Check(afterCount == 1 && Vector3.Distance(particles[0].position, beforeParticles[0].position) < .001f, "existing world-space particles do not move with rig: " + beforeParticles[0].position + " -> " + particles[0].position);
            Check(rig.transform.rotation == Quaternion.identity, "camera rotation is not inherited");

            var lightObject = new GameObject("Lightning fixture"); lightObject.transform.SetParent(world.transform);
            var flash = lightObject.AddComponent<Light>(); flash.type = LightType.Directional; flash.intensity = 0;
            visuals = world.AddComponent<WeatherVisualsManager>(); Call(visuals, "Awake");
            Set(visuals, "rainParticles", rainParticles); Set(visuals, "snowParticles", snowParticles);
            Set(visuals, "lightningLight", flash); Set(visuals, "maxRainEmission", 100f); Set(visuals, "maxSnowEmission", 100f);
            Call(visuals, "OnEnable"); Call(visuals, "Start");
            foreach (float intensity in new[] { .25f, .5f, .85f, 1f })
            {
                manager.ApplySnapshot(new SeasonManager.WeatherSaveData { version = 2, currentWeather = WeatherType.Rainy, currentTemperature = 10, weatherIntensity = intensity });
                Check(Mathf.Approximately(rainParticles.emission.rateOverTime.constant, intensity * 100), "rain immediate emission " + intensity);
            }
            manager.ApplySnapshot(new SeasonManager.WeatherSaveData { version = 2, currentWeather = WeatherType.Snowy, currentTemperature = -5, weatherIntensity = .85f });
            Check(Mathf.Approximately(snowParticles.emission.rateOverTime.constant, 85) && rainParticles.emission.rateOverTime.constant == 0, "snow restore immediately switches emitters");
            manager.ApplySnapshot(new SeasonManager.WeatherSaveData { version = 2, currentWeather = WeatherType.Storm, currentTemperature = 10, weatherIntensity = 1 });
            Check(flash.intensity == 0, "storm restore never flashes immediately");
            var storm = (IEnumerator)Call(visuals, "ThunderstormRoutine");
            Check(storm.MoveNext() && storm.Current is WaitForSeconds && flash.intensity == 0, "storm first yields a wait");
            storm.MoveNext(); Check(flash.intensity > 0, "scheduled lightning drives dedicated light");
            manager.SetWeather(WeatherType.Sunny, 0);
            Check(flash.intensity == 0, "leaving storm cancels flash immediately");
            visuals.ApplyWeatherImmediate();
            Check(rainParticles.emission.rateOverTime.constant == 0 && snowParticles.emission.rateOverTime.constant == 0 && !rainParticles.isPlaying && !snowParticles.isPlaying, "Sunny clears precipitation");

            // Exercise the actual enter/switch/exit methods without a running vehicle physics loop.
            vehicle = new GameObject("Vehicle fixture");
            var vehicleCamera = vehicle.AddComponent<VehicleCameraManager>(); VehicleCameraManager.Instance = vehicleCamera;
            vehicleCamera.fpsCameraObj = new GameObject("Vehicle FPS"); vehicleCamera.fpsCameraObj.transform.SetParent(vehicle.transform);
            vehicleCamera.tpsCameraObj = new GameObject("Vehicle TPS"); vehicleCamera.tpsCameraObj.transform.SetParent(vehicle.transform);
            var interaction = vehicle.AddComponent<VehicleInteractable>(); interaction.vehicleController = vehicle.AddComponent<VehicleController>();
            Set(interaction, "vehicleColliders", Array.Empty<Collider>());
            string before = JsonUtility.ToJson(manager.SaveState());
            player.SetActive(true); interaction.EnterVehicle(player); vehicleCamera.SwitchCamera(true); vehicleCamera.SwitchCamera(false);
            Check(!player.activeSelf && rig.activeInHierarchy, "vehicle enter/FPS/TPS retains scene rig");
            interaction.ExitVehicle();
            Check(player.activeSelf && rig.activeInHierarchy, "vehicle exit retains rig");
            Check(before == JsonUtility.ToJson(manager.SaveState()), "vehicle lifecycle does not mutate weather snapshot");
            Debug.Log($"Weather integration checks passed: {checks}; plus original 39 + 18.");
        }
        finally
        {
            if (visuals != null) Call(visuals, "OnDisable");
            VehicleCameraManager.Instance = previousVehicleCamera;
            foreach (var go in new[] { vehicle, camera, player, world }) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
