using System.Collections.Generic;
using UnityEngine;

public static class RandomEvents_Condition_Choice
{
    static Dictionary<string, RandomEventChoiceChartRow> Chart =>
        RandomEventChoiceChartLoader.Cache;

    // ── 커피가 필요해 ─────────────────────────────────────────────
    // 발동 조건: 커피 아이템 획득 시 2~4주 뒤 1회
    // [CDN fallback]
    // title:                   "커피가 필요해"
    // description:             "사장님… 이제 한계입니다… 커피를 주세요"
    // description2:            "사장님... 지금 딱 졸음이 쏟아지는 시간이라 그런데, 혹시 남는 커피 있으신가요?"
    // choice1_label:           "수락"
    // choice1_resultDesc1:     "오늘따라 커피를 먹으니 더 힘이나네요!"
    // choice1_resultDesc2:     "후우... 이제야 좀 살 것 같네요."
    // choice1_resultDesc3:     "으아아악 커피를 쏟았는데 화면이 나가버렸습니다. 저장도 안 했는데..."
    // choice1_systemMessage:   "커피가 사용됩니다. {직원이름} 능력치 +20%"
    // choice1_systemMessage2:  "개발 기간 {주수}주 지연"
    // choice2_label:           "거절"
    // choice2_resultDesc1:     "궁시렁 궁시렁 궁시렁 궁시렁"
    // choice2_resultDesc2:     "너무하다 너무해 진짜"
    // choice2_systemMessage:   "{직원이름} 만족도 -10"
    public static void TriggerCoffeeRequestEvent()
    {
        if (ItemManager.Instance == null) return;
        if (ItemManager.Instance.GetCount("coffee") <= 0) return;

        var employees = EmployeeManager.Instance?.ownedEmployees;
        if (employees == null || employees.Count == 0) return;

        EmployeeData targetEmp = null;
        RandomEventChoiceData evt = null;
        bool[] isSpill    = { false };
        int[]  delayWeeks = { 1 };

        evt = new RandomEventChoiceData
        {
            type                  = RandomEventType.CoffeeRequest,
            requiresPatrol        = true,
            requiredPatrolPointId = "master_desk",
            choices = new List<RandomEventChoiceOption>
            {
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        if (targetEmp == null) return;
                        ItemManager.Instance.UseItemDirect("coffee");
                        if (isSpill[0])
                        {
                            DevelopmentManager.Instance?.ExtendDevelopmentDuration(
                                delayWeeks[0] * 2f * GetSecondsPerWeek());
                        }
                        else
                        {
                            targetEmp.ApplyStatBuff(Random.Range(4, 9));
                            EmployeeManager.Instance.UpdateEmployee(targetEmp);
                            OfficeManager.Instance?.ShowStatPopup(targetEmp.id, "능력치 +20%", new Color(1f, 0.4f, 0.4f));
                        }
                        ItemPanelUI.Instance?.Refresh();
                        GameTimeManager.Instance?.SaveGameTime();
                    }
                },
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        if (targetEmp == null) return;
                        targetEmp.ChangeSatisfaction(-10);
                        EmployeeManager.Instance.UpdateEmployee(targetEmp);
                        OfficeManager.Instance?.ShowStatPopup(targetEmp.id, "만족도 -10", new Color(0.4f, 0.6f, 1f));
                    }
                }
            },
            onSetup = () => { }
        };

        // Apply 후 텍스트 캡처
        RandomEventChoiceChartLoader.Apply(evt, "CoffeeRequest", Chart);

        RandomEventChoiceChartRow coffeeRow = null;
        Chart?.TryGetValue("CoffeeRequest", out coffeeRow);

        // [CDN fallback] "사장님… 이제 한계입니다… 커피를 주세요"
        string descA      = coffeeRow?.description  ?? "";
        // [CDN fallback] "사장님... 지금 딱 졸음이 쏟아지는 시간이라 그런데, 혹시 남는 커피 있으신가요?"
        string descB      = coffeeRow?.description2 ?? "";
        // [CDN fallback] "오늘따라 커피를 먹으니 더 힘이나네요!"
        string okDesc1    = evt.choices[0].resultDescriptions.Count > 0 ? evt.choices[0].resultDescriptions[0] : "";
        // [CDN fallback] "후우... 이제야 좀 살 것 같네요."
        string okDesc2    = evt.choices[0].resultDescriptions.Count > 1 ? evt.choices[0].resultDescriptions[1] : "";
        // [CDN fallback] "으아아악 커피를 쏟았는데 화면이 나가버렸습니다. 저장도 안 했는데..."
        string spillDesc  = evt.choices[0].resultDescriptions.Count > 2 ? evt.choices[0].resultDescriptions[2] : "";
        // [CDN fallback] "커피가 사용됩니다. {직원이름} 능력치 +20%"
        string okSys      = evt.choices[0].resultSystemMessage ?? "";
        // [CDN fallback] "개발 기간 {주수}주 지연"
        string spillSysTpl= coffeeRow?.choices.Count > 0 ? coffeeRow.choices[0].resultSystemMessage2 : "";
        // [CDN fallback] "궁시렁 궁시렁 궁시렁 궁시렁"
        string c2Desc1    = evt.choices[1].resultDescriptions.Count > 0 ? evt.choices[1].resultDescriptions[0] : "";
        // [CDN fallback] "너무하다 너무해 진짜"
        string c2Desc2    = evt.choices[1].resultDescriptions.Count > 1 ? evt.choices[1].resultDescriptions[1] : "";
        // [CDN fallback] "{직원이름} 만족도 -10"
        string c2Sys      = evt.choices[1].resultSystemMessage ?? "";

        evt.onSetup = () =>
        {
            var emps = EmployeeManager.Instance?.ownedEmployees;
            if (emps == null || emps.Count == 0) { evt.cancelled = true; return; }
            if (ItemManager.Instance.GetCount("coffee") <= 0) { evt.cancelled = true; return; }

            targetEmp = emps[Random.Range(0, emps.Count)];
            evt.portraitId       = targetEmp.portraitId;
            evt.targetEmployeeId = targetEmp.id;

            evt.description = (!string.IsNullOrEmpty(descB) && Random.value >= 0.5f) ? descB : descA;

            bool isDeveloping = DevelopmentManager.Instance?.CurrentStage == ProjectStage.Developing;
            isSpill[0] = isDeveloping && Random.value >= 0.7f;

            delayWeeks[0] = ProjectSetupUI.SelectedScale switch
            {
                ProjectScale.Small  => 1,
                ProjectScale.Medium => 2,
                _                   => 3
            };

            if (isSpill[0])
            {
                if (!string.IsNullOrEmpty(spillDesc))
                    evt.choices[0].resultDescriptions  = new List<string> { spillDesc };
                if (!string.IsNullOrEmpty(spillSysTpl))
                    evt.choices[0].resultSystemMessage = spillSysTpl.Replace("{주수}", delayWeeks[0].ToString());
            }
            else
            {
                var okDescs = new List<string>();
                if (!string.IsNullOrEmpty(okDesc1)) okDescs.Add(okDesc1);
                if (!string.IsNullOrEmpty(okDesc2)) okDescs.Add(okDesc2);
                if (okDescs.Count > 0) evt.choices[0].resultDescriptions = okDescs;
                if (!string.IsNullOrEmpty(okSys))
                    evt.choices[0].resultSystemMessage = okSys.Replace("{직원이름}", targetEmp.employeeName);
            }

            var c2Descs = new List<string>();
            if (!string.IsNullOrEmpty(c2Desc1)) c2Descs.Add(c2Desc1);
            if (!string.IsNullOrEmpty(c2Desc2)) c2Descs.Add(c2Desc2);
            if (c2Descs.Count > 0) evt.choices[1].resultDescriptions = c2Descs;
            if (!string.IsNullOrEmpty(c2Sys))
                evt.choices[1].resultSystemMessage = c2Sys.Replace("{직원이름}", targetEmp.employeeName);
        };

        RandomEventManager.Instance?.TriggerConditionChoiceEvent(evt);
    }

    // ── 에너지 드링크는 내꺼야 ───────────────────────────────────────
    // 발동 조건: 에너지드링크 아이템 획득 시 2~4주 뒤 1회
    // [CDN fallback]
    // title:                "에너지 드링크는 내꺼야"
    // description:          "사장님 어제 에너지 드링크가 있는 거 봤는데.. 저한테 투자하시죠!"
    // description2:         "사장님... 혹시 바쁘신데 죄송하지만, 그 에너지 드링크 제가 좀 마셔도 될까요?"
    // choice1_label:        "수락"
    // choice1_resultDesc:   "크으, 이 맛이죠! 사장님, 역시 제 마음을 아시는 건 사장님뿐입니다."
    // choice1_systemMessage:"에너지 드링크가 사용됩니다. {직원이름} 능력치 +20%"
    // choice2_label:        "거절"
    // choice2_resultDesc:   "와... 사장님 정말 냉정하시네요. 눈앞에서 사람이 쓰러져 가는데..."
    // choice2_systemMessage:"{직원이름} 만족도 -10"
    public static void TriggerEnergyDrinkRequestEvent()
    {
        if (ItemManager.Instance == null) return;
        if (ItemManager.Instance.GetCount("energyDrink") <= 0) return;

        var employees = EmployeeManager.Instance?.ownedEmployees;
        if (employees == null || employees.Count == 0) return;

        EmployeeData targetEmp = null;
        RandomEventChoiceData evt = null;

        evt = new RandomEventChoiceData
        {
            type                  = RandomEventType.EnergyDrinkRequest,
            requiresPatrol        = true,
            requiredPatrolPointId = "master_desk",
            choices = new List<RandomEventChoiceOption>
            {
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        if (targetEmp == null) return;
                        ItemManager.Instance.UseItemDirect("energyDrink");
                        targetEmp.ApplyStatBuff(Random.Range(4, 9));
                        EmployeeManager.Instance.UpdateEmployee(targetEmp);
                        OfficeManager.Instance?.ShowStatPopup(targetEmp.id, "능력치 +20%", new Color(1f, 0.4f, 0.4f));
                        ItemPanelUI.Instance?.Refresh();
                    }
                },
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        if (targetEmp == null) return;
                        targetEmp.ChangeSatisfaction(-10);
                        EmployeeManager.Instance.UpdateEmployee(targetEmp);
                        OfficeManager.Instance?.ShowStatPopup(targetEmp.id, "만족도 -10", new Color(0.4f, 0.6f, 1f));
                    }
                }
            },
            onSetup = () => { }
        };

        // Apply 후 텍스트 캡처
        RandomEventChoiceChartLoader.Apply(evt, "EnergyDrinkRequest", Chart);

        RandomEventChoiceChartRow edRow = null;
        Chart?.TryGetValue("EnergyDrinkRequest", out edRow);

        // [CDN fallback] "사장님 어제 에너지 드링크가 있는 거 봤는데.. 저한테 투자하시죠!"
        string descA  = edRow?.description  ?? "";
        // [CDN fallback] "사장님... 혹시 바쁘신데 죄송하지만, 그 에너지 드링크 제가 좀 마셔도 될까요?"
        string descB  = edRow?.description2 ?? "";
        // [CDN fallback] "크으, 이 맛이죠! 사장님, 역시 제 마음을 아시는 건 사장님뿐입니다."
        string c1Desc = evt.choices[0].resultDescriptions.Count > 0 ? evt.choices[0].resultDescriptions[0] : "";
        // [CDN fallback] "에너지 드링크가 사용됩니다. {직원이름} 능력치 +20%"
        string c1Sys  = evt.choices[0].resultSystemMessage ?? "";
        // [CDN fallback] "와... 사장님 정말 냉정하시네요. 눈앞에서 사람이 쓰러져 가는데..."
        string c2Desc = evt.choices[1].resultDescriptions.Count > 0 ? evt.choices[1].resultDescriptions[0] : "";
        // [CDN fallback] "{직원이름} 만족도 -10"
        string c2Sys  = evt.choices[1].resultSystemMessage ?? "";

        evt.onSetup = () =>
        {
            var emps = EmployeeManager.Instance?.ownedEmployees;
            if (emps == null || emps.Count == 0) { evt.cancelled = true; return; }
            if (ItemManager.Instance.GetCount("energyDrink") <= 0) { evt.cancelled = true; return; }

            targetEmp = emps[Random.Range(0, emps.Count)];
            evt.portraitId       = targetEmp.portraitId;
            evt.targetEmployeeId = targetEmp.id;

            evt.description = (!string.IsNullOrEmpty(descB) && Random.value >= 0.5f) ? descB : descA;

            if (!string.IsNullOrEmpty(c1Desc))
                evt.choices[0].resultDescriptions  = new List<string> { c1Desc };
            if (!string.IsNullOrEmpty(c1Sys))
                evt.choices[0].resultSystemMessage = c1Sys.Replace("{직원이름}", targetEmp.employeeName);

            if (!string.IsNullOrEmpty(c2Desc))
                evt.choices[1].resultDescriptions  = new List<string> { c2Desc };
            if (!string.IsNullOrEmpty(c2Sys))
                evt.choices[1].resultSystemMessage = c2Sys.Replace("{직원이름}", targetEmp.employeeName);
        };

        RandomEventManager.Instance?.TriggerConditionChoiceEvent(evt);
    }

    // ── 의문의 투자 제안 ──────────────────────────────────────────
    // 발동 조건: 개발 시작 전 (DevelopmentManager.ProceedToInvestment → investmentTriggerChance 체크 후)
    // [CDN fallback]
    // title:              "의문의 투자 제안"
    // description:        "사장님 투자 제안이 들어왔어요!\n{파트} 수치가 {임계값} 이상이면 돈을 투자해 주지만 기준 미달이면 위약금까지 내야한다고 하네요 수락하시겠어요?"
    // choice1_label:      "수락"
    // choice1_resultDesc: "{파트} 점수가 {임계값}을 꼭 달성해서 능력을 보여주세요!"
    // choice2_label:      "거절"
    // choice2_resultDesc: "거절도 좋은 선택이라고 생각합니다 사장님!"
    public static void TriggerInvestmentEvent(System.Action onComplete)
    {
        var mgr = RandomEventManager.Instance;
        if (mgr == null) { onComplete?.Invoke(); return; }

        var statDefs = new (string key, string name)[]
        {
            ("planning", "기획"),
            ("develop",  "개발"),
            ("art",      "아트"),
        };
        int idx = Random.Range(0, statDefs.Length);
        mgr.InvestmentStat     = statDefs[idx].key;
        mgr.InvestmentStatName = statDefs[idx].name;

        float baseStat = 0f;
        var history = CompletedProjectManager.Instance?.completedProjects;
        if (history != null && history.Count > 0)
        {
            var last = history[history.Count - 1];
            baseStat = mgr.InvestmentStat switch
            {
                "planning" => last.planning,
                "develop"  => last.develop,
                "art"      => last.art,
                _ => 0f
            };
        }
        mgr.InvestmentThreshold = baseStat + 5f;
        mgr.InvestmentReward = Mathf.Max(100,
            Mathf.RoundToInt(EmployeeManager.Instance.GetTotalSalary() * 0.1f));

        // UI 표시 전 저장 — 재시작 시 RandomEventChoiceUI 복원용
        var dm = DevelopmentManager.Instance;
        dm.CurrentStage = ProjectStage.Developing;
        mgr.PendingInvestmentUI = true;
        ProjectSaveManager.Instance.SaveProject();

        ShowInvestmentEventUI(onComplete);
    }

    // 재시작 복원 시 투자 이벤트 UI 재표시 (mgr의 저장된 Stat/Threshold/Reward 사용)
    public static void RestoreInvestmentUI(System.Action onComplete)
    {
        var mgr = RandomEventManager.Instance;
        if (mgr == null) { onComplete?.Invoke(); return; }
        ShowInvestmentEventUI(onComplete);
    }

    static void ShowInvestmentEventUI(System.Action onComplete)
    {
        var mgr = RandomEventManager.Instance;

        var evt = new RandomEventChoiceData
        {
            type           = RandomEventType.Investment,
            portraitId     = "portrait_secretary",
            requiresPatrol = false,
            onConfirm      = () =>
            {
                mgr.PendingInvestmentUI = false;
                onComplete?.Invoke();
            },
            choices = new List<RandomEventChoiceOption>
            {
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        mgr.InvestmentAccepted = true;
                        InvestmentProgressUI.Instance?.Show(mgr.InvestmentStatName, mgr.InvestmentThreshold);
                    }
                },
                new RandomEventChoiceOption { onChoose = () => { } }
            },
            onSetup = () => { }
        };

        RandomEventChoiceChartLoader.Apply(evt, "Investment", Chart);

        RandomEventChoiceChartRow invRow = null;
        Chart?.TryGetValue("Investment", out invRow);

        // [CDN fallback] "사장님 투자 제안이 들어왔어요!\n{파트} 수치가 {임계값} 이상이면 돈을 투자해 주지만 기준 미달이면 위약금까지 내야한다고 하네요 수락하시겠어요?"
        string descTpl = invRow?.description ?? "";
        // [CDN fallback] "{파트} 점수가 {임계값}을 꼭 달성해서 능력을 보여주세요!"
        string c1Desc  = evt.choices[0].resultDescriptions.Count > 0 ? evt.choices[0].resultDescriptions[0] : "";
        // [CDN fallback] "거절도 좋은 선택이라고 생각합니다 사장님!"
        string c2Desc  = evt.choices[1].resultDescriptions.Count > 0 ? evt.choices[1].resultDescriptions[0] : "";

        evt.onSetup = () =>
        {
            string partName  = mgr.InvestmentStatName;
            string threshold = mgr.InvestmentThreshold.ToString("F0");

            evt.description = descTpl
                .Replace("{파트}", partName)
                .Replace("{임계값}", threshold);

            if (!string.IsNullOrEmpty(c1Desc))
                evt.choices[0].resultDescriptions = new List<string>
                {
                    c1Desc.Replace("{파트}", partName).Replace("{임계값}", threshold)
                };
            if (!string.IsNullOrEmpty(c2Desc))
                evt.choices[1].resultDescriptions = new List<string> { c2Desc };
        };

        evt.onSetup.Invoke();
        RandomEventChoiceUI.Instance?.Show(evt);
    }

    static float GetSecondsPerWeek() =>
        ProjectSetupUI.SelectedScale switch
        {
            ProjectScale.Small  => 5f,
            ProjectScale.Medium => 4.2f,
            _                   => 3.9f
        };
}
