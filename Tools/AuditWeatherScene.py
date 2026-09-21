"""Read-only audit of serialized SampleScene/prefab references, not a Unity runtime test.
Run from the repository root: python Tools/AuditWeatherScene.py
Only writes Docs/SampleSceneWeatherAudit.json. Does not save Unity assets or touch git.
"""
from pathlib import Path
import hashlib
import json
import re
from collections import Counter

ROOT = Path(__file__).resolve().parents[1]
SCENE = "Assets/Scenes/SampleScene.unity"
PLAYER = "Assets/Prefabs/Character/Player.prefab"


def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig", errors="replace")


def field(text, key):
    match = re.search(r"^  " + re.escape(key) + r": (.*)$", text, re.M)
    return match[1] if match else None


def refs(text):
    return re.findall(r"\{fileID: (-?\d+)(?:, guid: (\w+), type: \d+)?\}", text or "")


def documents(path):
    return {m[2]: {"class": m[1], "text": m[3]} for m in re.finditer(
        r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)", read(path), re.M | re.S)}


assets = {}
for meta in (ROOT / "Assets").rglob("*.meta"):
    match = re.search(r"^guid: (\w+)", meta.read_text(errors="replace"), re.M)
    if match:
        assets[match[1]] = meta.relative_to(ROOT).as_posix()[:-5]

# Resolve referenced package shaders/materials too; an Assets-only GUID index would
# incorrectly label ordinary URP Lit references as missing.
for suffix in ("*.shader.meta", "*.shadergraph.meta", "*.mat.meta"):
    for meta in (ROOT / "Library/PackageCache").rglob(suffix):
        match = re.search(r"^guid: (\w+)", meta.read_text(errors="replace"), re.M)
        if match:
            assets.setdefault(match[1], meta.relative_to(ROOT).as_posix()[:-5])


def asset_ref(value):
    found = refs(value)
    if not found:
        return None
    file_id, guid = found[0]
    return {"fileID": file_id, "asset": assets.get(guid, guid or SCENE),
            "assetExists": bool(guid in assets) if guid else None}


scene = documents(SCENE)
player = documents(PLAYER)


def hierarchy(docs, component_id):
    text = docs[component_id]["text"]
    go = refs(field(text, "m_GameObject"))
    if not go:
        return None
    go_id = go[0][0]
    name = field(docs[go_id]["text"], "m_Name")
    transform = next((d for d in docs.values() if d["class"] in ("4", "224")
                      and refs(field(d["text"], "m_GameObject")) == [(go_id, "")]), None)
    parent = refs(field(transform["text"], "m_Father")) if transform else []
    if parent and parent[0][0] != "0" and parent[0][0] in docs:
        return hierarchy(docs, parent[0][0]) + "/" + name
    return name


visual = scene["403068533"]["text"]
clock = scene["2147441290"]["text"]
report = {"scope": "Serialized files only. Does not evaluate runtime, import models or fully merge prefab overrides.",
          "inputsSHA256": {p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest()
                           for p in [SCENE, PLAYER, "Assets/Scripts/Vehicle/VehicleInteractable.cs"]},
          "weatherFields": {k: field(visual, k) for k in ["maxRainEmission", "maxSnowEmission", "maxRainVolume",
              "lightningLight", "windZone", "windAudio", "precipitationAnchor", "transitionDuration", "stormThreshold"]},
          "particles": [], "lights": [], "terrain": [], "materials": [], "cameras": []}
for key in ["rainParticles", "snowParticles"]:
    scene_id = refs(field(visual, key))[0][0]
    source = asset_ref(field(scene[scene_id]["text"], "m_CorrespondingSourceObject"))
    if source and source["fileID"] != "0":
        assert source["asset"] == PLAYER and source["fileID"] in player, key
        docs, pid = player, source["fileID"]
    else:
        docs, pid = scene, scene_id
        source = {"fileID": pid, "asset": SCENE, "assetExists": True}
    assert docs[pid]["class"] == "198"
    report["particles"].append({"field": key, "sceneFileID": scene_id, "source": source,
        "hierarchy": hierarchy(docs, pid),
        **{k: field(docs[pid]["text"], k) for k in ["looping", "playOnAwake", "moveWithTransform", "lengthInSec"]}})
for key in ["sunLight", "moonLight"]:
    light_id = refs(field(clock, key))[0][0]
    assert scene[light_id]["class"] == "108"
    report["lights"].append({"field": key, "sceneFileID": light_id, "hierarchy": hierarchy(scene, light_id),
        **{k: field(scene[light_id]["text"], k) for k in ["m_Enabled", "m_Type", "m_Intensity"]}})
report["rainClip"] = asset_ref(field(visual, "rainSoundClip"))
thunder = re.search(r"^  thunderSounds:\n((?:  - .*\n)+)", visual, re.M)
report["thunderClips"] = [asset_ref(line) for line in thunder[1].splitlines()] if thunder else []
report["skybox"] = asset_ref(field(read(SCENE), "m_SkyboxMaterial"))
report["profiles"] = {k: asset_ref(field(visual, k)) for k in ["appearanceProfile"]}
report["profiles"]["atmosphere"] = asset_ref(field(scene["900000004"]["text"], "profile"))
report["saveIDs"] = re.findall(r"^  id: (world-[^\n]+)", read(SCENE), re.M)
report["seasonProfileReferences"] = [asset_ref(line) for line in re.search(
    r"^  seasonProfiles:\n((?:  - .*\n)+)", scene["403068534"]["text"], re.M)[1].splitlines()]
report["controllerClockFileID"] = refs(field(scene["900000004"]["text"], "clock"))[0][0]
report["visualClockFileID"] = refs(field(visual, "clock"))[0][0]
report["managerEnabled"] = {key: field(scene[key]["text"], "m_Enabled")
                            for key in ["403068533", "403068534", "2147441290", "900000001", "900000004"]}

seen, pending, material_refs = set(), [SCENE], set()
while pending:
    path = pending.pop()
    if path in seen:
        continue
    seen.add(path)
    docs = documents(path)
    for fid, document in docs.items():
        text = document["text"]
        terrain_mat = field(text, "m_MaterialTemplate")
        if terrain_mat:
            report["terrain"].append({"source": path, "fileID": fid,
                "hierarchy": hierarchy(docs, fid), "material": asset_ref(terrain_mat)})
            material_refs.update(refs(terrain_mat))
        for match in re.finditer(r"^  m_Materials:\n((?:  - .*\n)+)", text, re.M):
            material_refs.update(refs(match[1]))
        if document["class"] == "20":
            report["cameras"].append({"source": path, "hierarchy": hierarchy(docs, fid),
                "clearFlags": field(text, "m_ClearFlags"), "enabled": field(text, "m_Enabled")})
        source = asset_ref(field(text, "m_SourcePrefab"))
        if source and str(source["asset"]).endswith(".prefab"):
            pending.append(source["asset"])

for fid, guid in sorted(material_refs):
    path = assets.get(guid)
    item = {"fileID": fid, "asset": path or guid, "shader": None}
    if path and path.endswith(".mat"):
        shader = asset_ref(field(read(path), "m_Shader"))
        item["shader"] = shader
        sp = shader["asset"] if shader else ""
        item["category"] = "snow-supported" if sp and ("Shaders/Weather/" in sp or sp == "Assets/FlatShadedShader.shadergraph") else "other shader (inspect/migrate selectively)"
    else:
        item["category"] = "embedded/model material" if path else "built-in/package/unresolved"
    report["materials"].append(item)
report["materialCounts"] = dict(Counter(m["category"] for m in report["materials"]))
report["assetDefinitionsVisited"] = sorted(seen)
light_id = refs(field(visual, "lightningLight"))[0][0]
assert light_id not in ("0", refs(field(clock, "sunLight"))[0][0], refs(field(clock, "moonLight"))[0][0])
assert scene[light_id]["class"] == "108"
report["lightning"] = {"fileID": light_id, "hierarchy": hierarchy(scene, light_id),
    "intensity": field(scene[light_id]["text"], "m_Intensity"), "type": field(scene[light_id]["text"], "m_Type")}
assert report["lightning"]["intensity"] == "0"
assert all("Player/" not in p["hierarchy"] and p["moveWithTransform"] == "0" for p in report["particles"])
assert field(scene["900100020"]["text"], "gameplayCamera") == "{fileID: 330585546}"
report["follower"] = {"hierarchy": hierarchy(scene, "900100020"), "camera": hierarchy(scene, "330585546")}
assert field(visual, "maxRainEmission") == field(visual, "maxSnowEmission") == "100"
report["findings"] = [
    "Dedicated scene-owned lightning light is distinct from Sun/Moon, starts at zero intensity.",
    "Scene-owned rain/snow world-space emitters are outside Player hierarchy; follower references Main Camera output transform.",
    "Rain/snow emissions remain authored at 100 maximum in SampleScene.",
    "windZone, windAudio and precipitationAnchor remain optional/unassigned.",
    "Material inventory counts unique serialized references, not instantiated object totals; embedded internals/effective overrides need Unity inspection."
]

out = ROOT / "Docs/SampleSceneWeatherAudit.json"
out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({k: v for k, v in report.items() if k not in ["materials", "assetDefinitionsVisited"]}, ensure_ascii=False, indent=2))
