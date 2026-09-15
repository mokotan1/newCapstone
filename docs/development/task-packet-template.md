# 작업 패킷 (짧게)

위임한 작업만 `docs/development/tasks/<feature-id>/<task-id>.md`에 복사한다.
부모 단독 구현은 index 한 장으로 충분하다. 빈 칸을 채우기 위해 위임하지 않는다.

```markdown
# <task-id>

- Feature:
- 한 줄 목표:
- AC1 (관찰 가능):
- AC2 (실패/경계):
- 제외:
- 허용 파일 (소유 + 테스트 쌍만):
- 공유 자원 (부모/Editor, 이 Task가 안 고침):
- 선행:
- 검증 명령:
- Cursor 도구: 부모 | explore | generalPurpose | code-reviewer | qa-*
- model 인자: inherit | <slug> | (생략=상속)
- 선택 이유 / provisional 여부:
- checkout / base revision:
- 위험도: R0 | R1 | R2 | R3
- Editor 소유권:
- 증거 위치:
- 검증 상태 / verificationStatus: passed | failed | blocked | not-applicable | waived
- 상태: planned | running | review | verified | blocked

## 결과

- 실제 변경 파일:
- 명령 / exit code / 요약:
- 실행 못 한 검사:
- independentReview: true | false
- 리뷰 세션 / 판정:
- 다음 단계:
```

`verified`는 검증 명령 통과와 별도 세션 리뷰가 있을 때만 쓴다.
