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
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.CompetitorGame,
            // title       = "대형 게임사의 경쟁작 출시",
            // description = "TODO",
            // weight      = mgr.competitorGameWeight,
            // categoryMin = 4, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

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
        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.AvoidingEmployee,
            // title       = "나를 피하는 직원",
            // description = "TODO",
            // weight      = mgr.avoidingEmployeeWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.Cold,
            // title       = "감기",
            // description = "TODO",
            // weight      = mgr.coldWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.BadReview,
            // title       = "이유 없는 별점 1점",
            // description = "TODO",
            // weight      = mgr.badReviewWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.Birthday,
            // title       = "생일",
            // description = "TODO",
            // weight      = mgr.birthdayWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.EarlyLeaveRequest,
            // title       = "퇴근 요청",
            // description = "TODO",
            // weight      = mgr.earlyLeaveRequestWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.EquipmentUpgrade,
            // title       = "장비 업그레이드 요청",
            // description = "TODO",
            // weight      = mgr.equipmentUpgradeWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.GameUpgradeRequest,
            // title       = "게임 업그레이드 요청",
            // description = "TODO",
            // weight      = mgr.gameUpgradeRequestWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

        Add(pool, chart, new RandomEventData
        {
            type    = RandomEventType.CompanyDinner,
            // title       = "오늘은 회식이다!",
            // description = "TODO",
            // weight      = mgr.companyDinnerWeight,
            // categoryMin = 1, categoryMax = 4,
            onApply = () => { /* TODO */ }
        });

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

        // ── 26~99% ──────────────────────────────────────────────
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
