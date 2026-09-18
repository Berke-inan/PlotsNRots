WATERWORKS - UNITY 6 LOW POLY v3
================================

WHAT'S NEW
- 3-direction blended vertex waves.
- Wave-derived normals so highlights/refraction move with the wave slope.
- WaterSurface.cs mirrors the shader wave math on CPU.
- WaterPhysicsVolume.cs uses a cheap BoxCollider trigger for water detection.
- BuoyantObject.cs adds mass-aware buoyancy, wave following, tilt and water drag.

IMPORTANT DESIGN NOTE
The BoxCollider does NOT deform with every GPU vertex. It is only a water-volume trigger.
BuoyantObject samples WaterSurface.GetWaterHeight/GetWaterNormal, so physics follows the same
wave function as the visible shader. This is much cheaper and more stable for a large lake.

LAKE SETUP
1. Lake_Water MeshRenderer -> material using WaterWorks/LowPolyWater_URP6.
2. Add Water_Settings (for the fullscreen volume effect, if used).
3. Add WaterSurface.
4. Add BoxCollider.
5. Add WaterPhysicsVolume. Is Trigger is set automatically.
6. Suggested WaterPhysicsVolume: Water Depth 25, Padding 2, Top Padding 0.5.

FLOATING TEST CUBE
1. Create Cube.
2. Add Rigidbody.
3. Keep BoxCollider.
4. Add BuoyantObject.
5. Drop it into the lake.

For a 1x1x1 m cube, water density 1000 means roughly:
- mass 500 kg -> floats around half submerged.
- mass 900 kg -> floats low in the water.
- mass > 1000 kg -> tends to sink (depending on buoyancy multiplier/settings).

BOAT SETUP
For better boat tilt, create 4 empty child objects near the hull bottom/corners and assign them
to BuoyantObject > Buoyancy Points. Different wave heights at those points naturally create roll/pitch.

SUGGESTED LOW-POLY MATERIAL VALUES
Wave Scale: 0.14
Wave Speed: 0.28
Wave Height: 0.11
Secondary Wave Strength: 0.55
Detail Wave Strength: 0.28
Foam Width: 1.1
Foam Hardness: 4
Color Bands: 5
Refraction: 0.002

NOTE
Character swimming is intentionally not forced into this Rigidbody buoyancy component. A player
controller normally needs its own swim-state integration; that can use WaterSurface.GetWaterHeight.
