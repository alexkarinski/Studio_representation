#!/usr/bin/env python3
"""
Скачивает 3D-модели с Poly Haven (CC0) и готовит текстуры для Unity (шейдер Standard).

  python3 tools/prepare_models.py                  # все модели из списка MODELS
  python3 tools/prepare_models.py sofa_03 ...      # добавить другие (id с polyhaven.com/models)

Результат: Assets/Studio/Resources/Models/<id>/<id>.fbx и Textures/<set>_albedo|_normal|_ms.
_ms: R = metallic, A = 1 - roughness (как ждёт Standard). Альфа листьев склеивается с альбедо (png).
Чтобы модель появилась в каталоге, добавьте строку Model(...) в PropFactory.Catalog.
Требуется Pillow: pip install pillow
"""
import concurrent.futures as cf, glob, json, os, re, shutil, sys, tempfile, urllib.request
from PIL import Image, ImageOps

MODELS = ["ArmChair_01", "modern_arm_chair_01", "mid_century_lounge_chair", "Sofa_01", "sofa_02", "Ottoman_01",
          "coffee_table_round_01", "side_table_01", "side_table_tall_01", "modern_coffee_table_01",
          "bar_chair_round_01", "metal_stool_02", "steel_frame_shelves_03", "wooden_display_shelves_01",
          "potted_plant_01", "potted_plant_02", "potted_plant_04", "modern_wooden_cabinet", "ceramic_vase_01",
          "throw_pillows_01", "standing_picture_frame_01"]
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = os.path.join(ROOT, "Assets/Studio/Resources/Models")

opener = urllib.request.build_opener()
opener.addheaders = [("User-Agent", "studio-representation/1.0")]
urllib.request.install_opener(opener)


def download(ids, tmp):
    jobs = []
    for mid in ids:
        files = json.load(urllib.request.urlopen(f"https://api.polyhaven.com/files/{mid}"))
        fbx = files["fbx"]["1k"]["fbx"]
        jobs.append((fbx["url"], f"{tmp}/{mid}/{mid}.fbx"))
        for key, v in fbx["include"].items():
            name, url = key.split("/")[-1], v["url"]
            if name.endswith(".exr"):  # Unity/Pillow удобнее jpg-версия той же карты
                name, url = name[:-4] + ".jpg", url.replace("/exr/", "/jpg/")[:-4] + ".jpg"
            jobs.append((url, f"{tmp}/{mid}/textures/{name}"))

    def get(job):
        url, path = job
        os.makedirs(os.path.dirname(path), exist_ok=True)
        urllib.request.urlretrieve(url, path)

    with cf.ThreadPoolExecutor(16) as ex:
        list(ex.map(get, jobs))


def convert(tmp, mid):
    od = f"{OUT}/{mid}"
    os.makedirs(od + "/Textures", exist_ok=True)
    shutil.copy(f"{tmp}/{mid}/{mid}.fbx", f"{od}/{mid}.fbx")
    sets = {}
    for t in glob.glob(f"{tmp}/{mid}/textures/*"):
        m = re.match(r"(.+?)_(diff|nor_gl|rough|roughness|metal|metallic|alpha)_1k\.\w+$", os.path.basename(t))
        if m:
            sets.setdefault(m.group(1).lower(), {})[m.group(2)] = t
    for s, maps in sets.items():
        diff = Image.open(maps["diff"]).convert("RGB")
        size = diff.size
        if "alpha" in maps:
            diff.putalpha(Image.open(maps["alpha"]).convert("L").resize(size))
            diff.save(f"{od}/Textures/{s}_albedo.png", optimize=True)
        else:
            diff.save(f"{od}/Textures/{s}_albedo.jpg", quality=88)
        if "nor_gl" in maps:
            Image.open(maps["nor_gl"]).convert("RGB").save(f"{od}/Textures/{s}_normal.jpg", quality=92)
        r = maps.get("rough") or maps.get("roughness")
        mt = maps.get("metal") or maps.get("metallic")
        if r or mt:
            rough = Image.open(r).convert("L").resize(size) if r else Image.new("L", size, 180)
            metal = Image.open(mt).convert("L").resize(size) if mt else Image.new("L", size, 0)
            Image.merge("RGBA", (metal, metal, metal, ImageOps.invert(rough))).save(f"{od}/Textures/{s}_ms.png", optimize=True)
    print(mid, sorted(sets))


if __name__ == "__main__":
    ids = sys.argv[1:] or MODELS
    with tempfile.TemporaryDirectory() as tmp:
        download(ids, tmp)
        for mid in ids:
            convert(tmp, mid)
