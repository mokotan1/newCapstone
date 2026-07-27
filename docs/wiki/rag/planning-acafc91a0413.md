---
source_id: planning:acafc91a0413
source_path: 기획서/현재 플로우 직관성 + 맵 제작 + 복도 기믹 추가.pptx
source_sha256: acafc91a041341b628d13634524da0ad46c5091f3a9426aec96fec1274e425d9
source_type: pptx
category: planning
title: 현재 플로우 직관성 + 맵 제작 + 복도 기믹 추가
status: needs_review
rag_eligible: true
---

## Slide 1

현재 플로우 직관성 + 맵 제작

## Slide 2

플로우 직관성

화면의 전체 VIGNNET 값을 조금 높여서 화면 밝기 올려야 함
->공모전 당시 화면이 너무 어두워서 플레이어가 길을 찾지 못하는 상황 발생

유니티 안에 에셋 넣어뒀음
-> 클릭으로 진행하다 보니 TAB을 누르지 않아 인벤토리가 있다는 것을 인지하지 못함 그래서 넣어둔 에셋 중 TAB을 가운데 아래에 넣어(클릭 처리 -> 클릭 했을 경우 인벤토리 올라오도록-> 클릭 한 번 더 했을 경우 인벤토리 내려오게) 인식할 수 있도록

## Slide 3

플로우 직관성

TAB 표시의 경우 인벤토리가 올라왔을 때 같이 올라오고 같이 내려가도록 만들어야 함 그래픽은 솔리드 x 아웃라인 o


다용도 실로 가는 문에 화살표 필요 문을 찾지 못하는 사람 많았음

## Slide 4

맵 구현 방식

> Visual asset present: slide 4, shape 3; inspect original PPTX/PDF.

-화살표를 누르면 1층 맵과 2층 맵을 바꿔서 출력

-아직 가지 않았거나 문을 열지 않았다면 잠금 장치를 건 것 처럼 만들기 + 클릭 처리 OFF

-그 방에서 찾지 못한 물건이 있거나 상호작용 하지 않은 물건이 있다면 아이템에 붙여둔 것처럼 별모양 붙여두기

-한 번 간 방은 맵에서 클릭 했을 경우 바로 갈 수 있도록 한다.

-기본적으로 맵은 키보드 M을 눌렀을 경우 켜지도록

## Slide 5

맵 구현 방식

> Visual asset present: slide 5, shape 3; inspect original PPTX/PDF.

-에셋이 만들어지기 전 까지는 빈 박스 안에 텍스트 박스 넣어서 방 이름 넣어야 함

## Slide 6

맵아트 레퍼런스

-1번째 가독성을 무조건 우선시 할 것

-왼쪽 처럼 말풍선 안에 방 이름을 써둬야 함

<- 레퍼런스 1

> Visual asset present: slide 6, shape 4; inspect original PPTX/PDF.

## Slide 7

맵아트 레퍼런스

-1번째 가독성을 무조건 우선시 할 것

-왼쪽 처럼 말풍선 안에 방 이름을 써둬야 함

<- 레퍼런스 2

-> 레퍼런스 1과 2를 섞은 방식으로

> Visual asset present: slide 7, shape 4; inspect original PPTX/PDF.

## Slide 8

맵아트 레퍼런스

-가지 않은 방이 잠겨있다고 보여주는 자물쇠 레퍼런스

> Visual asset present: slide 8, shape 4; inspect original PPTX/PDF.

> Visual asset present: slide 8, shape 5; inspect original PPTX/PDF.

방의 문을 열지 않았을 때의 모습 예시

## Slide 9

맵아트 레퍼런스

-가지 않은 방이 잠겨있다고 보여주는 자물쇠 레퍼런스 (위는 예시)

> Visual asset present: slide 9, shape 4; inspect original PPTX/PDF.

주방

> Visual asset present: slide 9, shape 6; inspect original PPTX/PDF.

서재

> Visual asset present: slide 9, shape 8; inspect original PPTX/PDF.

가정부 방

## Slide 10

복도 기믹 추가(적을 추가하여 긴장감, 공포 분위기 추가)

매인 홀에 들어갔을 때

> Visual asset present: slide 10, shape 3; inspect original PPTX/PDF.

-붉은 네모 칸이 적의 위치

-적 오브젝트에 캔버스를 씌워서 적 오브젝트 안에 패널을 넣음
[오브젝트 트리 구조]
–ENEMY <- CAPSULE COLLISION 2D(있어야 함)
   -PANEL
     -RAWIMAGE

클릭 했을 때 플레이어의 모든 행동을 일시 정지-마우스 삭제
-클릭x
-ESC금지

## Slide 11

복도 기믹 추가(적을 추가하여 긴장감, 공포 분위기 추가)

매인 홀에 들어갔을 때

> Visual asset present: slide 11, shape 3; inspect original PPTX/PDF.

-앵무새는 적과 같이 있으면 안됨
-> 처음 hall_playable 씬에 들어가면 앵무새가 없어야 함.

-> 적을 한 번 클릭 후 다시 hall_playable씬이 로드 되면 그때 적이 사라지고 앵무새가 나타나도록 해야함.

=> 이것은 앵무새가 적이 아닌 아군이라는 인식을 주면서 적이 위험하다라는 인식을 주기 위함.

## Slide 12

복도 기믹 추가(적을 추가하여 긴장감, 공포 분위기 추가)

적이 클릭 되었거나 같은 장소에서 1분이 지나 플레이어가 죽었을 경우

눈 깜박이는 효과 (0.2초)(mokotan->Prifab->BlinkManager 사용(지금은 스페이스파를 눌렀을 때 깜박이도록 코딩되어 있음 수정해서 사용 ))
    -> 애니메이션 실행
        ->사망

Scene->Mokotan->CreateEffect->shader->BlinkShader 사용

플로우
클릭 or 1분 지남-> 플레이어의 모든 행동 정지-> 눈깜박임-> 애니메이션 실행 -> 사망 페이지
