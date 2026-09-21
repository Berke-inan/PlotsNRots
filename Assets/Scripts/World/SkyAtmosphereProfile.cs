using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Plots & Rots/Sky Atmosphere Profile")]
public class SkyAtmosphereProfile : ScriptableObject
{
    [Header("Day palette")]
    [SerializeField] private Color dayZenith = new Color(.11f, .34f, .62f);
    [SerializeField] private Color dayMiddle = new Color(.32f, .59f, .8f);
    [SerializeField] private Color dayHorizon = new Color(.72f, .83f, .88f);
    [Header("Night palette")]
    [SerializeField] private Color nightZenith = new Color(.008f, .016f, .045f);
    [SerializeField] private Color nightMiddle = new Color(.025f, .045f, .1f);
    [SerializeField] private Color nightHorizon = new Color(.09f, .12f, .2f);
    [Header("Dawn / dusk")]
    [SerializeField] private Color twilightZenith = new Color(.13f, .19f, .34f);
    [SerializeField] private Color twilightMiddle = new Color(.58f, .39f, .43f);
    [SerializeField] private Color twilightHorizon = new Color(1f, .61f, .34f);
    [Header("Cloud and light")]
    [SerializeField] private Color cloudLight = new Color(.98f, .97f, .93f);
    [SerializeField] private Color cloudShadow = new Color(.48f, .62f, .75f);
    [SerializeField] private Color nightCloudLight = new Color(.14f, .2f, .3f);
    [SerializeField] private Color nightCloudShadow = new Color(.035f, .055f, .1f);
    [SerializeField] private Color sunNoon = new Color(1, .95f, .84f);
    [SerializeField] private Color sunHorizon = new Color(1, .51f, .24f);
    [SerializeField] private Color moonTint = new Color(.57f, .72f, 1);
    [Header("Cloud layers")]
    [SerializeField] private Texture2D cloudMask;
    [Min(.1f)] [SerializeField] private float macroScale = .95f;
    [FormerlySerializedAs("detailScaleRatio"), Min(1)] [SerializeField] private float lowerLayerScale = 1.72f;
    [Range(.2f, 2)] [SerializeField] private float farLayerSpeed = .55f;
    [Range(1, 3)] [SerializeField] private float nearLayerSpeed = 1.25f;
    [SerializeField] private Vector2 windDirection = new Vector2(1, .3f);
    [Min(0)] [SerializeField] private float cloudSpeed = .008f;
    [Range(.01f, .25f)] [SerializeField] private float coverageSoftness = .075f;
    [Range(0, 1)] [SerializeField] private float breakupStrength = .16f;
    [Range(2, 8)] [SerializeField] private int cloudToneSteps = 4;
    [Range(0, 2)] [SerializeField] private float highlightStrength = .9f;
    [Range(0, 1)] [SerializeField] private float undersideDarkness = .62f;
    [Range(.04f, .4f)] [SerializeField] private float horizonFade = .18f;
    [Header("Celestial details")]
    [Range(.1f, 3)] [SerializeField] private float sunAngularRadius = .9f;
    [Range(.1f, 3)] [SerializeField] private float moonAngularRadius = 1.1f;
    [Range(0, 1)] [SerializeField] private float sunHalo = .18f;
    [Range(0, 1)] [SerializeField] private float moonHalo = .06f;
    [Range(0, 1)] [SerializeField] private float starDensity = .018f;
    [Range(0, 3)] [SerializeField] private float starBrightness = .9f;

    public Color DayZenith => dayZenith;
    public Color DayMiddle => dayMiddle;
    public Color DayHorizon => dayHorizon;
    public Color NightZenith => nightZenith;
    public Color NightMiddle => nightMiddle;
    public Color NightHorizon => nightHorizon;
    public Color TwilightZenith => twilightZenith;
    public Color TwilightMiddle => twilightMiddle;
    public Color TwilightHorizon => twilightHorizon;
    public Color CloudLight => cloudLight;
    public Color CloudShadow => cloudShadow;
    public Color NightCloudLight => nightCloudLight;
    public Color NightCloudShadow => nightCloudShadow;
    public Color SunNoon => sunNoon;
    public Color SunHorizon => sunHorizon;
    public Color MoonTint => moonTint;
    public Texture2D CloudMask => cloudMask;
    public float MacroScale => macroScale;
    public float LowerLayerScale => lowerLayerScale;
    public float FarLayerSpeed => farLayerSpeed;
    public float NearLayerSpeed => nearLayerSpeed;
    public Vector2 WindDirection => windDirection;
    public float CloudSpeed => cloudSpeed;
    public float CoverageSoftness => coverageSoftness;
    public float BreakupStrength => breakupStrength;
    public int CloudToneSteps => cloudToneSteps;
    public float HighlightStrength => highlightStrength;
    public float UndersideDarkness => undersideDarkness;
    public float HorizonFade => horizonFade;
    public float SunAngularRadius => sunAngularRadius;
    public float MoonAngularRadius => moonAngularRadius;
    public float SunHalo => sunHalo;
    public float MoonHalo => moonHalo;
    public float StarDensity => starDensity;
    public float StarBrightness => starBrightness;

    public static float Daylight(float solarElevation) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.16f, .22f, solarElevation));
    public static float Twilight(float solarElevation) => 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.015f, .28f, Mathf.Abs(solarElevation)));
    public static float Stars(float solarElevation) => 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.2f, -.04f, solarElevation));
}
