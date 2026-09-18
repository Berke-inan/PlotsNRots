using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SeasonManager))]
public class SeasonManagerEditor : Editor
{
    private float previewIntensity = 0.8f;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        var manager = (SeasonManager)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime weather controls", EditorStyles.boldLabel);
        previewIntensity = EditorGUILayout.Slider("Intensity", previewIntensity, 0, 1);
        using (new EditorGUI.DisabledScope(!manager.SimulateLocally))
        {
            foreach (WeatherType weather in System.Enum.GetValues(typeof(WeatherType)))
                if (GUILayout.Button(weather.ToString())) manager.SetWeather(weather, previewIntensity);
        }
        if (GUILayout.Button("Snap visuals to current state")) manager.GetComponent<WeatherVisualsManager>()?.ApplyWeatherImmediate();
        var snow = manager.GetComponent<SnowAccumulationManager>();
        if (snow != null)
        {
            EditorGUILayout.LabelField("Ground snow", snow.Amount.ToString("F3"));
            if (GUILayout.Button("Set ground snow to 0")) snow.SetAmount(0);
            if (GUILayout.Button("Set ground snow to 1")) snow.SetAmount(1);
        }
        EditorGUILayout.HelpBox("Use these buttons to emit weather events. Editing serialized state fields alone does not emit events. Runtime changes are temporary unless saved through the game save menu.", MessageType.Info);
    }
}
