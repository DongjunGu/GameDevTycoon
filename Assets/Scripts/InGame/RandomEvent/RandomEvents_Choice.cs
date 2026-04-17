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
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────
    static void Apply(RandomEventChoiceData data,
                      Dictionary<string, RandomEventChoiceChartRow> chart)
    {
        RandomEventChoiceChartLoader.Apply(data, data.type.ToString(), chart);
    }
}
