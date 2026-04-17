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
//   GameUpgradeRequest1~99%  (min=1, max=4)
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
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.CompetitorGame,
            onApply = () =>
            {
                foreach (var emp in EmployeeManager.Instance.ownedEmployees)
                {
                    emp.ChangeSatisfaction(-10);
                    OfficeManager.Instance?.ShowStatPopup(emp.id, "만족도 -10", new Color(0.4f, 0.6f, 1f));
                }
            }
        });

        // ── 탕수육 부먹 찍먹 싸움 ───────────────────────────────
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.TangsuYukFight,
            // title       = "탕수육 부먹 찍먹 싸움",
            // description = "TODO",
            // weight      = mgr.tangsuYukFightWeight,
            // categoryMin = 4, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        // ── 1~99% ───────────────────────────────────────────────

        // ── 나를 피하는 직원 ─────────────────────────────────────
        {
            EmployeeData avoidEmp = null;
            RandomEventData avoidEvt = null;
            avoidEvt = new RandomEventData
            {
                type = RandomEventType.AvoidingEmployee,
                onSetup = () =>
                {
                    var employees = EmployeeManager.Instance.ownedEmployees;
                    avoidEmp = employees[UnityEngine.Random.Range(0, employees.Count)];
                    avoidEvt.portraitId      = avoidEmp.portraitId;
                    avoidEvt.targetEmployeeId = avoidEmp.id;
                    avoidEvt.systemMessage   = string.Format(avoidEvt.systemMessage, avoidEmp.employeeName);
                },
                onApply = () =>
                {
                    var emp = EmployeeManager.Instance.GetEmployee(avoidEvt.targetEmployeeId);
                    if (emp == null) return;
                    emp.ChangeSatisfaction(-10);
                    OfficeManager.Instance?.ShowStatPopup(emp.id, "만족도 -10", new Color(0.4f, 0.6f, 1f));
                }
            };
            Add(pool, chart, avoidEvt);
        }

        // ── 감기 ─────────────────────────────────────────────────
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
                    OfficeManager.Instance?.ShowStatPopup(emp.id, $"능력치 -{coldWeeks}주", new Color(0.4f, 0.6f, 1f));
                }
            };
            Add(pool, chart, coldEvt);
        }

        // ── 이유 없는 별점 1점 ──────────────────────────────────
        {
            EmployeeData badReviewEmp = null;
            RandomEventData badReviewEvt = null;
            badReviewEvt = new RandomEventData
            {
                type = RandomEventType.BadReview,
                onSetup = () =>
                {
                    var employees = EmployeeManager.Instance.ownedEmployees;
                    badReviewEmp = employees[UnityEngine.Random.Range(0, employees.Count)];
                    badReviewEvt.targetEmployeeId = badReviewEmp.id;
                    if (!string.IsNullOrEmpty(badReviewEvt.systemMessage))
                        badReviewEvt.systemMessage = string.Format(badReviewEvt.systemMessage, badReviewEmp.employeeName);
                },
                onApply = () =>
                {
                    var emp = EmployeeManager.Instance.GetEmployee(badReviewEvt.targetEmployeeId);
                    if (emp == null) return;
                    emp.ChangeSatisfaction(-10);
                    OfficeManager.Instance?.ShowStatPopup(emp.id, "만족도 -10", new Color(0.4f, 0.6f, 1f));
                }
            };
            Add(pool, chart, badReviewEvt);
        }

        // ── 퇴근 요청 ────────────────────────────────────────────
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.EarlyLeaveRequest,
            // title       = "퇴근 요청",
            // description = "TODO",
            // weight      = mgr.earlyLeaveRequestWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        // ── 장비 업그레이드 요청 ─────────────────────────────────
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.EquipmentUpgrade,
            // title       = "장비 업그레이드 요청",
            // description = "TODO",
            // weight      = mgr.equipmentUpgradeWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        // ── 게임 업그레이드 요청 ─────────────────────────────────
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.GameUpgradeRequest,
            // title       = "게임 업그레이드 요청",
            // description = "TODO",
            // weight      = mgr.gameUpgradeRequestWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        // ── 오늘은 회식이다! ─────────────────────────────────────
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.CompanyDinner,
            // title       = "오늘은 회식이다!",
            // description = "TODO",
            // weight      = mgr.companyDinnerWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        // ── 사장님 뒷담까기 ──────────────────────────────────────
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.BossGossip,
            // title       = "사장님 뒷담까기",
            // description = "TODO",
            // weight      = mgr.bossGossipWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        // ── 1~74% ───────────────────────────────────────────────

        // ── 네트워크 끊김 ────────────────────────────────────────
        {
            int delayWeeks = 0;
            RandomEventData networkEvt = null;
            networkEvt = new RandomEventData
            {
                type = RandomEventType.NetworkIssue,
                // title                 = "네트워크 끊김",
                // weight                = mgr.networkIssueWeight,
                // categoryMin           = 1, categoryMax = 3,
                // requiresPatrol        = true,
                // requiredPatrolPointId = "master_desk",
                onSetup = () =>
                {
                    var employees = EmployeeManager.Instance.ownedEmployees;
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
                        ProjectScale.Small  => 5f,
                        ProjectScale.Medium => 4.2f,
                        ProjectScale.Large  => 3.9f,
                        _ => 5f
                    };
                    DevelopmentManager.Instance.ExtendDevelopmentDuration(delayWeeks * 2 * secondsPerWeek);
                }
            };
            RandomEventChartLoader.Apply(networkEvt, chart);
            pool.Add(networkEvt);
        }

        // ── 드릴 소리에 내 머리도 지잉~ ────────────────────────
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
                    OfficeManager.Instance?.ShowStatPopup(emp.id, $"능력치 -{drillWeeks}주", new Color(0.4f, 0.6f, 1f));
                }
            };
            Add(pool, chart, drillEvt);
        }

        // ── 도둑이야! ────────────────────────────────────────────
        {
            int stolenGold = 0;
            RandomEventData thiefEvt = null;
            thiefEvt = new RandomEventData
            {
                type = RandomEventType.ThiefEvent,
                onSetup = () =>
                {
                    stolenGold = Mathf.Max(1, (int)(MoneyManager.Instance.Gold * 0.05f));
                    if (!string.IsNullOrEmpty(thiefEvt.systemMessage))
                        thiefEvt.systemMessage = string.Format(thiefEvt.systemMessage, stolenGold);
                },
                onApply = () =>
                {
                    MoneyManager.Instance.ForceSpendGold(stolenGold);
                }
            };
            Add(pool, chart, thiefEvt);
        }

        // ── 26~99% ──────────────────────────────────────────────

        // ── 야매코드 ─────────────────────────────────────────────
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.HackyCode,
            // title       = "야매코드",
            // description = "TODO",
            // weight      = mgr.hackyCodeWeight,
            // categoryMin = 2, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        // ── 51~99% ──────────────────────────────────────────────

        // ── 유튜버 선공개 요청 ───────────────────────────────────
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.YoutuberRequest,
            // title       = "유튜버 선공개 요청",
            // description = "TODO",
            // weight      = mgr.youtuberRequestWeight,
            // categoryMin = 3, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });
    }

    static void Add(List<RandomEventData> pool,
                    System.Collections.Generic.Dictionary<string, RandomEventChartRow> chart,
                    RandomEventData evt)
    {
        RandomEventChartLoader.Apply(evt, chart);
        pool.Add(evt);
    }
}
