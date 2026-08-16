using System.Collections.Generic;
using UnityEngine;

// ── 카테고리 구간 ────────────────────────────────────────────
// 1:  1~24%   2: 26~49%   3: 51~74%   4: 76~99%
//
// categoryMin~categoryMax 범위에 속하는 카테고리 풀에 포함.
// 스케줄링 중 한 카테고리에서 선택된 이벤트는 이후 카테고리에서 완전 제외.
//
// 이벤트별 등장 범위
//   CompetitorGame   76~99%  (min=4, max=4)
//   TangsuYukFight   76~99%  (min=4, max=4)
//   NetworkIssue      1~74%  (min=1, max=3)
//   AvoidingEmployee  1~99%  (min=1, max=4)
//   Cold              1~99%  (min=1, max=4)
//   BadReview         1~99%  (min=1, max=4)
//   Birthday          1~99%  (min=1, max=4)
//   EarlyLeaveRequest 1~99%  (min=1, max=4)
//   EquipmentUpgrade  1~99%  (min=1, max=4)
//   CompanyDinner     1~99%  (min=1, max=4)
//   BossGossip        1~99%  (min=1, max=4)
//   HackyCode        26~99%  (min=2, max=4)
//   YoutuberRequest  51~99%  (min=3, max=4)

public static class RandomEvents_Dev
{
    public static void Register(List<RandomEventData> pool, RandomEventManager mgr,
                                System.Collections.Generic.Dictionary<string, RandomEventChartRow> chart = null)
    {
        // ── 76~99% ──────────────────────────────────────────────

        // ── 대형 게임사의 경쟁작 출시 ───────────────────────────
        // CDN 로드 실패 시 차트 기본값 (RandomEvent_Chart.csv > CompetitorGame):
        //   title       = "대형 게임사의 경쟁작 출시"
        //   description = "사장님 대기업에서 역대급 게임이 나와서 직원들의 기가 죽고 있습니다"
        //   systemMessage = "전체 직원 만족도 -5"
        //   weight=4, categoryMin=4, categoryMax=4, portraitId=portrait_secretary
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.CompetitorGame,
            onApply = () =>
            {
                foreach (var emp in EmployeeManager.Instance.ownedEmployees)
                {
                    int before = emp.satisfaction;
                    emp.ChangeSatisfaction(-5);
                    InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
                }
            }
        });

        // ── 1~99% ───────────────────────────────────────────────

        // ── 나를 피하는 직원 ─────────────────────────────────────
        // CDN 로드 실패 시 차트 기본값 (RandomEvent_Chart.csv > AvoidingEmployee):
        //   title       = "나를 피하는 직원"
        //   description = "사장님 저를 꿈속에서 해고 했어요 오늘은 별로 보고 싶지 않네요"
        //   systemMessage = "{0} 만족도 -10"  ({0} → 직원이름)
        //   weight=1, categoryMin=1, categoryMax=4, requiresPatrol=true, requiredPatrolPointId=master_desk
        {
            EmployeeData avoidEmp = null;
            RandomEventData avoidEvt = null;
            avoidEvt = new RandomEventData
            {
                type = RandomEventType.AvoidingEmployee,
                onSetup = () =>
                {
                    var employees = EmployeeManager.Instance.ownedEmployees;
                    if (employees.Count == 0) { avoidEvt.cancelled = true; return; }
                    avoidEmp = employees[UnityEngine.Random.Range(0, employees.Count)];
                    avoidEvt.portraitId      = avoidEmp.portraitId;
                    avoidEvt.targetEmployeeId = avoidEmp.id;
                    avoidEvt.systemMessage   = string.Format(avoidEvt.systemMessage, avoidEmp.employeeName);
                },
                onApply = () =>
                {
                    var emp = EmployeeManager.Instance.GetEmployee(avoidEvt.targetEmployeeId);
                    if (emp == null) return;
                    int before = emp.satisfaction;
                    emp.ChangeSatisfaction(-10);
                    InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
                }
            };
            Add(pool, chart, avoidEvt);
        }

        // ── 감기 ─────────────────────────────────────────────────
        // CDN 로드 실패 시 차트 기본값 (RandomEvent_Chart.csv > Cold):
        //   title       = "감기"
        //   description = "어제 문을 열어두고 자서 그런지 컨디션이 안좋네요"
        //   systemMessage = "{0} {1}주간 능력치 -20%"  ({0} → 직원이름, {1} → 주수)
        //   weight=1, categoryMin=1, categoryMax=4, requiresPatrol=true, requiredPatrolPointId=master_desk
        {
            EmployeeData coldEmp = null;
            int coldWeeks = 0;
            RandomEventData coldEvt = null;
            coldEvt = new RandomEventData
            {
                type = RandomEventType.Cold,
                onSetup = () =>
                {
                    var employees = EmployeeManager.Instance.ownedEmployees;
                    if (employees.Count == 0) { coldEvt.cancelled = true; return; }
                    coldEmp   = employees[UnityEngine.Random.Range(0, employees.Count)];
                    coldWeeks = UnityEngine.Random.Range(4, 9);
                    coldEvt.portraitId    = coldEmp.portraitId;
                    coldEvt.targetEmployeeId = coldEmp.id;
                    if (!string.IsNullOrEmpty(coldEvt.systemMessage))
                        coldEvt.systemMessage = string.Format(coldEvt.systemMessage, coldEmp.employeeName, coldWeeks);
                },
                onApply = () =>
                {
                    var emp = EmployeeManager.Instance.GetEmployee(coldEvt.targetEmployeeId);
                    if (emp == null) return;
                    emp.ApplyStatDebuff(coldWeeks);
                    InfoFeedUI.Instance?.ShowStatBuff(emp, coldWeeks, 20, false);
                }
            };
            Add(pool, chart, coldEvt);
        }

        // ── 이유 없는 별점 1점 ──────────────────────────────────
        // CDN 로드 실패 시 차트 기본값 (RandomEvent_Chart.csv > BadReview):
        //   title       = "이유 없는 별점 1점"
        //   description = "새로운 리뷰가 올라왔는데 '게임은 안 해봤지만 오늘 비가 와서 기분이 안 좋네요. 별점 1점 드립니다.'라고 적혀 있습니다. 이걸 본 담당 직원이 멘탈이 나가서 울고 있어요."
        //   systemMessage = "{0} 만족도 -10"  ({0} → 직원이름)
        //   weight=1, categoryMin=1, categoryMax=4, portraitId=portrait_secretary
        {
            EmployeeData badReviewEmp = null;
            RandomEventData badReviewEvt = null;
            badReviewEvt = new RandomEventData
            {
                type = RandomEventType.BadReview,
                onSetup = () =>
                {
                    var employees = EmployeeManager.Instance.ownedEmployees;
                    if (employees.Count == 0) { badReviewEvt.cancelled = true; return; }
                    badReviewEmp = employees[UnityEngine.Random.Range(0, employees.Count)];
                    badReviewEvt.targetEmployeeId = badReviewEmp.id;
                    if (!string.IsNullOrEmpty(badReviewEvt.systemMessage))
                        badReviewEvt.systemMessage = string.Format(badReviewEvt.systemMessage, badReviewEmp.employeeName);
                },
                onApply = () =>
                {
                    var emp = EmployeeManager.Instance.GetEmployee(badReviewEvt.targetEmployeeId);
                    if (emp == null) return;
                    int before = emp.satisfaction;
                    emp.ChangeSatisfaction(-10);
                    InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
                }
            };
            Add(pool, chart, badReviewEvt);
        }


        // ── 1~74% ───────────────────────────────────────────────

        // ── 네트워크 끊김 ────────────────────────────────────────
        // CDN 로드 실패 시 차트 기본값 (RandomEvent_Chart.csv > NetworkIssue):
        //   title       = "네트워크 끊김"
        //   description = "사장님 제가 넘어지면서 실수로 코드를 뽑아버렸습니다..."
        //   systemMessage = "개발 기간 {0}주 지연"  ({0} → 지연 주수)
        //   weight=1.3, categoryMin=1, categoryMax=3, requiresPatrol=true, requiredPatrolPointId=master_desk
        {
            int delayWeeks = 0;
            RandomEventData networkEvt = null;
            networkEvt = new RandomEventData
            {
                type = RandomEventType.NetworkIssue,
                onSetup = () =>
                {
                    var employees = EmployeeManager.Instance.ownedEmployees;
                    if (employees.Count == 0) { networkEvt.cancelled = true; return; }
                    var emp = employees[UnityEngine.Random.Range(0, employees.Count)];
                    delayWeeks = ProjectSetupUI.SelectedScale switch
                    {
                        ProjectScale.Small  => 1,
                        ProjectScale.Medium => 2,
                        ProjectScale.Large  => 3,
                        _ => 1
                    };
                    networkEvt.portraitId       = emp.portraitId;   // 랜덤 직원 → 동적
                    networkEvt.targetEmployeeId = emp.id;
                    // description은 차트에서 관리 — 하드코딩 제거
                    // systemMessage는 차트 템플릿({0})에 주수 대입
                    networkEvt.systemMessage = string.Format(networkEvt.systemMessage, delayWeeks);
                },
                onApply = () =>
                {
                    float secondsPerWeek = ProjectSetupUI.SelectedScale switch
                    {
                        ProjectScale.Small  => 80f / 16f, // 5.0초/주
                        ProjectScale.Medium => 80f / 24f, // 3.33초/주
                        ProjectScale.Large  => 80f / 32f, // 2.5초/주
                        _ => 80f / 16f
                    };
                    DevelopmentManager.Instance.ExtendDevelopmentDuration(delayWeeks * secondsPerWeek, delayWeeks * 2 * secondsPerWeek); // 연장 N주 / 감속 2N주
                    InfoFeedUI.Instance?.ShowDevelopmentDelay(
                        EmployeeManager.Instance.GetEmployee(networkEvt.targetEmployeeId), delayWeeks);
                }
            };
            RandomEventChartLoader.Apply(networkEvt, chart);
            pool.Add(networkEvt);
        }

        // ── 드릴 소리에 내 머리도 지잉~ ────────────────────────
        // CDN 로드 실패 시 차트 기본값 (RandomEvent_Chart.csv > DrillEvent):
        //   title       = "드릴 소리에 내 머리도 지잉~"
        //   description = "옆 사무실에서 공사를 시작했는데 하루 종일 '드르륵- 쾅쾅!' 소리가 들려서 도저히 집중할 수가 없습니다"
        //   systemMessage = "{0} {1}주간 능력치 -20%"  ({0} → 직원이름, {1} → 주수)
        //   weight=1, categoryMin=1, categoryMax=4, requiresPatrol=true, requiredPatrolPointId=master_desk
        {
            EmployeeData drillEmp = null;
            int drillWeeks = 0;
            RandomEventData drillEvt = null;
            drillEvt = new RandomEventData
            {
                type = RandomEventType.DrillEvent,
                onSetup = () =>
                {
                    var employees = EmployeeManager.Instance.ownedEmployees;
                    if (employees.Count == 0) { drillEvt.cancelled = true; return; }
                    drillEmp   = employees[UnityEngine.Random.Range(0, employees.Count)];
                    drillWeeks = UnityEngine.Random.Range(4, 9);
                    drillEvt.portraitId       = drillEmp.portraitId;
                    drillEvt.targetEmployeeId = drillEmp.id;
                    if (!string.IsNullOrEmpty(drillEvt.systemMessage))
                        drillEvt.systemMessage = string.Format(drillEvt.systemMessage, drillEmp.employeeName, drillWeeks);
                },
                onApply = () =>
                {
                    var emp = EmployeeManager.Instance.GetEmployee(drillEvt.targetEmployeeId);
                    if (emp == null) return;
                    emp.ApplyStatDebuff(drillWeeks);
                    InfoFeedUI.Instance?.ShowStatBuff(emp, drillWeeks, 20, false);
                }
            };
            Add(pool, chart, drillEvt);
        }

        // ── 도둑이야! ────────────────────────────────────────────
        // CDN 로드 실패 시 차트 기본값 (RandomEvent_Chart.csv > ThiefEvent):
        //   title       = "도둑이야!"
        //   description = "어제 회사에 도둑이 들었습니다! 확인해보니 내부자의 소행인 것 같기도?"
        //   systemMessage = onSetup에서 결과에 맞게 직접 설정 (CSV값 무시)
        //   weight=1, categoryMin=1, categoryMax=4, portraitId=portrait_secretary
        {
            int    stolenGold          = 0;
            string preselectedItemId   = null; // onSetup에서 미리 선정, onApply에서 실제 차감
            bool   stealItemMode       = false;
            RandomEventData thiefEvt   = null;
            thiefEvt = new RandomEventData
            {
                type = RandomEventType.ThiefEvent,
                onSetup = () =>
                {
                    // 50:50 — 아이템이 없으면 돈 도난으로 fallback
                    if (UnityEngine.Random.value < 0.5f && ItemManager.Instance != null)
                        preselectedItemId = ItemManager.Instance.PickRandomItemId();

                    stealItemMode = !string.IsNullOrEmpty(preselectedItemId);

                    if (stealItemMode)
                    {
                        var itemChart = ItemChartLoader.Cache;
                        string itemName = itemChart.TryGetValue(preselectedItemId, out var row)
                            ? row.name : preselectedItemId;
                        thiefEvt.systemMessage = $"'{itemName}'을(를) 도난당했습니다!";
                    }
                    else
                    {
                        stolenGold = Mathf.Max(1, (int)(MoneyManager.Instance.Gold * 0.05f));
                        thiefEvt.systemMessage = $"{stolenGold}G가 감소하였습니다";
                    }
                },
                onApply = () =>
                {
                    if (stealItemMode)
                        ItemManager.Instance?.StealSpecificItem(preselectedItemId);
                    else
                        MoneyManager.Instance.ForceSpendGold(stolenGold);
                }
            };
            Add(pool, chart, thiefEvt);
        }

        // ── 26~99% ──────────────────────────────────────────────

        // ── 51~99% ──────────────────────────────────────────────
    }

    static void Add(List<RandomEventData> pool,
                    System.Collections.Generic.Dictionary<string, RandomEventChartRow> chart,
                    RandomEventData evt)
    {
        RandomEventChartLoader.Apply(evt, chart);
        pool.Add(evt);
    }

    // ── 튜토리얼 전용 — 나를 피하는 직원 (Tut3Event) ──────────────────
    // [CDN fallback — RandomEvent_Chart.csv 의 Tut3Event 행]
    // title/description은 AvoidingEmployee와 완전히 동일, systemMessage만 "{0} 만족도 -20"으로 강화
    // (원래 AvoidingEmployee는 -10). 대상 직원은 랜덤이 아니라 targetEmp로 강제 지정(desk_02 등).
    // 일반 랜덤 풀에는 등록하지 않음(Register의 pool.Add 없음) — RandomEventManager.TriggerTutorial3Event가
    // 결정적으로만 발동시킨다.
    public static RandomEventData CreateTut3Event(
        System.Collections.Generic.Dictionary<string, RandomEventChartRow> chart, EmployeeData targetEmp)
    {
        RandomEventData evt = null;
        evt = new RandomEventData
        {
            type = RandomEventType.Tut3Event,
            onApply = () =>
            {
                var emp = EmployeeManager.Instance.GetEmployee(evt.targetEmployeeId);
                if (emp == null) return;
                int before = emp.satisfaction;
                emp.ChangeSatisfaction(-20);
                InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
            }
        };
        RandomEventChartLoader.Apply(evt, chart);

        // [CDN fallback] "{0} 만족도 -20"
        string systemMessageTemplate = evt.systemMessage ?? "";

        evt.onSetup = () =>
        {
            evt.portraitId       = targetEmp.portraitId;
            evt.targetEmployeeId = targetEmp.id;
            evt.systemMessage    = string.Format(systemMessageTemplate, targetEmp.employeeName);
        };
        return evt;
    }
}
