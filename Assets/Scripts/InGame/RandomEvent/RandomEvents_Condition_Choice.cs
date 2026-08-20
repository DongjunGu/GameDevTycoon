using System.Collections.Generic;
using UnityEngine;

public static class RandomEvents_Condition_Choice
{
    static Dictionary<string, RandomEventChoiceChartRow> Chart =>
        RandomEventChoiceChartLoader.Cache;

    // ── 커피가 필요해 ─────────────────────────────────────────────
    // 발동 조건: 커피 아이템 획득 시 2~4주 뒤 1회
    // 대사1/대사2 = 랜덤 50/50 오프닝(둘 중 하나만 타이핑), 질문 표시 후 선택지.
    // 선택지1(준다) — 답변/결과팝업이 오프닝(대사1↔대사2)에 따라 페어링되고, 그와 별개로
    // 개발 중(Developing)이면 성공 70% / 실패(커피 쏟음) 30% 확률 분기(개발 중 아니면 항상 성공).
    // 선택지2(안준다) — 답변만 오프닝에 따라 페어링, 결과팝업은 고정.
    // [CDN fallback]
    // title:                   "커피가 필요해"
    // description:             "사장님... 이제 한계입니다... 커피를 주세요"
    // description2:            "사장님... 지금 딱 졸음이 쏟아지는 시간이라 그런데, 혹시 남는 커피 있으신가요?"
    // question:                "직원에게 커피를?"
    // choice1_label:           "준다"
    // choice1_resultDescription:  "오늘따라 커피를 먹으니 더 힘이나네요!"        (대사1 페어)
    // choice1_resultDescription2: "후우... 이제야 좀 살 것 같네요."             (대사2 페어)
    // choice1_resultDescription3: "으아아악 커피를 쏟았는데 화면이 나가버렸어요. 저장도 안 했는데..." (실패, 오프닝 무관)
    // choice1_ment1/2 (성공):      "커피(아이템) -1" / "{직원이름} 능력치 +10%"
    // choice1_resultTitle/systemMessage2 (실패 ment1/2 재사용): "커피를 먹지도 못하고 다 쏟아버렸습니다" / "개발 기간 +{주수}주"
    // choice2_label:           "안준다"
    // choice2_resultDescription:  "궁시렁 궁시렁 궁시렁 궁시렁"   (대사1 페어)
    // choice2_resultDescription2: "너무하다 너무해 진짜"          (대사2 페어)
    // choice2_ment1/2:            "커피에 미련이 남은듯 계속 쳐다봅니다" / "{직원이름} 만족도 - 10"
    public static void TriggerCoffeeRequestEvent()
    {
        if (ItemManager.Instance == null) return;
        if (ItemManager.Instance.GetCount("coffee") <= 0) return;

        var employees = EmployeeManager.Instance?.ownedEmployees;
        if (employees == null || employees.Count == 0) return;

        EmployeeData targetEmp = null;
        RandomEventChoiceData evt = null;
        bool[] usedDescB  = { false }; // 오프닝으로 대사2가 선택됐는지 — 답변 페어링에 사용
        int[]  delayWeeks = { 1 };

        evt = new RandomEventChoiceData
        {
            type                  = RandomEventType.CoffeeRequest,
            requiresPatrol        = true,
            requiredPatrolPointId = "master_desk",
            choices = new List<RandomEventChoiceOption>
            {
                new RandomEventChoiceOption(),
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        if (targetEmp == null) return;
                        int before = targetEmp.satisfaction;
                        targetEmp.ChangeSatisfaction(-10);
                        EmployeeManager.Instance.UpdateEmployee(targetEmp);
                        InfoFeedUI.Instance?.ShowSatisfaction(targetEmp, targetEmp.satisfaction - before);
                    }
                }
            },
            onSetup = () => { }
        };

        // Apply 후 텍스트 캡처
        RandomEventChoiceChartLoader.Apply(evt, "CoffeeRequest", Chart);

        RandomEventChoiceChartRow coffeeRow = null;
        Chart?.TryGetValue("CoffeeRequest", out coffeeRow);
        var c1Row = coffeeRow?.choices.Count > 0 ? coffeeRow.choices[0] : null;
        var c2Row = coffeeRow?.choices.Count > 1 ? coffeeRow.choices[1] : null;

        // [CDN fallback] "사장님... 이제 한계입니다... 커피를 주세요"
        string descA = coffeeRow?.description  ?? "";
        // [CDN fallback] "사장님... 지금 딱 졸음이 쏟아지는 시간이라 그런데, 혹시 남는 커피 있으신가요?"
        string descB = coffeeRow?.description2 ?? "";

        // choice1(준다) — 성공(대사1/대사2 페어) / 실패(단일) 답변 템플릿
        // [CDN fallback] "오늘따라 커피를 먹으니 더 힘이나네요!"
        string okReplyA   = c1Row?.resultDescription  ?? "";
        // [CDN fallback] "후우... 이제야 좀 살 것 같네요."
        string okReplyB   = c1Row?.resultDescription2 ?? "";
        // [CDN fallback] "으아아악 커피를 쏟았는데 화면이 나가버렸어요. 저장도 안 했는데..."
        string spillReply = c1Row?.resultDescription3 ?? "";
        // 성공 결과팝업(AlertUI4) — [CDN fallback] "커피(아이템) -1" / "{직원이름} 능력치 +10%"
        string okMent1Tpl = c1Row?.ment1 ?? "";
        string okMent2Tpl = c1Row?.ment2 ?? "";
        // 실패 결과팝업(AlertUI6, resultTitle/systemMessage2 컬럼 재사용) —
        // [CDN fallback] "커피를 먹지도 못하고 다 쏟아버렸습니다" / "개발 기간 +{주수}주"
        string spillMent1Tpl = c1Row?.resultTitle          ?? "";
        string spillMent2Tpl = c1Row?.resultSystemMessage2 ?? "";

        // choice2(안준다) — 답변만 대사1/대사2 페어, 결과팝업은 고정(Apply()가 이미 채워둠).
        // [CDN fallback] "궁시렁 궁시렁 궁시렁 궁시렁"
        string declineReplyA = c2Row?.resultDescription  ?? "";
        // [CDN fallback] "너무하다 너무해 진짜"
        string declineReplyB = c2Row?.resultDescription2 ?? "";
        // [CDN fallback] "{직원이름} 만족도 - 10"
        string declineMent2Tpl = evt.choices[1].resultMent2 ?? "";

        evt.onSetup = () =>
        {
            var emps = EmployeeManager.Instance?.ownedEmployees;
            if (emps == null || emps.Count == 0) { evt.cancelled = true; return; }
            if (ItemManager.Instance.GetCount("coffee") <= 0) { evt.cancelled = true; return; }

            // 파견중 직원이 뽑히면 master_desk 로 강제이동이 no-op → _pendingChoiceEvent 가 영구히 안 풀려
            // 다른 랜덤이벤트 전체가 막힘(RandomEventManager.IsTargetDispatched 가드와 동일한 이유). 후보에서 제외.
            var candidates = emps.FindAll(e => DispatchManager.Instance == null || !DispatchManager.Instance.IsDispatched(e.id));
            if (candidates.Count == 0) { evt.cancelled = true; return; }

            targetEmp = candidates[Random.Range(0, candidates.Count)];
            evt.portraitId       = targetEmp.portraitId;
            evt.targetEmployeeId = targetEmp.id;

            usedDescB[0] = !string.IsNullOrEmpty(descB) && Random.value >= 0.5f;
            evt.description = usedDescB[0] ? descB : descA;

            delayWeeks[0] = ProjectSetupUI.SelectedScale switch
            {
                ProjectScale.Small  => 1,
                ProjectScale.Medium => 2,
                _                   => 3
            };

            bool isDeveloping = DevelopmentManager.Instance?.CurrentStage == ProjectStage.Developing;

            // onChoose 는 UI가 reply1 을 읽기 전에 먼저 호출되므로, 여기서 성공/실패를 굴리고 reply1/ment 를
            // 확정해도 타이핑에 정확히 반영된다.
            evt.choices[0].onChoose = () =>
            {
                if (targetEmp == null) return;
                ItemManager.Instance.UseItemDirect("coffee");

                bool isSpill = isDeveloping && Random.value >= 0.7f; // 개발 중일 때만 30% 실패, 아니면 항상 성공
                var c1 = evt.choices[0];

                if (isSpill)
                {
                    c1.reply1          = spillReply;
                    c1.resultPopupType = 3;
                    c1.resultMent1     = spillMent1Tpl;
                    c1.resultMent2     = spillMent2Tpl.Replace("{주수}", delayWeeks[0].ToString());
                    DevelopmentManager.Instance?.ExtendDevelopmentDuration(
                        delayWeeks[0] * GetSecondsPerWeek(),
                        delayWeeks[0] * 2f * GetSecondsPerWeek()); // 연장 N주 / 감속 2N주
                    InfoFeedUI.Instance?.ShowDevelopmentDelay(targetEmp, delayWeeks[0]);
                }
                else
                {
                    c1.reply1          = usedDescB[0] ? okReplyB : okReplyA;
                    c1.resultPopupType = 1;
                    c1.resultMent1     = okMent1Tpl;
                    c1.resultMent2     = okMent2Tpl.Replace("{직원이름}", targetEmp.employeeName);

                    int buffWeeks = RandomEvents_Choice.RandomStatBuffWeeksByStage();
                    targetEmp.ApplyStatBuff(buffWeeks, 10);

                    // 커피 아이템 본연의 효과(만족도, Item_Chart "coffee" effectValue) 적용 — 이전엔 이벤트 전용
                    // 능력치 버프만 적용되고 아이템 자체의 만족도 효과가 누락돼 있었음.
                    int coffeeSat = ItemChartLoader.Cache.TryGetValue("coffee", out var coffeeRow2) ? coffeeRow2.effectValue : 15;
                    int coffeeSatBefore = targetEmp.satisfaction;
                    targetEmp.ChangeSatisfaction(coffeeSat);

                    EmployeeManager.Instance.UpdateEmployee(targetEmp);
                    InfoFeedUI.Instance?.ShowStatBuff(targetEmp, buffWeeks, 10, true);
                    InfoFeedUI.Instance?.ShowSatisfaction(targetEmp, targetEmp.satisfaction - coffeeSatBefore);
                }
                ItemPanelUI.Instance?.Refresh();
                GameTimeManager.Instance?.SaveGameTime();
            };

            evt.choices[1].reply1 = usedDescB[0] ? declineReplyB : declineReplyA;
            if (!string.IsNullOrEmpty(declineMent2Tpl))
                evt.choices[1].resultMent2 = declineMent2Tpl.Replace("{직원이름}", targetEmp.employeeName);
        };

        RandomEventManager.Instance?.TriggerConditionChoiceEvent(evt);
    }

    // ── 에너지 드링크는 내꺼야 ───────────────────────────────────────
    // 발동 조건: 에너지드링크 아이템 획득 시 2~4주 뒤 1회
    // CoffeeRequest와 달리 확률 분기 없음 — 항상 성공. 대사1/대사2 랜덤 오프닝, 질문 표시 후 선택지.
    // [CDN fallback]
    // title:                "에너지 드링크는 내꺼야"
    // description:          "사장님 어제 에너지 드링크가 있는 거 봤는데.. 저한테 투자하시죠!"
    // description2:         "사장님... 혹시 바쁘신데 죄송하지만, 그 에너지 드링크 제가 좀 마셔도 될까요?"
    // question:             "직원에게 에너지드링크를?"
    // choice1_label:        "준다"
    // choice1_resultDescription: "크으, 이 맛이죠! 사장님, 역시 제 마음을 아시는 건 사장님뿐입니다."
    // choice1_ment1/2:      "에너지드링크(아이템) -1" / "{직원이름} 능력치 +10%"
    // choice2_label:        "안준다"
    // choice2_resultDescription: "와... 사장님 정말 냉정하시네요. 눈앞에서 사람이 쓰러져 가는데..."
    // choice2_ment1/2:      "에너지 드링크에 미련이 남은듯 계속 쳐다봅니다" / "{직원이름} 만족도 - 10"
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
                        int buffWeeks = RandomEvents_Choice.RandomStatBuffWeeksByStage();
                        targetEmp.ApplyStatBuff(buffWeeks, 10);

                        // 에너지드링크 아이템 본연의 효과(만족도, Item_Chart "energyDrink" effectValue) 적용
                        // — CoffeeRequest 와 동일한 누락(능력치 버프만 적용되고 아이템 자체 효과 누락)을 여기도 수정.
                        int drinkSat = ItemChartLoader.Cache.TryGetValue("energyDrink", out var drinkRow) ? drinkRow.effectValue : 25;
                        int drinkSatBefore = targetEmp.satisfaction;
                        targetEmp.ChangeSatisfaction(drinkSat);

                        EmployeeManager.Instance.UpdateEmployee(targetEmp);
                        InfoFeedUI.Instance?.ShowStatBuff(targetEmp, buffWeeks, 10, true);
                        InfoFeedUI.Instance?.ShowSatisfaction(targetEmp, targetEmp.satisfaction - drinkSatBefore);
                        ItemPanelUI.Instance?.Refresh();
                    }
                },
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        if (targetEmp == null) return;
                        int before = targetEmp.satisfaction;
                        targetEmp.ChangeSatisfaction(-10);
                        EmployeeManager.Instance.UpdateEmployee(targetEmp);
                        InfoFeedUI.Instance?.ShowSatisfaction(targetEmp, targetEmp.satisfaction - before);
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
        string descA = edRow?.description  ?? "";
        // [CDN fallback] "사장님... 혹시 바쁘신데 죄송하지만, 그 에너지 드링크 제가 좀 마셔도 될까요?"
        string descB = edRow?.description2 ?? "";
        // [CDN fallback] "{직원이름} 능력치 +10%"
        string c1Ment2Tpl = evt.choices[0].resultMent2 ?? "";
        // [CDN fallback] "{직원이름} 만족도 - 10"
        string c2Ment2Tpl = evt.choices[1].resultMent2 ?? "";

        evt.onSetup = () =>
        {
            var emps = EmployeeManager.Instance?.ownedEmployees;
            if (emps == null || emps.Count == 0) { evt.cancelled = true; return; }
            if (ItemManager.Instance.GetCount("energyDrink") <= 0) { evt.cancelled = true; return; }

            // 파견중 직원이 뽑히면 master_desk 로 강제이동이 no-op → _pendingChoiceEvent 가 영구히 안 풀려
            // 다른 랜덤이벤트 전체가 막힘(RandomEventManager.IsTargetDispatched 가드와 동일한 이유). 후보에서 제외.
            var candidates = emps.FindAll(e => DispatchManager.Instance == null || !DispatchManager.Instance.IsDispatched(e.id));
            if (candidates.Count == 0) { evt.cancelled = true; return; }

            targetEmp = candidates[Random.Range(0, candidates.Count)];
            evt.portraitId       = targetEmp.portraitId;
            evt.targetEmployeeId = targetEmp.id;

            evt.description = (!string.IsNullOrEmpty(descB) && Random.value >= 0.5f) ? descB : descA;

            if (!string.IsNullOrEmpty(c1Ment2Tpl))
                evt.choices[0].resultMent2 = c1Ment2Tpl.Replace("{직원이름}", targetEmp.employeeName);
            if (!string.IsNullOrEmpty(c2Ment2Tpl))
                evt.choices[1].resultMent2 = c2Ment2Tpl.Replace("{직원이름}", targetEmp.employeeName);
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

        // 과거 프로젝트가 없으면 baseStat 기준이 없어 임계값(=baseStat+5)이 의미 없음 → 이벤트 자체 스킵
        var history = CompletedProjectManager.Instance?.completedProjects;
        if (history == null || history.Count == 0)
        {
            Debug.Log("[투자] 과거 프로젝트 없음 → 투자 이벤트 스킵");
            onComplete?.Invoke();
            return;
        }

        var statDefs = new (string key, string name)[]
        {
            ("planning", "기획"),
            ("develop",  "개발"),
            ("art",      "아트"),
        };
        int idx = Random.Range(0, statDefs.Length);
        mgr.InvestmentStat     = statDefs[idx].key;
        mgr.InvestmentStatName = statDefs[idx].name;

        var last = history[history.Count - 1];
        float baseStat = mgr.InvestmentStat switch
        {
            "planning" => last.planning,
            "develop"  => last.develop,
            "art"      => last.art,
            _ => 0f
        };
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
        // [CDN fallback] 결과팝업(AlertUI6) — 답변1/멘트1 = "{파트} 점수가 {임계값}을 꼭 달성해서 능력을 보여주세요!" / 멘트2 = "+총 연봉 *10% G"
        string c1Reply1Tpl = evt.choices[0].reply1      ?? "";
        string c1Ment1Tpl  = evt.choices[0].resultMent1 ?? "";
        string c1Ment2Tpl  = evt.choices[0].resultMent2 ?? "";

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

            evt.choices[0].reply1      = c1Reply1Tpl.Replace("{파트}", partName).Replace("{임계값}", threshold);
            evt.choices[0].resultMent1 = c1Ment1Tpl.Replace("{파트}", partName).Replace("{임계값}", threshold);
            evt.choices[0].resultMent2 = c1Ment2Tpl;
        };

        evt.onSetup.Invoke();
        RandomEventChoiceUI.Instance?.Show(evt);
    }

    static float GetSecondsPerWeek() =>
        ProjectSetupUI.SelectedScale switch
        {
            ProjectScale.Small  => 80f / 16f, // 5.0초/주
            ProjectScale.Medium => 80f / 24f, // 3.33초/주
            _                   => 80f / 32f  // 2.5초/주 (Large)
        };
}
