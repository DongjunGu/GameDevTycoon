# 퀘스트 시스템 메뉴얼

## 1. 구조 개요

| 구성요소 | 역할 |
|---|---|
| 뒤끝 Chart `Quest` | 퀘스트 마스터 데이터 (읽기 전용) |
| 뒤끝 GameData `UserQuest` | 유저별 진행 상태 저장 |
| `QuestData.cs` | 퀘스트 데이터 클래스 |
| `QuestManager.cs` | 로드, 진행, 완료, 저장 로직 |
| `QuestUI.cs` | 퀘스트 목록 UI |

---

## 2. 뒤끝 차트 컬럼 설명

| 컬럼 | 타입 | 설명 |
|---|---|---|
| questId | string | 고유 ID (예: quest_001, main_quest_001) |
| title | string | 퀘스트 제목 |
| description | string | 퀘스트 설명 |
| type | string | 퀘스트 종류 (아래 QuestType 참고) |
| targetValue | int | 목표 수치 |
| rewardGold | int | 보상 골드 (메인퀘스트는 0) |
| isMainQuest | int | 메인퀘스트 여부 (0 = 일반, 1 = 메인) |
| unlockAfter | string | 이 questId 완료 시 공개 (빈 값 = 처음부터 공개) |

---

## 3. QuestType 종류

| QuestType | 진행 트리거 | UpdateProgress 호출 위치 |
|---|---|---|
| TotalSales | 게임 판매량 누적 | `SalesUI` — 판매 완료 시 |
| HireEmployee | 직원 채용 누적 | `EmployeeManager.HireEmployee` |
| SurviveYears | 연도 경과 누적 | `GameTimeManager.AdvanceWeek` — 연도 변경 시 |
| TotalRevenue | 매출 누적 | `SalesUI` — 주차별 수익 지급 시 |

---

## 4. 일반 퀘스트

### 특징
- 기본 `isVisible = false` (숨김 상태로 시작)
- 코드에서 `QuestManager.Instance.UnlockQuest("quest_001")` 호출 시 공개
- 완료 시 Alert → 퀘스트 UI 오픈 → 클릭으로 보상 수령

### 공개 흐름
```
UnlockQuest("quest_001") 호출
    → isVisible = true
    → UserQuest 저장
    → QuestUI 갱신
```

### 완료 흐름
```
UpdateProgress(QuestType.TotalSales, 500) 호출
    → isVisible인 퀘스트만 진행
    → currentValue += 500
    → targetValue 도달 시 isCompleted = true
    → Alert "퀘스트 완료! 보상: 500G"
    → 확인 클릭 → QuestUI 오픈
    → 퀘스트 클릭 → 보상 수령 (ClaimReward)
```

### 새 퀘스트 추가 방법
1. 차트에 행 추가 (`isMainQuest = 0`, `unlockAfter` 빈 값)
2. 적절한 시점에 코드에서 `UnlockQuest("questId")` 호출
3. 해당 `QuestType`의 `UpdateProgress`가 이미 호출되고 있으면 별도 코드 불필요

---

## 5. 메인 퀘스트

### 특징
- `isMainQuest = 1`
- `unlockAfter`가 빈 값이면 게임 시작부터 공개
- `unlockAfter`에 questId가 있으면 해당 퀘스트 완료 시 자동 공개
- 보상 없음 (`rewardGold = 0`, 자동으로 `isRewarded = true` 처리)
- **루트 메인퀘스트** (`unlockAfter` 빈 값) 전부 완료 시 "메인퀘스트 완료!" Alert

### unlockAfter 체인 예시
```
main_quest_001 (unlockAfter: 빈값) → 처음부터 공개
main_quest_002 (unlockAfter: main_quest_001) → 001 완료 시 공개

main_quest_003 (unlockAfter: 빈값) → 처음부터 공개
main_quest_004 (unlockAfter: main_quest_003) → 003 완료 시 공개
```

### 완료 흐름
```
UpdateProgress 호출 → targetValue 도달
    → isCompleted = true, isRewarded = true
    → UnlockChainedMainQuests 호출 (체인 퀘스트 공개)
    → 루트 메인퀘스트 전부 완료 여부 확인
        → 전부 완료: Alert "메인퀘스트 완료!"
        → 미완료: Alert 없음
```

### 새 메인퀘스트 추가 방법
1. 차트에 행 추가 (`isMainQuest = 1`)
2. 처음부터 공개: `unlockAfter` 빈 값
3. 체인 공개: `unlockAfter`에 선행 questId 입력
4. 별도 코드 수정 불필요 (QuestType이 이미 있는 경우)

---

## 6. QuestUI 표시 규칙

| 상태 | 배경색 | 비고 |
|---|---|---|
| 진행중 | 흰색 (투명) | 기본 |
| 완료 (수령 전) | 초록 | 클릭 시 보상 수령 |
| 수령 완료 | 어둡게 | 비활성 |

- 메인퀘스트: 제목 앞에 `[메인]` 접두사, 항상 상단 고정
- 정렬 순서: 메인퀘스트 → 일반퀘스트 / 각 그룹 내 진행중 → 완료 → 수령완료

---

## 7. 현재 차트 데이터

```csv
questId,title,description,type,targetValue,rewardGold,isMainQuest,unlockAfter
quest_001,첫 판매,게임을 총 1000개 판매하세요,TotalSales,1000,500,0,
quest_002,히트작,게임을 총 10000개 판매하세요,TotalSales,10000,2000,0,
quest_003,팀 꾸리기,직원을 2명 채용하세요,HireEmployee,2,300,0,
quest_004,든든한 팀,직원을 5명 채용하세요,HireEmployee,5,1000,0,
main_quest_001,1년 버티기,1년 이상 회사를 운영하세요,SurviveYears,1,0,1,
main_quest_002,2년 버티기,2년 이상 회사를 운영하세요,SurviveYears,2,0,1,main_quest_001
main_quest_003,매출 100000G,누적 매출 100000G를 달성하세요,TotalRevenue,100000,0,1,
main_quest_004,매출 200000G,누적 매출 200000G를 달성하세요,TotalRevenue,200000,0,1,main_quest_003
```
