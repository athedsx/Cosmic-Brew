"""
Subsets the game fonts so they only contain the characters used by the translations.

  python Tools/fonts/build_fonts.py

Input:  Tools/fonts/src/*.ttf (Google Fonts, SIL OFL) + Assets/Resources/Localization/strings.txt
Output: Assets/Resources/Fonts/*.ttf (a few KB to MB each)
Run it again whenever you add or change text in strings.txt.
"""
import io, os, shutil
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer
from fontTools import subset

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Tools", "fonts", "src")
OUT = os.path.join(ROOT, "Assets", "Resources", "Fonts")
STRINGS = os.path.join(ROOT, "Assets", "Resources", "Localization", "strings.txt")

LANGS = ["pt", "en", "ru", "ko", "zh", "ja"]
NATIVE = {"pt": "Português", "en": "English", "ru": "Русский", "ko": "한국어", "zh": "中文（简体）", "ja": "日本語"}

# characters always included: ASCII, Latin-1, typographic punctuation, UI symbols
BASE = "".join(chr(c) for c in range(0x20, 0x7F)) + "".join(chr(c) for c in range(0xA0, 0x100)) \
       + "–—‘’“”•…·★☆♪♫«»№←→↑↓✓×（）：，。！？～〜、「」・％＋－"


def read_columns():
    cols = {l: set() for l in LANGS}
    with io.open(STRINGS, encoding="utf-8") as f:
        header = f.readline().rstrip("\n").split("\t")
        idx = {l: header.index(l) for l in LANGS}
        for line in f:
            if not line.strip() or line.startswith("#"):
                continue
            parts = line.rstrip("\n").split("\t")
            for l in LANGS:
                if idx[l] < len(parts):
                    cols[l].update(parts[idx[l]])
    return cols


def build(src_name, out_name, weight, chars):
    path = os.path.join(SRC, src_name)
    font = TTFont(path)
    if "fvar" in font:
        font = instancer.instantiateVariableFont(font, {"wght": weight}, updateFontNames=False)
    opts = subset.Options()
    opts.layout_features = ["kern", "liga", "calt", "ccmp", "locl", "mark", "mkmk", "vert", "vrt2"]
    opts.name_IDs = ["*"]
    opts.name_languages = ["*"]
    opts.notdef_outline = True
    opts.hinting = False
    opts.drop_tables += ["DSIG"]
    sub = subset.Subsetter(opts)
    sub.populate(unicodes=sorted({ord(c) for c in chars}))
    sub.subset(font)
    os.makedirs(OUT, exist_ok=True)
    dst = os.path.join(OUT, out_name)
    font.save(dst)
    cmap = font.getBestCmap()
    missing = sorted({c for c in chars if ord(c) not in cmap and c.strip()})
    print(f"  {out_name:28s} {os.path.getsize(dst) / 1024:7.1f} KB  glyphs={len(cmap):5d}  missing: {''.join(missing)[:60]}")


def main():
    cols = read_columns()
    names = "".join(NATIVE.values())
    latin = BASE + "".join(cols["pt"] | cols["en"] | cols["ru"]) + "".join(chr(c) for c in range(0x400, 0x460))
    print("Fonts:")
    build("Nunito[wght].ttf", "Nunito-SemiBold.ttf", 650, latin + names)
    build("NotoSansKR[wght].ttf", "NotoSansKR-Medium.ttf", 550, BASE + "".join(cols["ko"]) + NATIVE["ko"])
    build("NotoSansSC[wght].ttf", "NotoSansSC-Medium.ttf", 550, BASE + "".join(cols["zh"]) + NATIVE["zh"])
    # the Japanese font also serves as a fallback for symbols (♪) that Nunito lacks
    build("NotoSansJP[wght].ttf", "NotoSansJP-Medium.ttf", 550, BASE + "".join(cols["ja"]) + NATIVE["ja"])
    lic = os.path.join(OUT, "LICENSE-OFL.txt")
    shutil.copy(os.path.join(SRC, "OFL.txt"), lic)
    with io.open(lic, "a", encoding="utf-8") as f:
        f.write("\n\nFontes incluídas (cortadas): Nunito (The Nunito Project Authors), "
                "Noto Sans KR / SC / JP (Google LLC / Adobe). Todas sob SIL Open Font License 1.1.\n")


if __name__ == "__main__":
    main()
