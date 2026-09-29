from pathlib import Path
import re, codecs
text = Path(r"disputatio/Assets/Scenes/Mokotan/Opening_Mention _open.unity").read_text(encoding="utf-8", errors="replace")
print("controller", "OpeningMentionController" in text)
print("blocks", re.findall(r"blockName: (.+)", text))
print("flowchart", "7a334fe2ffb574b3583ff3b18b4792d3" in text)
for m in re.finditer(r"storyText: \"([^\"]*)\"", text):
    raw = m.group(1)
    try: s = codecs.decode(raw, "unicode_escape")
    except Exception: s = raw
    print("SAY", repr(s)[:160])
for m in re.finditer(r"stringVal: (.+)", text):
    v = m.group(1).strip()
    if any(x in v for x in ("Hall", "Opening", "Mention", "animate")):
        print("str", v)
for m in re.finditer(r"sfxIndex: (\d+)", text):
    print("sfx", m.group(1))
for m in re.finditer(r"duration: ([0-9.]+)\n  targetAlpha: ([0-9.]+)", text):
    print("fade", m.group(1), "->", m.group(2))
