# GameDevTycoon 스크립트 문서

> 게임 개발 회사 경영 시뮬레이션 (Unity 2D Isometric / 뒤끝 BaaS)

---

## 목차
1. [백엔드/서버](#1-백엔드서버)
2. [시간 & 경제](#2-시간--경제)
3. [프로젝트 개발](#3-프로젝트-개발)
4. [직원 관리](#4-직원-관리)
5. [마케팅 & 판매](#5-마케팅--판매)
6. [퀘스트](#6-퀘스트)
7. [랜덤 이벤트](#7-랜덤-이벤트)
8. [테크트리](#8-테크트리)
9. [다이얼로그](#9-다이얼로그)
10. [공통 UI](#10-공통-ui)
11. [캐릭터 & 오피스](#11-캐릭터--오피스)
12. [길찾기 & 그리드](#12-길찾기--그리드)
13. [씬 초기화](#13-씬-초기화)
14. [데이터 모델](#14-데이터-모델)

---

## 1. 백엔드/서버

### `BackendManager.cs`
뒤끝 SDK 전체 초기화 및 데이터 로드 오케스트레이터.
- `Start()` — SDK 초기화 → 플랫폼별 로그인 분기
  - `#if UNITY_EDITOR` → TestLogin()
  - `#elif UNITY_ANDROID` → GPGSLogin.StartLogin()
  - `#elif UNITY_IOS` → LoginButtonPanel 버튼으로 처리 (자동 로그인 없음)
- `LoadAllAndEnterGame()` — 직원/재화/시간/퀘스트/프로젝트/대출/테크트리 순차 로드 후 GameScene 전환
- `OnLoginSuccess()` — 로그인 완료 후 호출되는 공통 콜백
- DontDestroyOnLoad

### `BackendLogin.cs`
뒤끝 커스텀 로그인 처리.
- `CustomLogin(id, pw)` — 계정 로그인
- `CustomSignUp(id, pw)` — 계정 생성
- 로그인 성공 시 `BackendManager.OnLoginSuccess()` 콜백

### `GPGSLogin.cs`
Google Play Games Services 인증 후 뒤끝 Federation 로그인 연결.
- `#if UNITY_ANDROID` 전체 래핑 (iOS 빌드 시 컴파일 제외)
- `Login()` — GPGS 인증 → 뒤끝 연동
- DontDestroyOnLoad

### `GameCenterLogin.cs`
Sign in with Apple 구현 (파일명은 GameCenter이나 실제로는 Apple 로그인).
- Awake: `DontDestroyOnLoad`, `gameObject.name = "AppleLogin"` (네이티브 콜백 수신 이름)
- `StartLogin()` — `_RequestAppleSignIn()` 네이티브 호출 (`#if UNITY_IOS`)
- `OnTokenReceived(identityToken)` — `Backend.BMember.AuthorizeFederation(token, FederationType.Apple)`
- `OnTokenFailed(error)` — 에러 로그

### `LoginButtonPanel.cs`
iOS 전용 로그인 버튼 패널.
- Awake: `#if UNITY_IOS`만 활성화 (Android는 `SetActive(false)`)
- `appleLoginButton` — Apple 로그인 버튼, 클릭 시 `GameCenterLogin.StartLogin()`
- 추후 Google 버튼 자리 확보 (주석으로 준비)

### `BackendGameData.cs`
뒤끝 GameData 테이블 CRUD 래퍼.
- `Insert / Update / Get` — 테이블별 범용 저장/로드

### `BackendGameLog.cs`
게임 이벤트 로그 기록 (분석용).

---

## 2. 시간 & 경제

### `GameTimeManager.cs`
게임 내 시간 흐름 관리.
- `secondsPerWeek = 10f` — 실시간 10초 = 게임 1주
- `StopTime()` / `StartTime()` — 참조 카운터(_stopCount) 방식 정지/재개
- `ForceStartTime()` — 카운터 무시하고 강제 재개
- `IsRunning` — 현재 시간 흐름 여부 (모든 코루틴에서 체크)
- 연도 변경 시 `PayAnnualSalary()` → `SalaryNegotiationManager` 자동 발동
- `OnApplicationPause(paused)` / `OnApplicationQuit()` — 앱 백그라운드/종료 시 자동 저장
- 뒤끝 `UserGameTime` 테이블에 저장
- DontDestroyOnLoad

### `MoneyManager.cs`
금화(G) 재화 관리.
- `AddGold(amount)` — 수입 추가
- `SpendGold(amount, saveImmediately)` — 지출 (잔액 부족 시 false 반환)
- `ForceSpendGold(amount)` — 음수 허용 강제 차감 (파산 체크용)
- `HandleDialogResult(result)` — GoldChange / SatisfactionChange 처리, 각각 SaveGameTime 호출
- `ShowAfterDialog(message)` — OnDialogEnd 1회 구독 → 다이얼로그 종료 후 AlertUI 표시
- 뒤끝 `UserMoney` 테이블에 저장
- DontDestroyOnLoad

### `LoanManager.cs`
대출 시스템.
- 1~5단계 (10,000G ~ 100,000G), 기본 이자율 4%
- 대출 1개 제한
- 만기 시 `ForceSpendGold()` → 음수면 파산 Alert
- 뒤끝 `UserLoans` 테이블에 저장
- DontDestroyOnLoad

---

## 3. 프로젝트 개발

### `DevelopmentManager.cs`
게임 개발 메인 루프 총괄.

**단계**: `None → Developing → BugFixing → Marketing → Complete`

**틱 시스템 (구간 분할 랜덤)**:
- 직원마다 전체 개발 시간을 틱 수(8~10회)로 균등 분할
- 각 구간 안에서 랜덤한 시점에 발동 → 불규칙하게 느껴지면서 밸런스 보장
- tickType 0(주스탯 60%) / 1(창의성 20%) / 2(버그 20%) 비율로 순서 셔플
- 수치 = `CalcConstantDev(skill) × 만족도배율 × 네트워크배율`
- 틱 발생 시 `OfficeManager.ShowStatPopup`으로 캐릭터 머리 위 팝업 표시

**흐름**:
```
StartDevelopment()
→ TriggerInvestmentEvent()
→ LeaderSelectUI(기획팀장, 0%)
→ DevelopmentCoroutine()
  → 25%: LeaderSelectUI(개발팀장) → TryTriggerNetworkIssue()
  → 50%: CheckTrigger() [랜덤이벤트]
  → 75%: LeaderSelectUI(아트팀장)
→ OnDevelopmentComplete() → BugFixCoroutine()
→ ShowResult() → CheckInvestmentResult()
→ DevelopmentResultUI.Show()
```

**점수 계산**:
- `rawScore` = 플랫폼별 공식 (P/D/A/C 스탯 조합)
- `lBug` = 1 - 1.03^(-B_found)
- `finalScore` = 100 * ln(sAdj+1) / ln(5001)
- `quality` = finalScore + MarketFit + MarketingBonus (0~100 클램프)

**플랫폼별 공식**:
| 플랫폼 | 공식 |
|--------|------|
| Mobile | 1.5P + D + A + 1.5C |
| PC | P + 1.5D + A + 1.5C |
| Nintendo | P + D + 1.5A + 1.5C |
| Console | 6×min(P,D,A,C) + 1.5C |

### `ProjectSaveManager.cs`
개발 진행 상태 저장/복원.
- `SaveProject()` — `UserProject` 테이블에 진행 상태 저장
- `LoadProject()` — 데이터 파싱만 (씬 로드는 별도)
- `RestoreIfNeeded()` — GameScene 초기화 시 진행 중인 프로젝트 복원
- DontDestroyOnLoad

### `ProjectSetupUI.cs`
프로젝트 시작 전 설정 UI.
- 규모(Small/Medium/Large), 장르, 플랫폼 선택
- 선택 완료 시 `DevelopmentManager.StartDevelopment()` 호출

### `DevelopmentPanelUI.cs`
개발 진행 중 수치 실시간 표시.
- 기획 / 개발 / 아트 / 버그 / 창의성 수치 막대

### `DevelopmentResultUI.cs`
개발 완료 후 결과 화면.
- finalScore, 버그 수, 각 수치 표시
- 평론가 리뷰로 넘기는 버튼

### `DevelopmentTimerUI.cs`
개발 진행도 타이머 UI.

---

## 4. 직원 관리

### `EmployeeManager.cs`
직원 데이터 전체 관리.
- `HireEmployee(poolData)` — 채용 확정, 뒤끝 저장 → `OfficeManager.OnEmployeeHired()`
- `FireEmployee(id)` — 해고, 뒤끝 삭제 → `OfficeManager.OnEmployeeFired()`
- `UpdateEmployee(data)` — 수치 변경 후 저장
- `GetEmployee(id)` — id로 직원 데이터 반환
- `ownedEmployees` — 보유 직원 리스트
- `satisfactionDecayPerWeek` (기본값 1) — 인스펙터 설정, 매주 자동 감소
- `OnWeekPassed()` — `GameTimeManager.OnTimeChanged` 구독, 매주 모든 직원 만족도 감소 후 저장
- 뒤끝 `Employee` 테이블 (GameData)
- DontDestroyOnLoad

### `EmployeeData.cs`
직원 데이터 모델.

| 필드 | 설명 |
|------|------|
| id, employeeName | 식별자, 이름 |
| role | Planner / Programmer / Artist |
| grade | Normal / Rare / Epic / Unique |
| potential | F / D / C / B / A |
| developSkill, planningSkill, artSkill, perfectionSkill | 확정 스탯 |
| salary | 연봉 |
| enhancementLevel | 강화 단계 (0~25) |
| satisfaction | 만족도 (1~100, 기본 90) |
| assignedDeskId | 배정 책상 |
| assignedProjectId | 참여 프로젝트 |
| portraitId | 캐릭터 프리팹 식별자 |
| lastIsFront | 마지막 이동 방향 |

**만족도 배율**:
| 상태 | 조건 | 개발수치 배율 |
|------|------|--------------|
| VeryHappy | 90+ | 1.2× |
| Happy | 80~90 | 1.0× |
| Neutral | 70~80 | 1.0× |
| Unhappy | 60~70 | 0.8× |
| VeryUnhappy | 50이하 | 0.8× |

### `HiringUI.cs`
채용 UI.
- 티어별 채용 비용/확률 표시
- 채용 결과 애니메이션

### `EmployeeListUI.cs`
보유 직원 목록 UI.
- 직원 카드 표시, 해고 버튼

### `TrainingUI.cs`
직원 강화 UI (스타포스 방식).
- 0~14강: 성공/하락 (0강은 하락 없음)
- 15~25강: 성공/유지
- potential별 주스탯/서브스탯 증가량 상이

### `LeaderSelectUI.cs`
개발 단계별 팀장 선택 UI (기획/개발/아트).
- 0% (기획팀장), 25% (개발팀장), 75% (아트팀장) 시점 등장

### `SalaryNegotiationManager.cs`
연봉 협상 시스템.
- 연도 변경 시 자동 발동, 직원 큐 순차 처리
- +500G 제안, 거절 시 `resignChance` 확률로 퇴사
- DontDestroyOnLoad

---

## 5. 마케팅 & 판매

### `MarketingUI.cs`
마케팅 활동 UI.
- 5가지 마케팅 방식 (비용/효과 상이)
- 마케팅 비용: `SpendGold(saveImmediately=false)` → 완료 시 `SaveMoney()`
- `marketingBonus` → quality 점수에 반영

### `CriticReviewUI.cs`
평론가 리뷰 화면.
- finalScore 기준 1~10점 산정, 평론가별 ±1 랜덤 변동
- 4명 평론가 슬롯 미리 생성 (GridLayoutGroup 유지 위해 SetActive 대신 텍스트 초기화)
- `totalScoreObject` / `totalScoreText` — 4명 합산 총점 표시
- `LastCriticTotal` 프로퍼티 — SalesUI에서 completedData에 저장용
- Show 시 `StopTime()`, 확인 버튼 시 `StartTime()`
- `OnClickRelease()` 흐름: 평론가 → 출시 랜덤이벤트 → 마케팅 → 점수계산 → 판매

### `SalesUI.cs`
판매량 차트 애니메이션.
- `totalUnits = qualityScore × Random(130~161)`
- 규모별 감소 곡선: Small=0.55, Medium=0.70, Large=0.82
- `barCount` 인스펙터 설정

### `FeedbackUI.cs`
게임 평가 피드백 표시.
- marketFit, marketingBonus, bug, releaseEventBonus 기반 멘트 생성

---

## 6. 퀘스트

### `QuestManager.cs`
퀘스트 로드/진행/보상 관리.
- 뒤끝 Chart에서 퀘스트 마스터 로드
- `UpdateProgress(type, value)` — 조건 달성 체크
- `ClaimReward(questId)` — 보상 수령
- 뒤끝 `UserQuest` 테이블 저장
- DontDestroyOnLoad

### `QuestUI.cs`
퀘스트 목록 UI.

### `QuestData.cs`
퀘스트 데이터 모델 (조건, 보상, 진행도).

---

## 7. 랜덤 이벤트

### `RandomEventManager.cs`
이벤트 풀 관리 및 트리거.
- `CheckTrigger(stage)` — 단계별 이벤트 발동 확인
- `TryTriggerNetworkIssue()` — 25% 시점 네트워크 이슈
- `TriggerInvestmentEvent()` — 프로젝트 시작 시 투자 이벤트

**투자 이벤트 설정값**:
```
investmentTriggerChance = 0.5f
investmentThreshold     = 80f
investmentReward        = 1000G
InvestmentStat          = "planning"/"develop"/"art"/"creativity"
```

**네트워크 이슈 설정값**:
```
networkIssueTriggerChance = 0.5f
networkSpeedMultiplier    = 0.8f
networkIssueDuration      = 0.1f (진행도 %)
```
- DontDestroyOnLoad

### `RandomEvents_Dev.cs`
개발 중 이벤트 풀 등록 (50% 시점).
| 이벤트 | 효과 |
|--------|------|
| Blackout | 모든 수치 0.5배, 만족도 -10 |
| TeamDinner | 모든 수치 2배, 만족도 +10 |
| DevBoost | 개발 +50 |
| PlanBoost | 기획 +50 |
| ArtBoost | 아트 +50 |
| CreativityBoost | 창의성 +30 |

### `RandomEvents_Release.cs`
출시 이벤트 풀 등록.
| 이벤트 | 효과 |
|--------|------|
| CompetitorRelease | sAdj -3 |
| PerfectTiming | sAdj +3 |
| AlgorithmChoice | 매출 상승 (미구현) |

### `RandomEvents_Condition.cs`
조건 이벤트 풀 등록 (미구현).
- EmployeeRun: 만족도 50이하 → 퇴사
- EmployeeFight: 직원 불화
- BadCompanyEvent: 2년내 2명 해고 → 채용 패널티

### `RandomEventUI.cs`
이벤트 팝업 UI (이벤트명, 설명, 확인 버튼).

### `InvestmentProgressUI.cs`
투자 이벤트 진행도 표시 (목표 수치 달성 여부).

---

## 8. 테크트리

### `TechTreeManager.cs`
기술 노드 해금 관리.
- 5개 카테고리: EmployeeSatisfaction, EmployeeEfficiency, GenrePlatform, Novelty, Utility
- `UnlockNode(nodeId)` — 선행조건 체크 후 100G 차감, 해금
- `unlockedIds` — 콤마 구분 string으로 `UserTechTree` 저장
- DontDestroyOnLoad

### `TechTreeUI.cs`
테크트리 UI.
- 탭 방식 (카테고리별), HorizontalLayoutGroup
- 스크롤 위치 코루틴으로 초기화

### `TechTreeData.cs`
노드 데이터 모델 (id, 카테고리, 비용, 선행조건, 효과).

---

## 9. 다이얼로그

### `DialogManager.cs`
다이얼로그 재생 엔진.
- `Play(groupId, triggerOnce)` — 그룹 재생 시작
- `Next()` — 다음 노드 진행
- `Resume()` — 일시정지 후 재개
- `EndDialog()` — 종료, `ContextEmployeeId` 초기화
- `ContextEmployeeId` 프로퍼티 — 현재 다이얼로그 대상 직원 ID (결과 처리용)
- `SetContextEmployeeId(id)` — 다이얼로그 시작 전 직원 ID 세팅
- 플레이스홀더 치환: `{employeeName}`, `{salary}`, `{newSalary}`, `{portraitId}`
- DontDestroyOnLoad

### `DialogUI.cs`
다이얼로그 UI.
- 타이핑 애니메이션 (텍스트 순차 출력)
- 선택지 버튼 동적 생성
- `SetDialogUI()` — GameScene 로드 후 런타임 연결

### `DialogData.cs`
다이얼로그 데이터 모델 (노드, 선택지, 다음 노드 ID).
- `ResultType` enum: `GoldChange`, `SatisfactionChange` 등

### `DialogChartLoader.cs`
뒤끝 Chart API에서 `DialogNode.csv`, `DialogChoice.csv` 로드.

### `EventDialogTable.cs`
이벤트별 다이얼로그 groupId 매핑 테이블.

---

## 10. 공통 UI

### `HUDUI.cs`
메인 HUD.
- 금화(G), 게임 날짜(연/월/주), 직원 연봉 합산 표시
- `RefreshAll()` — 씬 초기화 시 전체 갱신

### `AlertUI.cs`
단순 알림 팝업.
- 표시 시 `StopTime()`, 닫을 때 `StartTime()`

### `ConfirmUI.cs`
확인/취소 팝업.
- 표시 시 `StopTime()`, 닫을 때 `StartTime()`
- 콜백 방식으로 확인/취소 처리

### `LoanUI.cs`
대출 신청 UI.
- 단계별 대출 금액/이자 표시, 신청/상환 버튼

### `CompletedProjectsUI.cs`
완료된 프로젝트 기록 열람 UI.
- `detailQualityScoreText` — 품질 점수 표시
- `detailCriticTotalText` — 평론가 총점 표시

### `SafeAreaPanel.cs`
Safe Area 대응 패널 (노치/홈바 영역 제외).
- 모든 UI는 이 패널 하위에 배치

### `GameUIHelper.cs`
UI 공통 헬퍼 (등급 색상, 텍스트 포맷 등).

---

## 11. 캐릭터 & 오피스

### `OfficeManager.cs`
캐릭터 스폰/복원 총괄.
- `OnEmployeeHired(employee)` — 빈 Desk 배정 → `GetPrefab(portraitId)` 스폰 → GoToDesk() → `EnsurePatrolScheduler()`
- `OnEmployeeFired(employee)` — 캐릭터 제거, Desk 해제
- `RestoreEmployees()` — GameScene 로드 시 보유 직원 전원 복원 → `EnsurePatrolScheduler()`
- `GetPrefab(portraitId)` — `Resources/Characters/{portraitId}` 로드, 없으면 `fallbackPrefab`
- `ShowStatPopup(employeeId, text, color)` — 해당 직원 캐릭터에 팝업 전달
- `EnsurePatrolScheduler()` — 이미 실행 중이 아닐 때만 PatrolScheduler 코루틴 시작 (멱등)
- `PatrolScheduler` — 주기적으로 일반 patrol + dialog patrol 트리거 (스테이지 체크 없음)
- `TriggerDialogPatrolRandom()` — 보유 직원 랜덤 1명을 `DialogPatrolPoint`로 보냄
- `_dialogPatrolPoints` — lazy-load (`TriggerDialogPatrolRandom` 호출 시 없으면 FindObjectsByType)

> 프리팹 위치: `Assets/Resources/Characters/`
> 파일명 = portraitId (예: `portrait_emp_01.prefab`)

### `OfficeCharacter.cs`
사무실 캐릭터 래퍼.
- `Init(empId, desk)` — employeeId, 책상 연결
- `GoToDesk()` — 배정 책상으로 이동 명령
- `ShowStatPopup(text, color)` — `StatFloatingTextPool`에서 팝업 꺼내 머리 위 표시
- `statPopupAnchor` — 팝업 위치 기준점 (미설정 시 `+0.6f` 자동 적용)
- `StartPatrolWithDialog(target, dialogGroupId, triggerOnce)` — 다이얼로그 patrol 시작
- `PatrolWithDialogRoutine` — 목적지 이동 → employeeName/portraitId 플레이스홀더 세팅 → `ContextEmployeeId` 세팅 → `StopTime` → 다이얼로그 재생 → `OnDialogEnd` 대기 → `StartTime` → 책상 복귀

### `DialogPatrolPoint.cs`
다이얼로그 트리거 순찰 지점 마커 컴포넌트.
- `employeeId` — 특정 직원 지정 (빈 값이면 랜덤)
- `dialogGroupId` — 재생할 다이얼로그 그룹
- `triggerOnce` — 1회만 발동 여부

### `StatFloatingText.cs`
머리 위 부유 텍스트 컴포넌트.
- `Show(text, color)` — 텍스트/색상 설정 후 애니메이션 시작
- 위로 이동하며 페이드아웃, 종료 시 `StatFloatingTextPool`로 반환
- `GameTimeManager.IsRunning` 체크 — 시간 멈춤 시 애니메이션도 정지
- `floatSpeed`, `duration` 인스펙터 조정 가능
- 프리팹: `Assets/Resources/StatFloatingText.prefab`

### `StatFloatingTextPool.cs`
StatFloatingText Object Pool.
- Awake 시 `poolSize`(기본 20)개 미리 생성
- `Get(position)` — 비활성 오브젝트 꺼내 위치 설정 후 반환
- `Return(item)` — 비활성화 후 풀에 반환 (GC 없음)
- `poolSize` 인스펙터 조정 가능
- GameScene의 오브젝트에 컴포넌트 추가 필요

### `CharacterController.cs`
캐릭터 이동 상태 관리.
- `MoveTo(cell, worldPos)` — A* 경로 계산 후 이동 시작
- `Patrol()` — WaypointPath 순찰
- `OnMoveComplete()` — 도착 시 콜백, `lastIsFront` 저장

### `CharacterMover.cs`
경로(Vector3 리스트)를 따라 실제 이동 처리.
- 속도, 도착 판정 처리

### `CharacterAnimator.cs`
애니메이션 & 방향 제어.
- `SetIdle(isFront)` — `animator.speed = 0`, 방향 유지
- `SetWalk(isFront)` — `animator.speed = 1`
- `SpriteRenderer.flipX` = `delta.x < 0` (좌우 반전)
- Animator Parameters: `isFront (Bool)`

### `IsometricSorter.cs`
아이소메트릭 Y좌표 기반 sortingOrder 자동 설정.
```csharp
sortingOrder = Mathf.RoundToInt(-transform.position.y * sortMultiplier);
```
- 모든 오브젝트 동일 Sorting Layer 사용, Sprite Pivot = Bottom

### `DeskManager.cs`
책상 배정/해제 관리.
- `GetEmptyDesk()` — 빈 책상 반환
- `AssignDesk(deskId, employeeId)` — 배정
- `UnassignDesk(deskId)` — 해제
- `GetDeskById(deskId)` — ID로 조회

### `WorkStation.cs`
책상 오브젝트.
- `deskId`, `workPoint` (캐릭터 이동 목표 셀/월드 좌표)
- `GetWorkCell()`, `GetWorkWorldPos()`

### `CharacterManager.cs`
씬 내 캐릭터 선택 및 명령 처리.

---

## 12. 길찾기 & 그리드

### `GridManager.cs`
타일맵 기반 이소메트릭 그리드.
- 장애물 셀 등록/해제
- 월드 좌표 ↔ 그리드 셀 변환
- A*에 통행 가능 여부 제공

### `AStarPathfinder.cs`
A* 경로 탐색 알고리즘.
- `FindPath(start, goal)` → `List<Vector2Int>` 반환
- GridManager에서 장애물 정보 참조

### `WaypointPath.cs`
순찰 경로 정의.
- 웨이포인트 리스트, 루프/핑퐁 옵션

---

## 13. 씬 초기화

### `StageManager.cs`
게임 단계(스테이지) 관리.
- `CurrentStage` — 현재 단계 (인스펙터에서 직접 수정)
- `MaxEmployeeCount` — 단계별 최대 직원 수 (`maxEmployeePerStage[]` 배열로 인스펙터 설정)
- 단계 추가 시 배열 크기만 늘리면 됨 (인덱스 0 = 1단계)
- LoadingScene 오브젝트에 컴포넌트 추가, DontDestroyOnLoad

### `GameSceneInitializer.cs`
GameScene 진입 시 초기화 순서 관리.
```csharp
void Start()
{
    var dialogUI = FindAnyObjectByType<DialogUI>();
    if (dialogUI != null) DialogManager.Instance.SetDialogUI(dialogUI);

    SalaryNegotiationManager.Instance.InitializeUI();
    ProjectSaveManager.Instance.RestoreIfNeeded();
    HUDUI.Instance?.RefreshAll();
    OfficeManager.Instance?.RestoreEmployees();
}
```

### `LogoScenario.cs`
로고 씬 연출 (로고 페이드인/아웃 후 로딩씬 전환).

### `Progress.cs`
로딩 씬 진행 바 UI.

---

## 14. 데이터 모델

### `ProjectData.cs`
프로젝트 규모(Small/Medium/Large), 장르, 플랫폼 enum 및 메타데이터.

### `CompletedProjectData.cs`
완료된 프로젝트 기록 (이름, 점수, 판매량, 날짜 등).
- `qualityScore` (float) — 품질 점수
- `criticTotalScore` (int) — 평론가 총점

### `CharacterData.cs`
캐릭터 위치/상태 직렬화 데이터.

### `CharacterState.cs`
캐릭터 상태 열거형 (Idle, Walking, Working 등).

---

## 설계 원칙

| 원칙 | 내용 |
|------|------|
| 뒤끝 스키마 변경 | 컬럼 추가 시 기존 데이터 삭제 후 재생성 |
| OnEnable 타이밍 | OnEnable이 Setup보다 먼저 실행 → Unique 코루틴은 OnEnable에서만 시작 |
| 캐릭터 정체성 | stat 범위는 캐릭터별 고정 (potential/grade 무관) |
| BackEnd 역할 분리 | Chart=마스터/읽기전용, GameData=유저별 가변 |
| 저장 타이밍 | 중요 상태 변경 후 SaveProject() + SaveGameTime() 필수 호출 |
| 씬 종속 UI | AlertUI, ConfirmUI 등은 GameScene에만 배치 |
| 시간 멈춤 | Alert/ConfirmUI 표시 시 StopTime, 닫을 때 StartTime |

---

## PENDING 항목

- [ ] 조건 이벤트 트리거 시점 (`CheckConditionEvents` 호출 위치)
- [ ] Scout, BetaTestIssue, AlgorithmEvent, EmployeeRunEvent, EmployeeFightEvent, BadCompanyEvent 구현
- [ ] 테크트리 실제 효과 연동
- [ ] 규모별 secondsPerWeek 차등 (Small:10, Medium:8, Large:6)
- [ ] iOS TestFlight 배포 (GitHub Actions 워크플로우 작성, 인증서/PP/API Key 등록)
  - 코드 완료 (GameCenterLogin, GameCenterPlugin.mm, LoginButtonPanel)
  - 뒤끝 콘솔 Apple 소셜 로그인 설정 필요 (Team ID / Service ID / Key ID + .p8)
  - Xcode에서 "Sign in with Apple" Capability 추가 필요
- [ ] 닉네임 설정 UI
- [ ] 파산 후처리
- [ ] 프로젝트 개발 중 캐릭터 patrol 연동
