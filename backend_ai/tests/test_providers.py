from __future__ import annotations

from providers.litert_provider import LiteRTProvider


def test_litert_provider_name_and_defaults() -> None:
    provider = LiteRTProvider(
        base_url="http://127.0.0.1:9379/",
        model="gemma4-e2b",
        num_ctx=4096,
        think=False,
    )
    assert provider.name == "litert"
    assert provider._base_url == "http://127.0.0.1:9379"
    assert provider._model == "gemma4-e2b"
    assert provider._num_ctx == 4096
    assert provider._think is False
