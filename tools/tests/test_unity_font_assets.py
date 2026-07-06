from pathlib import Path
import re


REPO_ROOT = Path(__file__).resolve().parents[2]
FONT_ASSET = REPO_ROOT / "disputatio/Assets/Font/NanumGothic SDF.asset"
FONT_META = FONT_ASSET.with_suffix(FONT_ASSET.suffix + ".meta")
TMP_SETTINGS = (
    REPO_ROOT
    / "disputatio/Assets/Fungus/Thirdparty/TextMeshPro/Resources/TMP Settings.asset"
)


def _read_guid(meta_path: Path) -> str:
    match = re.search(r"^guid:\s*([0-9a-f]{32})$", meta_path.read_text(), re.MULTILINE)
    assert match, f"Unity meta file has no guid: {meta_path}"
    return match.group(1)


def test_nanum_gothic_default_tmp_font_asset_is_present_and_guid_stable():
    assert FONT_ASSET.exists(), "NanumGothic SDF.asset is missing; TMP references will become unmapped"
    assert FONT_META.exists(), "NanumGothic SDF.asset.meta is missing; Unity cannot keep TMP references stable"

    font_guid = _read_guid(FONT_META)
    settings_text = TMP_SETTINGS.read_text()

    assert (
        f"m_defaultFontAsset: {{fileID: 11400000, guid: {font_guid}, type: 2}}"
        in settings_text
    )
