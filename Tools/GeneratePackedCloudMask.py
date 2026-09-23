"""Build the linear RGBA sky mask from art-directed source images.

R fair/cumulus, G overcast/stratus, B sparse cirrus, A broad variation.
Offline authoring only; the game never generates cloud textures at runtime.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageEnhance, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parents[1]
SOURCES = ROOT / "Tools/CloudMaskSources"
OUTPUT = ROOT / "Assets/Art/Sky/Clouds/CloudMasksPacked.png"


def authored_channel(name: str, black: int, white: int) -> Image.Image:
    source = Image.open(SOURCES / name).convert("L")
    # Source generators may return 1254px. Work from a square crop, then downsample.
    side = min(source.size)
    source = source.crop(((source.width-side)//2, (source.height-side)//2,
                          (source.width+side)//2, (source.height+side)//2))
    source = source.resize((1024, 1024), Image.Resampling.LANCZOS)
    values = np.asarray(source, dtype=np.float32)
    values = np.clip((values-black) * (255.0/max(1, white-black)), 0, 255).astype(np.uint8)
    # Shift the original border to the middle, where a narrow local repair can hide it.
    # Unlike mirrored quadrants this preserves the authored asymmetric composition.
    shifted = np.roll(values, (512, 512), axis=(0, 1))
    blurred = np.asarray(Image.fromarray(shifted, "L").filter(ImageFilter.GaussianBlur(18)), dtype=np.float32)
    coordinates = np.arange(1024, dtype=np.float32)
    distance_x = np.abs(coordinates - 512)
    distance_y = np.abs(coordinates - 512)
    seam_x = np.clip(1-distance_x/52, 0, 1)[None, :]
    seam_y = np.clip(1-distance_y/52, 0, 1)[:, None]
    seam = np.maximum(seam_x, seam_y)
    seam = seam * seam * (3-2*seam)
    periodic = shifted.astype(np.float32) * (1-seam) + blurred * seam
    periodic = np.clip(periodic, 0, 255).astype(np.uint8)
    # Exact equal boundaries avoid bilinear/mipmap seams at Repeat wrap.
    edge = ((periodic[:, 0].astype(np.uint16)+periodic[:, -1].astype(np.uint16))//2).astype(np.uint8)
    periodic[:, 0] = edge; periodic[:, -1] = edge
    edge = ((periodic[0].astype(np.uint16)+periodic[-1].astype(np.uint16))//2).astype(np.uint8)
    periodic[0] = edge; periodic[-1] = edge
    return Image.fromarray(periodic, "L")


fair = authored_channel("fair-cumulus-generated.png", 12, 215)
overcast = authored_channel("overcast-stratus-generated.png", 24, 210)
cirrus = authored_channel("sparse-cirrus-generated.png", 10, 170)

# Remove generator speckles and retain intentional large forms.
fair = fair.filter(ImageFilter.MedianFilter(5))
overcast = overcast.filter(ImageFilter.GaussianBlur(2.0))
cirrus = cirrus.filter(ImageFilter.GaussianBlur(1.2))

fair_values = np.asarray(fair, dtype=np.uint8)
overcast_values = np.asarray(overcast, dtype=np.uint8)
cirrus_values = np.asarray(cirrus, dtype=np.uint8)
broad = Image.fromarray(np.maximum(fair_values, (overcast_values * .65).astype(np.uint8)), "L")
broad = ImageEnhance.Contrast(broad.filter(ImageFilter.GaussianBlur(26))).enhance(.7)

rgba = Image.merge("RGBA", (fair, overcast, cirrus, broad))
# Filters above are deliberately non-wrapping authoring operations. Reconcile the
# final four-channel boundary once, after all of them, so every mip starts periodic.
final_values = np.asarray(rgba, dtype=np.uint8).copy()
edge = ((final_values[:, 0].astype(np.uint16)+final_values[:, -1].astype(np.uint16))//2).astype(np.uint8)
final_values[:, 0] = edge; final_values[:, -1] = edge
edge = ((final_values[0].astype(np.uint16)+final_values[-1].astype(np.uint16))//2).astype(np.uint8)
final_values[0] = edge; final_values[-1] = edge
rgba = Image.fromarray(final_values, "RGBA")
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
rgba.save(OUTPUT, optimize=True)

array = np.asarray(rgba)
edge_error = max(
    int(np.abs(array[:, 0].astype(int)-array[:, -1].astype(int)).max()),
    int(np.abs(array[0].astype(int)-array[-1].astype(int)).max()))
stats = {
    "size": list(rgba.size), "edgeMaxDifference": edge_error,
    "channelMean": [round(float(array[..., i].mean()), 3) for i in range(4)],
    "channelCoverageAbove128": [round(float((array[..., i] > 128).mean()), 4) for i in range(4)],
    "packing": {"R": "fair/cumulus", "G": "overcast/stratus",
                "B": "sparse cirrus", "A": "broad lighting variation"}
}
(OUTPUT.with_suffix(".stats.json")).write_text(json.dumps(stats, indent=2)+"\n", encoding="utf-8")
if edge_error != 0:
    raise RuntimeError("Packed cloud texture is not seamless: " + str(edge_error))
print(json.dumps(stats, indent=2))
