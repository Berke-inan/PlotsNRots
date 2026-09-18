# Validation record — 2026-09-17

Status: **Static validation passed; Unity runtime/build validation blocked by unavailable Editor/project.**

## Baseline and scope

Input: WaterWorks_Unity6_LowPoly_v3(1).zip and the supplied Unity screenshot.
User target: Unity 6.3 LTS 6000.3.19f1, URP. Screenshot shows DX12.
The archive is an asset/code bundle, not a complete project: no Packages manifest,
ProjectSettings, scene, prefab, or assigned surface material assets. Actual URP package
version and renderer feature configuration are unknown. No Unity MCP or Editor is available.
No assumptions were made from earlier conversations about this water implementation.

Static evidence of banding: surface shader quantized depth, wave tint and specular;
volume shader quantized both depth tint and absorption. The original surface also
composited scene color before transparent blending. These paths were changed.
The exact original screenshot has not been reproduced in the engine.

## Executed checks

- Tree-sitter C# grammar parsed **8 C# files**, zero syntax error nodes.
  This is syntax validation, **not C# type checking or Unity API compilation**.
- Checked duplicate public class names within this package: none.
- Checked local HLSL include targets and delimiter balance: passed.
  This is **not shader compilation**.
- Checked every `_WW...` shader property written by WaterSurface against the active
  shader/include declarations: passed.
- Byte-compared all **11 supplied original .meta files**: unchanged.
- Byte-compared all **6 original Shader Graph/SubGraph files**: unchanged.
- Reviewed the diff for relevant changes only; the pre-existing Water_Volume.cs renderer
  feature and WaterSSR.hlsl are retained. Their engine compatibility is not certified.
- New files get new .meta files. Pre-existing files whose .meta was absent from the input
  intentionally ship without a new GUID; users must keep their project's existing .meta.

Machine-readable result: static-validation.json.

## Not executed

Unity C# API compilation, URP shader compilation, wave math menu assertions,
Play Mode, scene reload, physics contact tests, camera visual tests, DX12 Player build,
GPU profiling and target device checks were **not run**.

## Included validation aids

Tools > WaterWorks > 4 - Run Wave Math Checks runs 17 assertions in a disposable preview
scene: continuity at birth, finite-difference normal agreement (12 positions), outward
propagation, expiry, bounded capacity and bounded superposition. These are provided for
Unity execution and were not counted as passing tests here.

Tools > WaterWorks > 3 creates a boat proxy on the selected lake. Its component context
menu applies a 5 m/s forward velocity change in Play Mode. The Turkish guide contains
expected results and a reproducible manual matrix including 500/1200 kg cubes,
compound colliders, disable/re-enable and a second Play session.

## Important limits

The wake is an analytic ripple approximation shared by CPU and GPU, not a full fluid solver.
A sparse mesh samples that height field only at vertices; the adaptive grid improves
local agreement, but distant or off-target boats can differ visually from exact CPU sampling.
At most 32 ripple events exist per water surface; excess events replace the oldest.
A single adaptive mesh has one focus. No measured frame-rate/performance claims are made.
The legacy fullscreen volume is a single shared-material water volume; multi-lake
underwater rendering, stereo/XR and near-waterline wave-matched underwater transitions
remain outside this change.

Original serialized field names and public height/normal methods are retained. Automatic
BoxCollider buoyancy now samples the bottom and uses the box height by default. Existing
buoyancy tuning must be checked; the old half-submersion documentation did not match its
old sample positions. The new damping is a gameplay stabilizer with an acceleration cap.

## Primary technical reference

Unity 6.3 URP manual: depth texture and world-position reconstruction, used to avoid
perspective-only eye-depth assumptions in the surface shader:
https://docs.unity3d.com/6000.3/Documentation/Manual/urp/writing-shaders-urp-reconstruct-world-position.html
