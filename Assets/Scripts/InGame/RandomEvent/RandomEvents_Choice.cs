using System.Collections.Generic;
using UnityEngine;

public static class RandomEvents_Choice
{
    public static void Register(List<RandomEventChoiceData> pool, RandomEventManager mgr,
                                Dictionary<string, RandomEventChoiceChartRow> chart = null)
    {
        // ── 생일 ─────────────────────────────────────────────────
        {
            EmployeeData birthdayEmp = null;
            RandomEventChoiceData birthdayEvt = null;
            birthdayEvt = new RandomEventChoiceData
            {
                type        = RandomEventType.Birthday,
                weight      = 1f,
                categoryMin = 1,
                categoryMax = 4,
                choices = new List<RandomEventChoiceOption>
                {
                    // ── 선택지 1: 음...그래서? ──────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            var emp = EmployeeManager.Instance.GetEmployee(birthdayEvt.targetEmployeeId);
                            if (emp == null) return;
                            emp.ChangeSatisfaction(-5);
                            OfficeManager.Instance?.ShowStatPopup(emp.id, "만족도 -5", new Color(0.4f, 0.6f, 1f));
                        }
                    },
                    // ── 선택지 2: 강아지 생일 챙기기 ────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            var emp = EmployeeManager.Instance.GetEmployee(birthdayEvt.targetEmployeeId);
                            if (emp == null) return;

                            int cost = Mathf.Max(1, (int)(emp.salary * 0.03f));
                            MoneyManager.Instance.ForceSpendGold(cost, saveImmediately: false);
                            emp.ChangeSatisfaction(5);
                            OfficeManager.Instance?.ShowStatPopup(emp.id, "만족도 +5", new Color(1f, 0.4f, 0.4f));
                        }
                    }
                },
                onSetup = () =>
                {
                    var employees = EmployeeManager.Instance.ownedEmployees;
                    if (employees.Count == 0) { birthdayEvt.cancelled = true; return; }
                    birthdayEmp = employees[Random.Range(0, employees.Count)];
                    birthdayEvt.portraitId      = birthdayEmp.portraitId;
                    birthdayEvt.targetEmployeeId = birthdayEmp.id;

                    // choice2 버튼 텍스트에 골드 금액 반영 (차트 텍스트 뒤에 비용 추가)
                    int cost = Mathf.Max(1, (int)(birthdayEmp.salary * 0.03f));
                    birthdayEvt.choices[1].buttonLabel += $"-{cost}G";

                    // 플레이스홀더 치환 ({해당직원이름}, {비용})
                    birthdayEvt.choices[0].resultSystemMessage =
                        birthdayEvt.choices[0].resultSystemMessage
                            ?.Replace("{해당직원이름}", birthdayEmp.employeeName);
                    birthdayEvt.choices[1].resultSystemMessage =
                        birthdayEvt.choices[1].resultSystemMessage
                            ?.Replace("{해당직원이름}", birthdayEmp.employeeName)
                             .Replace("{비용}", cost.ToString());
                }
            };
            Apply(birthdayEvt, chart);
            pool.Add(birthdayEvt);
        }

        // ── 두 직원 싸움 계열 (EmployeeFight wrapper) ─────────────
        {
            var fightSubs = new List<RandomEventChoiceData>
            {
                CreateTwoEmpFightEvent(RandomEventType.TangsuYukFight, chart),
                CreateTwoEmpFightEvent(RandomEventType.AntiMintchoc,   chart),
                CreateTwoEmpFightEvent(RandomEventType.AcWar,          chart),
            };

            var fightEvt = new RandomEventChoiceData
            {
                type                  = RandomEventType.EmployeeFight,
                weight                = 4f,
                categoryMin           = 3,
                categoryMax           = 4,
                requiresPatrol        = true,
                requiredPatrolPointId = "master_desk",
                choices               = new List<RandomEventChoiceOption>()
            };
            Apply(fightEvt, chart);

            fightEvt.onSetup = () =>
            {
                var chosen = fightSubs[UnityEngine.Random.Range(0, fightSubs.Count)];
                chosen.cancelled = false;
                chosen.onSetup?.Invoke();

                if (chosen.cancelled) { fightEvt.cancelled = true; return; }

                fightEvt.title            = chosen.title;
                fightEvt.description      = chosen.description;
                fightEvt.portraitId       = chosen.portraitId;
                fightEvt.portraitId2      = chosen.portraitId2;
                fightEvt.choices          = chosen.choices;
                fightEvt.targetEmployeeId = chosen.targetEmployeeId;
            };

            pool.Add(fightEvt);
        }

        // ── 퇴근 요청 ─────────────────────────────────────────────
        {
            EmployeeData earlyLeaveEmp = null;
            RandomEventChoiceData earlyLeaveEvt = null;
            earlyLeaveEvt = new RandomEventChoiceData
            {
                type        = RandomEventType.EarlyLeaveRequest,
                weight      = 1f,
                categoryMin = 1,
                categoryMax = 4,
                requiresPatrol        = true,
                requiredPatrolPointId = "master_desk",
                choices = new List<RandomEventChoiceOption>
                {
                    // ── 선택지 1: 거절 ──────────────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            var emp = EmployeeManager.Instance.GetEmployee(earlyLeaveEvt.targetEmployeeId);
                            if (emp == null) return;
                            emp.ChangeSatisfaction(-5);
                            OfficeManager.Instance?.ShowStatPopup(emp.id, "만족도 -5", new Color(0.4f, 0.6f, 1f));
                        }
                    },
                    // ── 선택지 2: 허락 ──────────────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            var emp = EmployeeManager.Instance.GetEmployee(earlyLeaveEvt.targetEmployeeId);
                            if (emp == null) return;
                            emp.ChangeSatisfaction(5);
                            OfficeManager.Instance?.ShowStatPopup(emp.id, "만족도 +5", new Color(1f, 0.4f, 0.4f));

                            int delayWeeks = UnityEngine.Random.Range(1, 4); // 1~3주 랜덤
                            float secondsPerWeek = ProjectSetupUI.SelectedScale switch
                            {
                                ProjectScale.Small  => 5f,
                                ProjectScale.Medium => 4.2f,
                                ProjectScale.Large  => 3.9f,
                                _ => 5f
                            };
                            DevelopmentManager.Instance.ExtendDevelopmentDuration(delayWeeks * 2 * secondsPerWeek);
                        }
                    }
                },
                onSetup = () =>
                {
                    var employees = EmployeeManager.Instance.ownedEmployees;
                    if (employees.Count == 0) { earlyLeaveEvt.cancelled = true; return; }
                    earlyLeaveEmp = employees[UnityEngine.Random.Range(0, employees.Count)];
                    earlyLeaveEvt.portraitId      = earlyLeaveEmp.portraitId;
                    earlyLeaveEvt.targetEmployeeId = earlyLeaveEmp.id;
                }
            };
            Apply(earlyLeaveEvt, chart);
            pool.Add(earlyLeaveEvt);
        }

        // ── 야매 코드 ─────────────────────────────────────────────
        {
            EmployeeData hackyEmp = null;
            RandomEventChoiceData hackyEvt = null;
            hackyEvt = new RandomEventChoiceData
            {
                type        = RandomEventType.HackyCode,
                weight      = 1.3f,
                categoryMin = 2,
                categoryMax = 3,
                requiresPatrol        = true,
                requiredPatrolPointId = "master_desk",
                choices = new List<RandomEventChoiceOption>
                {
                    // ── 선택지 1: 날림 개발 ─────────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            if (UnityEngine.Random.value < 1f)
                            {
                                float bonus = DevelopmentManager.Instance.LeaderDevelopBonusTotal;
                                float penalty = Mathf.Max(1f, bonus * 0.1f);
                                mgr.PendingHackyCodePenalty    = penalty;
                                mgr.PendingHackyCodePortraitId = hackyEvt.portraitId;
                                mgr.PendingHackyCodeWeeksLeft  = 2;
                            }
                        }
                    },
                    // ── 선택지 2: 완벽하게 개발 ─────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            int delayWeeks = UnityEngine.Random.Range(1, 4);
                            float secondsPerWeek = ProjectSetupUI.SelectedScale switch
                            {
                                ProjectScale.Small  => 5f,
                                ProjectScale.Medium => 4.2f,
                                ProjectScale.Large  => 3.9f,
                                _ => 5f
                            };
                            DevelopmentManager.Instance.ExtendDevelopmentDuration(delayWeeks * 2 * secondsPerWeek);
                            string weeks = delayWeeks.ToString();
                            string desc = hackyEvt.choices[1].resultDescription?.Replace("{주수}", weeks);
                            hackyEvt.choices[1].resultDescription = desc;
                            hackyEvt.choices[1].resultDescriptions.Clear();
                            if (!string.IsNullOrEmpty(desc)) hackyEvt.choices[1].resultDescriptions.Add(desc);
                            hackyEvt.choices[1].resultSystemMessage =
                                hackyEvt.choices[1].resultSystemMessage?.Replace("{주수}", weeks);
                        }
                    }
                }
            };
            Apply(hackyEvt, chart);

            // Apply 후 템플릿 캡처 — onSetup에서 매번 복원
            string hackyDesc2Template   = hackyEvt.choices[1].resultDescriptions.Count > 0
                ? hackyEvt.choices[1].resultDescriptions[0] : "";
            string hackySystem2Template = hackyEvt.choices[1].resultSystemMessage ?? "";

            hackyEvt.onSetup = () =>
            {
                var programmers = EmployeeManager.Instance.ownedEmployees
                    .FindAll(e => e.role == EmployeeRole.Programmer);
                if (programmers.Count == 0) { hackyEvt.cancelled = true; return; }
                hackyEmp = programmers[UnityEngine.Random.Range(0, programmers.Count)];
                hackyEvt.portraitId       = hackyEmp.portraitId;
                hackyEvt.targetEmployeeId = hackyEmp.id;

                hackyEvt.choices[1].resultDescription = hackyDesc2Template;
                hackyEvt.choices[1].resultDescriptions.Clear();
                if (!string.IsNullOrEmpty(hackyDesc2Template))
                    hackyEvt.choices[1].resultDescriptions.Add(hackyDesc2Template);
                hackyEvt.choices[1].resultSystemMessage = hackySystem2Template;
            };
            pool.Add(hackyEvt);
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────
    static void Apply(RandomEventChoiceData data,
                      Dictionary<string, RandomEventChoiceChartRow> chart)
    {
        RandomEventChoiceChartLoader.Apply(data, data.type.ToString(), chart);
    }

    // 두 직원 싸움 계열 이벤트 공통 생성 (TangsuYukFight / AntiCilantro / AcWar)
    static RandomEventChoiceData CreateTwoEmpFightEvent(
        RandomEventType type,
        Dictionary<string, RandomEventChoiceChartRow> chart)
    {
        EmployeeData emp1 = null;
        EmployeeData emp2 = null;
        RandomEventChoiceData evt = null;
        evt = new RandomEventChoiceData
        {
            type                  = type,
            weight                = 1f,
            categoryMin           = 4,
            categoryMax           = 4,
            requiresPatrol        = true,
            requiredPatrolPointId = "master_desk",
            choices = new List<RandomEventChoiceOption>
            {
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        var winner = EmployeeManager.Instance.GetEmployee(emp1?.id);
                        if (winner == null) return;
                        winner.ChangeSatisfaction(10);
                        OfficeManager.Instance?.ShowStatPopup(winner.id, "만족도 +10", new Color(1f, 0.4f, 0.4f));
                        float bonus = DevelopmentManager.Instance.GetLeaderBonusByRole(winner.role);
                        float delta = Mathf.Max(1f, bonus * 0.1f);
                        DevelopmentPanelUI.Instance.AddValues(
                            winner.role == EmployeeRole.Planner    ?  delta : 0f,
                            winner.role == EmployeeRole.Programmer ?  delta : 0f,
                            winner.role == EmployeeRole.Artist     ?  delta : 0f,
                            0f, 0f);
                    }
                },
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        var winner = EmployeeManager.Instance.GetEmployee(emp2?.id);
                        if (winner == null) return;
                        winner.ChangeSatisfaction(10);
                        OfficeManager.Instance?.ShowStatPopup(winner.id, "만족도 +10", new Color(1f, 0.4f, 0.4f));
                        float bonus = DevelopmentManager.Instance.GetLeaderBonusByRole(winner.role);
                        float delta = Mathf.Max(1f, bonus * 0.1f);
                        DevelopmentPanelUI.Instance.AddValues(
                            winner.role == EmployeeRole.Planner    ?  delta : 0f,
                            winner.role == EmployeeRole.Programmer ?  delta : 0f,
                            winner.role == EmployeeRole.Artist     ?  delta : 0f,
                            0f, 0f);
                    }
                }
            }
        };
        Apply(evt, chart);

        string label0Template  = evt.choices[0].buttonLabel ?? "";
        string label1Template  = evt.choices[1].buttonLabel ?? "";
        string system0Template = evt.choices[0].resultSystemMessage ?? "";
        string system1Template = evt.choices[1].resultSystemMessage ?? "";
        var    happyDescs      = new List<string>(evt.choices[0].resultDescriptions);
        var    angryDescs      = new List<string>(evt.choices[1].resultDescriptions);
        string happyTitle      = evt.choices[0].resultTitle ?? "";
        string angryTitle      = evt.choices[1].resultTitle ?? "";

        evt.onSetup = () =>
        {
            var availableRoles = new List<EmployeeRole>();
            foreach (var r in new[] { EmployeeRole.Planner, EmployeeRole.Programmer, EmployeeRole.Artist })
                if (EmployeeManager.Instance.ownedEmployees.Exists(e => e.role == r))
                    availableRoles.Add(r);

            if (availableRoles.Count < 2) { evt.cancelled = true; return; }

            for (int i = availableRoles.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var tmp = availableRoles[i]; availableRoles[i] = availableRoles[j]; availableRoles[j] = tmp;
            }

            var pool1 = EmployeeManager.Instance.ownedEmployees.FindAll(e => e.role == availableRoles[0]);
            var pool2 = EmployeeManager.Instance.ownedEmployees.FindAll(e => e.role == availableRoles[1]);
            emp1 = pool1[UnityEngine.Random.Range(0, pool1.Count)];
            emp2 = pool2[UnityEngine.Random.Range(0, pool2.Count)];

            evt.portraitId       = emp1.portraitId;
            evt.portraitId2      = emp2.portraitId;
            evt.targetEmployeeId = emp1.id;

            string role1 = emp1.role switch {
                EmployeeRole.Planner    => "기획",
                EmployeeRole.Programmer => "개발",
                EmployeeRole.Artist     => "아트",
                _ => emp1.role.ToString()
            };
            string role2 = emp2.role switch {
                EmployeeRole.Planner    => "기획",
                EmployeeRole.Programmer => "개발",
                EmployeeRole.Artist     => "아트",
                _ => emp2.role.ToString()
            };

            evt.choices[0].buttonLabel      = label0Template.Replace("{해당직원이름}", emp1.employeeName);
            evt.choices[0].resultPortraitId  = null;
            evt.choices[0].resultPortraitId2 = null;
            evt.choices[1].buttonLabel      = label1Template.Replace("{해당직원이름}", emp2.employeeName);
            evt.choices[1].resultPortraitId  = null;
            evt.choices[1].resultPortraitId2 = emp2.portraitId;

            evt.choices[0].resultSystemMessage = system0Template
                .Replace("{직원1파트}", role1).Replace("{직원1이름}", emp1.employeeName)
                .Replace("{직원2파트}", role2).Replace("{직원2이름}", emp2.employeeName);
            evt.choices[1].resultSystemMessage = system1Template
                .Replace("{직원1파트}", role1).Replace("{직원1이름}", emp1.employeeName)
                .Replace("{직원2파트}", role2).Replace("{직원2이름}", emp2.employeeName);

            evt.choices[0].resultDescriptions = new List<string>(happyDescs);
            evt.choices[0].resultTitle        = happyTitle;
            evt.choices[1].resultDescriptions = new List<string>(happyDescs);
            evt.choices[1].resultTitle        = happyTitle;

            evt.choices[0].secondaryPortraitId   = emp2.portraitId;
            evt.choices[0].secondaryUsePortrait1 = false;
            evt.choices[0].secondaryTitle        = angryTitle;
            evt.choices[0].secondaryDescriptions = new List<string>(angryDescs);
            evt.choices[0].onSecondaryShow = () =>
            {
                var loser = EmployeeManager.Instance.GetEmployee(emp2?.id);
                if (loser == null) return;
                loser.ChangeSatisfaction(-10);
                OfficeManager.Instance?.ShowStatPopup(loser.id, "만족도 -10", new Color(0.4f, 0.6f, 1f));
                float b = DevelopmentManager.Instance.GetLeaderBonusByRole(loser.role);
                float d = Mathf.Max(1f, b * 0.1f);
                DevelopmentPanelUI.Instance.AddValues(
                    loser.role == EmployeeRole.Planner    ? -d : 0f,
                    loser.role == EmployeeRole.Programmer ? -d : 0f,
                    loser.role == EmployeeRole.Artist     ? -d : 0f,
                    0f, 0f);
            };

            evt.choices[1].secondaryPortraitId   = emp1.portraitId;
            evt.choices[1].secondaryUsePortrait1 = true;
            evt.choices[1].secondaryTitle        = angryTitle;
            evt.choices[1].secondaryDescriptions = new List<string>(angryDescs);
            evt.choices[1].onSecondaryShow = () =>
            {
                var loser = EmployeeManager.Instance.GetEmployee(emp1?.id);
                if (loser == null) return;
                loser.ChangeSatisfaction(-10);
                OfficeManager.Instance?.ShowStatPopup(loser.id, "만족도 -10", new Color(0.4f, 0.6f, 1f));
                float b = DevelopmentManager.Instance.GetLeaderBonusByRole(loser.role);
                float d = Mathf.Max(1f, b * 0.1f);
                DevelopmentPanelUI.Instance.AddValues(
                    loser.role == EmployeeRole.Planner    ? -d : 0f,
                    loser.role == EmployeeRole.Programmer ? -d : 0f,
                    loser.role == EmployeeRole.Artist     ? -d : 0f,
                    0f, 0f);
            };
        };

        return evt;
    }
}
