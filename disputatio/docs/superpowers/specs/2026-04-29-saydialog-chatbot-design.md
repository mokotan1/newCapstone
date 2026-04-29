# SayDialogChatbot 디자인 스펙 (D-2 앙부일구)

## 채택 디자인: D-2 앙부일구

카타나 제로 스타일(화면 상단 고정) + 게임 세피아/황금 분위기 결합.

## 비주얼 스펙

| 요소 | 값 |
|------|----|
| 위치 | 화면 상단 고정 (anchor top-center) |
| 크기 | 1500 × 220 px (reference 1600×1200) |
| 배경 | #060402 near-black, alpha 0.98 |
| 좌/우 테두리 | 2px, #c8a84b gold, alpha 0.4 |
| 코너 ✦ | #c8a84b gold, alpha 0.6, 24px |
| 이름 텍스트 | #c8a84b gold, 38pt, 중앙 정렬, "✦  Chester  ✦" |
| 이름 구분선 | 1px, #c8a84b, alpha 0.15, 너비 92% |
| 대사 텍스트 | #d8ccb0 cream, 40pt, 중앙 정렬 |
| 하단 힌트 | "— SPACE TO CONTINUE —", #c8a84b, alpha 0.2 |

## 프리팹 계층 (fileID 40001–40093)

```
SayDialogChatbot (40001)
├── Canvas: Screen Space Overlay, sortingOrder 10
├── SayDialog (40007): nameText→40063, storyText→40073, continueButton→40084
├── Writer (40008): targetTextObject→40070, punchObject→40020
├── DialogInput (40010): clickMode 1
└── Panel (40020) — anchor top-center, 1500×220
    ├── LeftBorder (40030)  — 2px gold, full height
    ├── RightBorder (40034) — 2px gold, full height
    ├── CornerLeft (40038)  — "✦" top-left
    ├── CornerRight (40042) — "✦" top-right
    ├── NameText (40060)    — anchor top, h:52, y:-32 [SayDialog.nameText]
    ├── NameDivider (40064) — 1px line, y:-62
    ├── StoryText (40070)   — stretch, cream [SayDialog.storyText / Writer.targetTextObject]
    └── Continue (40080)    — anchor bottom, h:30, invisible button [SayDialog.continueButton]
        └── FooterText (40090) — "— SPACE TO CONTINUE —"
```

## 씬 적용 방법

각 챗봇 MonoBehaviour 인스펙터에서 `Chat Say Dialog` 필드에 `SayDialogChatbot.prefab` 할당:
- GlobalChatbot (CentralHall)
- TutorChatbot (TutorRoom)
- KitchenChatbot (Kitchen)
- WifeRoomChatbot, SonRoomChatbot, MainBedroomChatbot (Parret_Panel)

## Unity Editor 조정 권장 사항

- NameText 기본값 "✦  Chester  ✦" → Fungus Say 커맨드가 캐릭터 이름으로 덮어씀
- 폰트 크기/위치는 실제 화면에서 확인 후 인스펙터에서 조정
- SayDialogNotebook(일반 내러티브용)과 구분하여 챗봇 전용으로만 사용
