WaterWorks - Unity 6 / URP 17 Low Poly Update
=============================================

WHAT CHANGED
------------
1) Water_Volume.cs
   - RenderTargetHandle removed.
   - RTHandle + Render Graph path added.
   - Compatibility Mode remains supported.
   - Requests camera depth explicitly.
   - Uses Unity 6 Blitter / AddBlitPass workflow.

2) Water_Settings.cs
   - Can auto-size the volume from the lake MeshRenderer.
   - Adds configurable volumeDepth and horizontal padding.
   - Keeps the volume centered below the visible water surface.

3) Volumetric_Water.shader + Water_Volume.hlsl
   - Rewritten as a Unity 6 fullscreen blit shader.
   - Samples _BlitTexture, not legacy _MainTex.
   - Removed the old maximum-250-step ray march.
   - Uses a single ray/box intersection calculation instead.
   - Added low-poly color bands and broad stylized water noise.
   - Added very cheap optional refraction.

4) WaterSSR.hlsl
   - Updated for URP scene depth/opaque texture helpers.
   - SSR steps capped at 12.
   - Adds low-poly color quantization.

5) SSR_Water.shadergraph
   - Existing node structure and GUID are preserved.
   - Default look is adjusted for low-poly water.
   - Screen Space Reflections are OFF by default for performance.

6) SubGraphs
   - Existing GUIDs and graph connections are preserved.
   - Unity 6 can import/migrate the serialized graph data.
   - Their default values are kept unless they directly affect the low-poly surface preset.

INSTALL
-------
A) Back up the old WaterWorks folder first.
B) Copy the contents of this package over the matching files in WaterWorks.
C) Keep the supplied .meta files so existing references/GUIDs stay intact.
D) In the URP Asset enable Depth Texture.
E) The surface shader's Scene Color/refraction needs Opaque Texture if you continue using it.
F) Add/keep Water_Volume as a Renderer Feature and assign the Water_Volume material.
G) Put Water_Settings on the lake surface MeshRenderer.
H) Start volumeDepth around 15-30 for a lake and adjust to your terrain depth.

LOW-POLY STARTING POINT
-----------------------
Surface material:
- Screen Space Reflections: OFF
- Use Foam: ON
- Displacement Amount: ~0.12
- Normal Strength: ~0.06
- Wave Speed: ~0.08
- Wave Frequency: ~2.0
- Tiling: ~0.14
- Caustic Strength: ~0.35
- Transparency: ~0.82

Volume material:
- Density: 0.12-0.22
- Color Bands: 4-6
- Refraction: 0.001-0.004
- Depth Color Distance: 15-30

IMPORTANT
---------
Shader Graph files from older Unity versions can be migrated by Unity when opened/imported.
Do not manually change m_SGVersion numbers in the text files; Unity's importer owns those migrations.
