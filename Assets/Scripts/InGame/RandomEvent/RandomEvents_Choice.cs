using System.Collections.Generic;
using UnityEngine;

public static class RandomEvents_Choice
{
    public static void Register(List<RandomEventChoiceData> pool, RandomEventManager mgr,
                                Dictionary<string, RandomEventChoiceChartRow> chart = null)
    {
        // ── 생일 ─────────────────────────────────────────────────
        // [CDN fallback]
        // title:   "생일"
        // desc:    "사장님 오늘 저희 집 강아지 생일이에요!"
        // choice1: label="음...그래서?"
        //          result="흥! 제 가족같은 강아지를 안챙겨주다니 너무하네요."
        //          sys="{해당직원이름} 만족도 -5"
        // choice2: label="직원들 강아지 생일까지 챙겨야 하는건가..."
        //          result="와 사장님 감동이에요!"
        //          sys="{해당직원이름} 만족도 +5 / -{비용}G"
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
                            int before = emp.satisfaction;
                            emp.ChangeSatisfaction(-5);
                            InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
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
                            int goldAfter = MoneyManager.Instance.Gold - cost;
                            MoneyManager.Instance.ForceSpendGold(cost, saveImmediately: false);
                            int birthdayBefore = emp.satisfaction;
                            emp.ChangeSatisfaction(10);
                            InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - birthdayBefore);
                            if (goldAfter < 0) GameTimeManager.Instance?.TriggerBankruptcy();
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

                    int cost = Mathf.Max(1, (int)(EmployeeManager.Instance.GetTotalSalary() * 0.03f));
                    // 결과 팝업 전용(코인 아이콘 인라인 + 노란색) — 버튼 라벨에는 안 붙임, 대신 ConditionText로.
                    string costTagResult = $"<sprite=\"CoinSpriteAsset\" name=\"coin\"> <color=#F3C01D>(-{cost} G)</color>";
                    birthdayEvt.choices[1].buttonLabel =
                        birthdayEvt.choices[1].buttonLabel?.Replace("\n(-{N}G)", "").Replace("(-{N}G)", "");
                    birthdayEvt.choices[1].conditionText = $"자금 -{cost:N0} G";

                    var c0 = birthdayEvt.choices[0];
                    c0.reply1     = c0.reply1?.Replace("{해당직원이름}", birthdayEmp.employeeName);
                    c0.resultMent1 = c0.resultMent1?.Replace("{해당직원이름}", birthdayEmp.employeeName);
                    c0.resultMent2 = c0.resultMent2?.Replace("{해당직원이름}", birthdayEmp.employeeName);

                    var c1 = birthdayEvt.choices[1];
                    c1.reply1     = c1.reply1?.Replace("{해당직원이름}", birthdayEmp.employeeName);
                    c1.resultMent1 = c1.resultMent1?.Replace("{해당직원이름}", birthdayEmp.employeeName);
                    c1.resultMent2 = c1.resultMent2?.Replace("{해당직원이름}", birthdayEmp.employeeName);
                    c1.resultMent3 = c1.resultMent3?.Replace("-{비용}G", costTagResult);
                }
            };
            Apply(birthdayEvt, chart);
            pool.Add(birthdayEvt);
        }

        // ── 장비 업그레이드 요청 ──────────────────────────────────
        // [CDN fallback]
        // title:       "장비 업그레이드 요청"
        // desc (기본):  "사장님 컴퓨터 새로 하나 바꿔주시죠"
        // desc2 (아티스트): "사장님 태블릿 하나 새로 사주시죠"
        // choice1: label="그래.. 알겠어 바꿔줘야지(-{N}G)"
        //          result="이정도면 확실히 업그레이드네요 기대하세요"
        //          sys="{해당직원이름} 능력치 {주수}주 동안 +20%"
        // choice2: label="지금은 예산초과야… 이번 게임 대박나면 꼭 바꿔줄게"
        //          result="이런 장비로 뭘 얼마나 잘만들겠어요~"
        {
            EmployeeData equipEmp = null;
            RandomEventChoiceData equipEvt = null;
            int equipCostSnapshot = 0;
            equipEvt = new RandomEventChoiceData
            {
                type        = RandomEventType.EquipmentUpgrade,
                weight      = 1f,
                categoryMin = 1,
                categoryMax = 4,
                requiresPatrol        = true,
                requiredPatrolPointId = "master_desk",
                choices = new List<RandomEventChoiceOption>
                {
                    // ── 선택지 1: 구매 ───────────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            var emp = EmployeeManager.Instance.GetEmployee(equipEvt.targetEmployeeId);
                            if (emp == null) return;

                            int goldAfter = MoneyManager.Instance.Gold - equipCostSnapshot;
                            MoneyManager.Instance.ForceSpendGold(equipCostSnapshot, saveImmediately: false);

                            int buffWeeks = RandomStatBuffWeeksByStage();
                            emp.ApplyStatBuff(buffWeeks, 10);

                            string weeks = buffWeeks.ToString();
                            var c0 = equipEvt.choices[0];
                            c0.resultMent2 = c0.resultMent2?.Replace("{주수}", weeks);
                            if (goldAfter < 0) GameTimeManager.Instance?.TriggerBankruptcy();
                        }
                    },
                    // ── 선택지 2: 거절 ───────────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () => { }
                    }
                }
            };
            Apply(equipEvt, chart);

            // [CDN fallback] "그래.. 알겠어 바꿔줘야지(-{N}G)"
            string equipLabel0Template  = equipEvt.choices[0].buttonLabel ?? "";

            // description2는 아티스트 전용 (chart row에서 직접 참조)
            RandomEventChoiceChartRow eqChartRow = null;
            chart?.TryGetValue("EquipmentUpgrade", out eqChartRow);
            // [CDN fallback] "사장님 컴퓨터 새로 하나 바꿔주시죠"
            string equipDescDefaultTemplate = equipEvt.description ?? "";
            // [CDN fallback] "사장님 태블릿 하나 새로 사주시죠"
            string equipDescArtistTemplate  = !string.IsNullOrEmpty(eqChartRow?.description2)
                ? eqChartRow.description2 : "";
            // 답변1(reply1) 역할 분기 — choice1_resultDescription(기본/개발)·resultDescription2(아트) 컬럼을
            // role-alt 저장용으로 재사용(자동 resultDescriptions 랜덤픽 경로는 reply1이 우선이라 사용 안 됨).
            var eqC1 = eqChartRow?.choices.Count > 0 ? eqChartRow.choices[0] : null;
            string equipReplyDefaultTemplate = eqC1?.resultDescription  ?? "";
            string equipReplyArtistTemplate  = eqC1?.resultDescription2 ?? "";
            // [CDN fallback] "{해당직원이름} 능력치 {주수}주 동안 +10%"
            string equipMent2Template = equipEvt.choices[0].resultMent2 ?? "";
            // [CDN fallback] "-{비용}G"
            string equipMent3Template = equipEvt.choices[0].resultMent3 ?? "";

            equipEvt.onSetup = () =>
            {
                var candidates = EmployeeManager.Instance.ownedEmployees
                    .FindAll(e => e.role == EmployeeRole.Planner ||
                                  e.role == EmployeeRole.Programmer ||
                                  e.role == EmployeeRole.Artist);
                if (candidates.Count == 0) { equipEvt.cancelled = true; return; }

                equipEmp = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                equipEvt.portraitId       = equipEmp.portraitId;
                equipEvt.targetEmployeeId = equipEmp.id;

                bool isArtist = equipEmp.role == EmployeeRole.Artist;

                equipEvt.description = (isArtist && !string.IsNullOrEmpty(equipDescArtistTemplate))
                    ? equipDescArtistTemplate
                    : equipDescDefaultTemplate;

                equipCostSnapshot = Mathf.Max(1, (int)(EmployeeManager.Instance.GetTotalSalary() * 0.03f));
                // 결과 팝업 전용(코인 아이콘 인라인 + 노란색) — 버튼 라벨에는 안 붙임, 대신 ConditionText로.
                string costTagResult = $"<sprite=\"CoinSpriteAsset\" name=\"coin\"> <color=#F3C01D>(-{equipCostSnapshot} G)</color>";

                equipEvt.choices[0].buttonLabel =
                    equipLabel0Template.Replace("\n(-{N}G)", "").Replace("(-{N}G)", "");
                equipEvt.choices[0].conditionText = $"자금 -{equipCostSnapshot:N0} G";
                equipEvt.choices[0].reply1 = (isArtist && !string.IsNullOrEmpty(equipReplyArtistTemplate))
                    ? equipReplyArtistTemplate
                    : equipReplyDefaultTemplate;
                equipEvt.choices[0].resultMent1 = "장비 업그레이드 완료";
                equipEvt.choices[0].resultMent2 = equipMent2Template
                    .Replace("{해당직원이름}", equipEmp.employeeName);
                equipEvt.choices[0].resultMent3 = equipMent3Template
                    .Replace("-{비용}G", costTagResult);
            };
            pool.Add(equipEvt);
        }

        // ── 오늘은 회식이다! ──────────────────────────────────────
        // [CDN fallback]
        // title: "오늘은 회식이다!"
        // desc:  "직원들이 회식을 요구하고 있어요 어떻게 할까요?"
        // portrait: "portrait_secretary"
        // choice1: label="회식은 뭔 회식이야 게임이나 만들자"
        //          result="아 네… 알겠습니다…"    sys="전 직원 만족도 -5"
        // choice2: label="오늘은 삼겹살로 가자!(-{N}G)"
        //          result_happy="와아! 삼겹살에 소주! 오늘 스트레스 다 날려버려요!"
        //          result_meh="소고기도 아니고 삼겹살이라니... 뭐 안 먹는 것보단 낫겠네요."
        //          sys_happy="전 직원 만족도 +5 / -{비용}G"    sys_meh="-{비용}G"
        // choice3: label="고생하는데 무리좀 해야지… 오늘은 소고기다(-{N}G)"
        //          result="진짜 소고기요? 꽃등심? 와... 사장님 만세!"
        //          sys="전 직원 만족도 +10 / -{비용}G"
        {
            RandomEventChoiceData dinnerEvt = null;
            int    dinnerCost5    = 0;
            int    dinnerCost10   = 0;
            string dinnerDesc2Happy = "";
            string dinnerDesc2Meh   = "";
            string dinnerHappyMent1Template = "";
            string dinnerMehMent1Template   = "";
            string dinnerMent2Template      = "";
            dinnerEvt = new RandomEventChoiceData
            {
                type        = RandomEventType.CompanyDinner,
                weight      = 1f,
                categoryMin = 1,
                categoryMax = 4,
                choices = new List<RandomEventChoiceOption>
                {
                    // ── 선택지 1: 거절 ───────────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                            EmployeeManager.Instance.ChangeAllSatisfaction(-5)
                    },
                    // ── 선택지 2: 삼겹살 (50% 확률로 만족도 +5, happy/meh 결과팝업도 함께 분기) ──
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            int goldAfter = MoneyManager.Instance.Gold - dinnerCost5;
                            MoneyManager.Instance.ForceSpendGold(dinnerCost5, saveImmediately: false);

                            bool happy = UnityEngine.Random.value < 0.5f;
                            if (happy)
                                EmployeeManager.Instance.ChangeAllSatisfaction(5);

                            var c1 = dinnerEvt.choices[1];
                            // 괄호까지 색상 태그 안에 포함 — "(-3000G)" 형태로 괄호도 같이 노란색(#F3C01D)으로. 앞에 코인 아이콘 인라인.
                            string costTag = $"<sprite=\"CoinSpriteAsset\" name=\"coin\"> <color=#F3C01D>(-{dinnerCost5} G)</color>";
                            c1.reply1         = happy ? dinnerDesc2Happy : dinnerDesc2Meh;
                            c1.resultPopupType = happy ? 1 : 3;
                            // happy 결과팝업멘트1="전 직원 만족도 +5"(choice2_ment1 원본) / meh 멘트1은
                            // choice2_resultTitle 컬럼을 재사용해 저장(dinnerMeh1Template).
                            c1.resultMent1 = (happy ? dinnerHappyMent1Template : dinnerMehMent1Template);
                            c1.resultMent2 = dinnerMent2Template.Replace("-{비용}G", costTag);
                            if (goldAfter < 0) GameTimeManager.Instance?.TriggerBankruptcy();
                        }
                    },
                    // ── 선택지 3: 소고기 ─────────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            int goldAfter = MoneyManager.Instance.Gold - dinnerCost10;
                            MoneyManager.Instance.ForceSpendGold(dinnerCost10, saveImmediately: false);
                            EmployeeManager.Instance.ChangeAllSatisfaction(10);
                            if (goldAfter < 0) GameTimeManager.Instance?.TriggerBankruptcy();
                        }
                    }
                }
            };
            Apply(dinnerEvt, chart);

            // [CDN fallback] "오늘은 삼겹살로 가자!(-{N}G)"
            string dinnerLabel1Template  = dinnerEvt.choices[1].buttonLabel ?? "";
            // [CDN fallback] "고생하는데 무리좀 해야지… 오늘은 소고기다(-{N}G)"
            string dinnerLabel2Template  = dinnerEvt.choices[2].buttonLabel ?? "";
            // [CDN fallback] "전 직원 만족도 +10 / -{비용}G"
            string dinnerSystem2Template = dinnerEvt.choices[2].resultSystemMessage ?? "";
            // [CDN fallback] "와아! 삼겹살에 소주! 오늘 스트레스 다 날려버려요!"
            dinnerDesc2Happy = dinnerEvt.choices[1].resultDescriptions.Count > 0
                ? dinnerEvt.choices[1].resultDescriptions[0] : "";
            // [CDN fallback] "소고기도 아니고 삼겹살이라니... 뭐 안 먹는 것보단 낫겠네요."
            dinnerDesc2Meh   = dinnerEvt.choices[1].resultDescriptions.Count > 1
                ? dinnerEvt.choices[1].resultDescriptions[1] : "";
            // happy 결과팝업멘트1(choice2_ment1) / meh 결과팝업멘트1(choice2_resultTitle 재사용) / 공통 멘트2(비용)
            dinnerHappyMent1Template = dinnerEvt.choices[1].resultMent1 ?? "";
            dinnerMehMent1Template   = dinnerEvt.choices[1].resultTitle ?? "";
            dinnerMent2Template      = dinnerEvt.choices[1].resultMent2 ?? "";
            // [CDN fallback] "전 직원 만족도 +10"
            string dinnerMent1_3Template    = dinnerEvt.choices[2].resultMent1 ?? "";
            // [CDN fallback] "-{비용}G"
            string dinnerMent2_3Template    = dinnerEvt.choices[2].resultMent2 ?? "";

            dinnerEvt.onSetup = () =>
            {
                int total    = EmployeeManager.Instance.GetTotalSalary();
                dinnerCost5  = Mathf.Max(1, (int)(total * 0.05f));
                dinnerCost10 = Mathf.Max(1, (int)(total * 0.10f));

                dinnerEvt.choices[1].buttonLabel = dinnerLabel1Template.Replace("\n(-{N}G)", "").Replace("(-{N}G)", "");
                dinnerEvt.choices[2].buttonLabel = dinnerLabel2Template.Replace("\n(-{N}G)", "").Replace("(-{N}G)", "");
                dinnerEvt.choices[1].conditionText = $"자금 -{dinnerCost5:N0} G";
                dinnerEvt.choices[2].conditionText = $"자금 -{dinnerCost10:N0} G";
                dinnerEvt.choices[1].resultDescriptions.Clear();
                if (!string.IsNullOrEmpty(dinnerDesc2Happy)) dinnerEvt.choices[1].resultDescriptions.Add(dinnerDesc2Happy);
                if (!string.IsNullOrEmpty(dinnerDesc2Meh))   dinnerEvt.choices[1].resultDescriptions.Add(dinnerDesc2Meh);
                dinnerEvt.choices[2].resultSystemMessage = dinnerSystem2Template.Replace("-{비용}G", $"<color=#F3C01D>(-{dinnerCost10} G)</color>");

                // 괄호까지 색상 태그 안에 포함 — "(-3000G)" 형태로 괄호도 같이 노란색(#F3C01D)으로. 앞에 코인 아이콘 인라인.
                string cost10Tag = $"<sprite=\"CoinSpriteAsset\" name=\"coin\"> <color=#F3C01D>(-{dinnerCost10} G)</color>";
                dinnerEvt.choices[2].resultMent1 = dinnerMent1_3Template;
                dinnerEvt.choices[2].resultMent2 = dinnerMent2_3Template.Replace("-{비용}G", cost10Tag);
            };
            pool.Add(dinnerEvt);
        }

        // ── 사장님 뒷담까기 ──────────────────────────────────────
        // [CDN fallback]
        // title: "사장님 뒷담까기"
        // desc:  "사장님 {직원이름}이 사장님 뒷담을 하는걸 제가 들었어요 어떻게 할까요?"
        // portrait: "portrait_secretary"
        // choice1: label="오늘 간식은 내가 쏜다"
        //          result="애들이 일하느라 예민한가 본데 오늘 간식은 내가 쏜다고 전해. (눈물을 닦으며)"
        //          sys="전체 직원 만족도 +5"
        // choice2: label="회의실로 불러"
        //          result="회의실로 오라고 해봐. 면담 좀 하자."
        //          sys="{직원이름} 만족도 -10"
        {
            EmployeeData gossiperEmp = null;
            RandomEventChoiceData bossGossipEvt = null;
            bossGossipEvt = new RandomEventChoiceData
            {
                type        = RandomEventType.BossGossip,
                weight      = 1f,
                categoryMin = 1,
                categoryMax = 4,
                choices = new List<RandomEventChoiceOption>
                {
                    // ── 선택지 1: 간식 ───────────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () => EmployeeManager.Instance.ChangeAllSatisfaction(5)
                    },
                    // ── 선택지 2: 면담 ───────────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            var emp = EmployeeManager.Instance.GetEmployee(gossiperEmp?.id);
                            if (emp == null) return;
                            int before = emp.satisfaction;
                            emp.ChangeSatisfaction(-5);
                            InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);

                            int buffWeeks = RandomStatBuffWeeksByStage();
                            emp.ApplyStatBuff(buffWeeks, 10);

                            var c1 = bossGossipEvt.choices[1];
                            c1.resultMent3 = c1.resultMent3?.Replace("{주수}", buffWeeks.ToString());
                        }
                    }
                }
            };
            Apply(bossGossipEvt, chart);

            // [CDN fallback] "사장님 {직원이름}가 사장님 뒷담을 하는걸 제가 들었어요 어떻게 할까요?"
            string gossipDescTemplate = bossGossipEvt.description ?? "";
            // [CDN fallback] "{직원이름} 만족도 -5"
            string gossipMent2Template = bossGossipEvt.choices[1].resultMent2 ?? "";
            // [CDN fallback] "{직원이름} 능력치 {주수}주 동안 +10%"
            string gossipMent3Template = bossGossipEvt.choices[1].resultMent3 ?? "";

            bossGossipEvt.onSetup = () =>
            {
                var employees = EmployeeManager.Instance.ownedEmployees;
                if (employees.Count == 0) { bossGossipEvt.cancelled = true; return; }
                gossiperEmp = employees[UnityEngine.Random.Range(0, employees.Count)];

                bossGossipEvt.description =
                    gossipDescTemplate.Replace("{직원이름}", gossiperEmp.employeeName);
                bossGossipEvt.choices[1].resultMent2 =
                    gossipMent2Template.Replace("{직원이름}", gossiperEmp.employeeName);
                bossGossipEvt.choices[1].resultMent3 =
                    gossipMent3Template.Replace("{직원이름}", gossiperEmp.employeeName);
            };
            pool.Add(bossGossipEvt);
        }

        // ── 유튜버 선공개 요청 ────────────────────────────────────
        // [CDN fallback]
        // title:   "유튜버 선공개 요청"
        // desc:    "유튜버가 게임을 미리 해보고 싶다고 연락해 옵니다! 완성이 안된 상태인데 그래도 전달해볼까요?"
        // portrait: "portrait_secretary"
        // choice1: label="난 우리 직원들이 만든게임을 믿고 있어!"
        //          result_high(pop≥3)="인기있는 장르여서 좋은 반응이 나오고 있습니다."
        //          sys_high="이번 게임의 기대감이 오르고 있습니다."
        //          result_mid(pop=2)="흐음 애매한 반응이네요 이걸 좋아해야 할지 안 좋아해야 할지…"
        //          sys_mid=""
        //          result_low(pop≤1)="인기없는 장르라서 그런가… 다들 노잼이라는 반응이 나오고 있습니다."
        //          sys_low="이번 게임의 기대감이 떨어지고 있습니다."
        // choice2: label="미완성 상태에서 보여주는건 너무 도박인 것 같아 패스하자"
        //          result="괜찮은 선택이에요. 완성된 게임으로 승부하죠!"
        {
            RandomEventChoiceData youtuberEvt = null;
            youtuberEvt = new RandomEventChoiceData
            {
                type        = RandomEventType.YoutuberRequest,
                weight      = 1f,
                categoryMin = 3,
                categoryMax = 4,
                choices = new List<RandomEventChoiceOption>
                {
                    // ── 선택지 1: 전달 (onChoose는 Apply 후 캡처된 텍스트 사용)
                    new RandomEventChoiceOption { onChoose = () => { } },
                    // ── 선택지 2: 패스 ───────────────────────────
                    new RandomEventChoiceOption { onChoose = () => { } }
                }
            };
            Apply(youtuberEvt, chart);

            // 장르 인기도별 분기 텍스트를 chart에서 캡처 (CDN 로드 실패 시 빈 문자열)
            string ytDescHigh = youtuberEvt.choices[0].resultDescriptions.Count > 0
                ? youtuberEvt.choices[0].resultDescriptions[0] : "";
            string ytDescMid  = youtuberEvt.choices[0].resultDescriptions.Count > 1
                ? youtuberEvt.choices[0].resultDescriptions[1] : "";
            string ytDescLow  = youtuberEvt.choices[0].resultDescriptions.Count > 2
                ? youtuberEvt.choices[0].resultDescriptions[2] : "";

            RandomEventChoiceChartRow ytRow = null;
            chart?.TryGetValue("YoutuberRequest", out ytRow);
            var ytC1 = ytRow?.choices.Count > 0 ? ytRow.choices[0] : null;
            string ytSysMid  = ytC1?.resultSystemMessage2 ?? "";
            string ytSysLow  = ytC1?.resultSystemMessage3 ?? "";

            // [CDN fallback] 결과팝업멘트1 — 인기도별(high/mid/low) 분기. high=choice1_ment1 원본,
            // mid/low는 systemMessage(=ytSysMid)/systemMessage3(=ytSysLow) 컬럼을 재사용해 저장.
            string ytMent1High = youtuberEvt.choices[0].resultMent1 ?? "";
            string ytMent1Mid  = ytSysMid;
            string ytMent1Low  = ytSysLow;

            youtuberEvt.choices[0].onChoose = () =>
            {
                int pop = ProjectSetupUI.SelectedGenrePopularity;
                youtuberEvt.choices[0].resultDescriptions.Clear();

                if (pop >= 3)
                {
                    mgr.YoutuberSalesBonus = 1.05f;
                    if (!string.IsNullOrEmpty(ytDescHigh)) youtuberEvt.choices[0].resultDescriptions.Add(ytDescHigh);
                    youtuberEvt.choices[0].resultMent1 = ytMent1High;
                }
                else if (pop == 2)
                {
                    mgr.YoutuberSalesBonus = 1.0f;
                    if (!string.IsNullOrEmpty(ytDescMid)) youtuberEvt.choices[0].resultDescriptions.Add(ytDescMid);
                    youtuberEvt.choices[0].resultMent1 = ytMent1Mid;
                }
                else
                {
                    mgr.YoutuberSalesBonus = 0.95f;
                    if (!string.IsNullOrEmpty(ytDescLow)) youtuberEvt.choices[0].resultDescriptions.Add(ytDescLow);
                    youtuberEvt.choices[0].resultMent1 = ytMent1Low;
                }
            };

            youtuberEvt.onSetup = () =>
            {
                youtuberEvt.choices[0].resultDescriptions.Clear();
                youtuberEvt.choices[0].resultMent1 = ytMent1High;
            };
            pool.Add(youtuberEvt);
        }

        // ── 두 직원 싸움 계열 (EmployeeFight wrapper) ─────────────
        // [CDN fallback → 각 서브이벤트 주석 참조]
        // ── 탕수육 부먹 찍먹 싸움 (TangsuYukFight) ──────────────────
        // ── 반민초파의 공격 (AntiMintchoc) ──────────────────────────
        // ── 에어컨 전쟁 (AcWar) ─────────────────────────────────────
        // [CDN fallback 공통]
        // choice1: label="{해당직원이름} 편 들어주기"
        //          resultTitle="이건 육아일까 회사일까"
        //          result1="사장님! 진짜 제 마음을 어쩜 그렇게 잘 알아주세요?..."
        //          result2="사장님이 제 편 안 들어주셨으면 저 오늘 진짜 사직서 쓸 뻔했잖아요..."
        //          result3="거봐요! 내가 맞다니까! 사장님 역시 보는 눈이 정확하시네요"
        //          sys="{직원1파트} 팀장점수 10% 증가 / {직원1이름} 만족도 +10 / {직원2파트} 팀장점수 10% 감소 / {직원2이름} 만족도 -10"
        // choice2: 동일 구조 (반대 직원 편)
        //          result1="말 걸지 마세요"    result2="평생 기억하겠습니다…"
        //          sys="{직원2파트} 팀장점수 10% 증가 / ..."
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
                fightEvt.dialogue2        = chosen.dialogue2;
                fightEvt.question         = chosen.question;
                fightEvt.portraitId       = chosen.portraitId;
                fightEvt.portraitId2      = chosen.portraitId2;
                fightEvt.choices          = chosen.choices;
                fightEvt.targetEmployeeId = chosen.targetEmployeeId;
            };

            pool.Add(fightEvt);
        }

        // ── 퇴근 요청 ─────────────────────────────────────────────
        // [CDN fallback]
        // title: "퇴근요청"
        // desc:  "사장님 이번주는 집중이 안돼서 3시간만 일찍 퇴근하겠습니다!"
        // choice1: label="이건 또 뭔 소리지 당연히 안되는거 아니야?"
        //          result="사장님 지금 '저장' 이랑 '삭제' 버튼을 자꾸 헷갈려 하는데... 이대로 계속 일해 볼게요"
        //          sys="해당직원 만족도 -5"
        // choice2: label="이거 허락 안해주면 또 난리치겠지? 그냥 해줘야겠다"
        //          result="이미 말이 끝나기도 전에 짐을 싸고 사라져있었다…"
        //          sys="해당직원 만족도 +5 / 개발 기간 +{주수}주"
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
                            int before = emp.satisfaction;
                            emp.ChangeSatisfaction(-5);
                            InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
                        }
                    },
                    // ── 선택지 2: 허락 ──────────────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            var emp = EmployeeManager.Instance.GetEmployee(earlyLeaveEvt.targetEmployeeId);
                            if (emp == null) return;
                            int before = emp.satisfaction;
                            emp.ChangeSatisfaction(5);
                            InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);

                            int delayWeeks = ProjectSetupUI.SelectedScale switch
                            {
                                ProjectScale.Small  => 1,
                                ProjectScale.Medium => 2,
                                ProjectScale.Large  => 3,
                                _ => 1
                            };
                            float secondsPerWeek = ProjectSetupUI.SelectedScale switch
                            {
                                ProjectScale.Small  => 80f / 16f, // 5.0초/주
                                ProjectScale.Medium => 80f / 24f, // 3.33초/주
                                ProjectScale.Large  => 80f / 32f, // 2.5초/주
                                _ => 80f / 16f
                            };
                            DevelopmentManager.Instance.ExtendDevelopmentDuration(delayWeeks * secondsPerWeek, delayWeeks * 2 * secondsPerWeek); // 연장 N주 / 감속 2N주
                            InfoFeedUI.Instance?.ShowDevelopmentDelay(emp, delayWeeks);

                            string weeks = delayWeeks.ToString();
                            var c1 = earlyLeaveEvt.choices[1];
                            c1.resultMent3 = c1.resultMent3?.Replace("{주수}", weeks);
                        }
                    }
                }
            };
            Apply(earlyLeaveEvt, chart);

            // [CDN fallback] "{해당직원이름} 만족도 -5" / "{해당직원이름} 만족도 +5"
            string earlyLeaveMent2_0Template = earlyLeaveEvt.choices[0].resultMent2 ?? "";
            string earlyLeaveMent2_1Template = earlyLeaveEvt.choices[1].resultMent2 ?? "";

            earlyLeaveEvt.onSetup = () =>
            {
                var employees = EmployeeManager.Instance.ownedEmployees;
                if (employees.Count == 0) { earlyLeaveEvt.cancelled = true; return; }
                earlyLeaveEmp = employees[UnityEngine.Random.Range(0, employees.Count)];
                earlyLeaveEvt.portraitId       = earlyLeaveEmp.portraitId;
                earlyLeaveEvt.targetEmployeeId = earlyLeaveEmp.id;

                earlyLeaveEvt.choices[0].resultMent2 = earlyLeaveMent2_0Template.Replace("{해당직원이름}", earlyLeaveEmp.employeeName);
                earlyLeaveEvt.choices[1].resultMent2 = earlyLeaveMent2_1Template.Replace("{해당직원이름}", earlyLeaveEmp.employeeName);
            };
            pool.Add(earlyLeaveEvt);
        }

        // ── 야매 코드 ─────────────────────────────────────────────
        // [CDN fallback]
        // title: "야매 코드"
        // desc:  "사장님 정석대로 만들면 너무 오래 걸려요. 일단 대충 돌아가게만 짜둘까요?"
        // choice1: label="일단 날림으로 개발하고 나중에 고쳐볼까?"
        //          result="나중에 서버 터져도 저한테만 뭐라하면 안 돼요?"
        //          sys="" (디버프는 나중에 발동)
        // choice2: label="오래 걸리더라도 무조건 완벽하게 개발하자"
        //          result="후… 일단 {주수}주는 더 걸릴 것 같네요"
        //          sys="개발 기간 +{주수}주 연장"
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
                            int delayWeeks = ProjectSetupUI.SelectedScale switch
                            {
                                ProjectScale.Small  => 1,
                                ProjectScale.Medium => 2,
                                ProjectScale.Large  => 3,
                                _ => 1
                            };
                            float secondsPerWeek = ProjectSetupUI.SelectedScale switch
                            {
                                ProjectScale.Small  => 80f / 16f, // 5.0초/주
                                ProjectScale.Medium => 80f / 24f, // 3.33초/주
                                ProjectScale.Large  => 80f / 32f, // 2.5초/주
                                _ => 80f / 16f
                            };
                            DevelopmentManager.Instance.ExtendDevelopmentDuration(delayWeeks * secondsPerWeek, delayWeeks * 2 * secondsPerWeek); // 연장 N주 / 감속 2N주
                            InfoFeedUI.Instance?.ShowDevelopmentDelay(hackyEmp, delayWeeks);
                            string weeks = delayWeeks.ToString();
                            var c1 = hackyEvt.choices[1];
                            c1.reply1     = c1.reply1?.Replace("{주수}", weeks);
                            c1.resultMent2 = c1.resultMent2?.Replace("{주수}", weeks);
                        }
                    }
                }
            };
            Apply(hackyEvt, chart);

            // [CDN fallback] "후… 일단 {주수}주는 더 걸릴 것 같네요"
            string hackyReply1Template = hackyEvt.choices[1].reply1 ?? "";
            // [CDN fallback] "개발 기간 +{주수}주"
            string hackyMent2Template  = hackyEvt.choices[1].resultMent2 ?? "";

            hackyEvt.onSetup = () =>
            {
                var programmers = EmployeeManager.Instance.ownedEmployees
                    .FindAll(e => e.role == EmployeeRole.Programmer);
                if (programmers.Count == 0) { hackyEvt.cancelled = true; return; }
                hackyEmp = programmers[UnityEngine.Random.Range(0, programmers.Count)];
                hackyEvt.portraitId       = hackyEmp.portraitId;
                hackyEvt.targetEmployeeId = hackyEmp.id;

                hackyEvt.choices[1].reply1     = hackyReply1Template;
                hackyEvt.choices[1].resultMent2 = hackyMent2Template;
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

    // 사무실 단계별 스탯버프 지속주수 랜덤(사용자 명세: "1,2단계 5~15 랜덤 / 3,4단계 10~20 랜덤").
    // StageManager.CurrentStage(사무실 확장 단계) 기준 — 1~2단계 vs 3단계 이상.
    // internal — RandomEvents_Condition_Choice(CoffeeRequest/EnergyDrinkRequest)도 동일 공식 재사용.
    internal static int RandomStatBuffWeeksByStage()
    {
        int stage = StageManager.Instance != null ? StageManager.Instance.CurrentStage : 1;
        bool highTier = stage >= 3;
        return highTier ? UnityEngine.Random.Range(10, 21) : UnityEngine.Random.Range(5, 16);
    }

    // ── 튜토리얼 전용 — 주말 출근 (Tut1Event) ──────────────────────
    // [CDN fallback — RandomEventChoice_Chart.csv 의 Tut1Event 행]
    // title: "Tut1Event" / desc: "사장님, 시키신 일이 많아서 다 못끝냈는데.. 주말에도 출근해서 끝내야겠죠?"
    // question: "직원의 주말 출근... 어떻게할까?"
    // choice1: label="그 일은 너밖에 못하는거라… 부탁할게"
    //          reply1="하긴, 이건 제가 아니면 못 하는 일이긴 하죠. … 믿을만한 사람은 또 저밖에 없네요!"
    //          popupType=1, ment1="{직원이름} 만족도 +25"
    // choice2: label="그래도 주말엔 쉬어야지 노트북 두고 가"
    //          reply1="네? 진짜 가도 돼요..? 저 월요일에 두 배로 할게요. 진짜로요."
    //          popupType=1, ment1="{직원이름} 만족도 +25" (어느 쪽을 골라도 결과는 동일하게 만족도 +25)
    // 일반 랜덤 풀에는 등록하지 않음(Register의 pool.Add 없음) — RandomEventManager.TriggerTutorial1Event가
    // 특정 직원을 지정해 결정적으로만 발동시킨다.
    public static RandomEventChoiceData CreateTut1Event(
        Dictionary<string, RandomEventChoiceChartRow> chart, EmployeeData targetEmp)
    {
        RandomEventChoiceData evt = null;
        evt = new RandomEventChoiceData
        {
            type        = RandomEventType.Tut1Event,
            weight      = 1f,
            categoryMin = 1,
            categoryMax = 4,
            requiresPatrol        = true,
            requiredPatrolPointId = "master_desk",
            choices = new List<RandomEventChoiceOption>
            {
                // ── 선택지 1: 부탁하기 ───────────────────────
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        var emp = EmployeeManager.Instance.GetEmployee(evt.targetEmployeeId);
                        if (emp == null) return;
                        int before = emp.satisfaction;
                        emp.ChangeSatisfaction(25);
                        InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
                    }
                },
                // ── 선택지 2: 쉬게 해주기 ─────────────────────
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        var emp = EmployeeManager.Instance.GetEmployee(evt.targetEmployeeId);
                        if (emp == null) return;
                        int before = emp.satisfaction;
                        emp.ChangeSatisfaction(25);
                        InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
                    }
                }
            }
        };
        Apply(evt, chart);

        // [CDN fallback] "{직원이름} 만족도 +25" (둘 다 동일 템플릿)
        string ment1_0Template = evt.choices[0].resultMent1 ?? "";
        string ment1_1Template = evt.choices[1].resultMent1 ?? "";

        evt.onSetup = () =>
        {
            evt.portraitId       = targetEmp.portraitId;
            evt.targetEmployeeId = targetEmp.id;

            evt.choices[0].resultMent1 = ment1_0Template.Replace("{직원이름}", targetEmp.employeeName);
            evt.choices[1].resultMent1 = ment1_1Template.Replace("{직원이름}", targetEmp.employeeName);
        };
        return evt;
    }

    // ── 튜토리얼 전용 — 표절 논란 (Tut2Event) ────────────────────────
    // [CDN fallback — RandomEventChoice_Chart.csv 의 Tut2Event 행]
    // title: "Tut2Event" / desc: "사장님, 커뮤니티에 우리 게임 얘기가 올라왔는데… 좀 안 좋은 쪽이에요."
    // dialogue2: "어떤 사람이 저희 게임이 자기 아이디어를 베꼈다고 글을 올렸습니다. 댓글이 벌써 300개를 넘었어요."
    // question: "표절이라는 주장… 어떻게 할까?" / portrait: portrait_secretary(비서가 보고)
    // choice1: label="사실이 아니잖아. 정면으로 반박해."
    //          reply1="반박문 올렸는데… 댓글이 두 배가 됐어요. 이제 기사까지 났고요."
    //          reply2="논란으로 인해 매출이 하락하고 있습니다."
    // choice2: label="혹시 모르니 억울해지기 전에 조용히 합의하자." (자금 -100의 자리까지)
    //          reply1="합의는 했는데요… 그 사람이 합의금 받은 걸 인증샷으로 올렸어요."
    //          reply2="논란으로 인해 매출이 하락하고 있습니다."
    // 능력치 디버프는 제거됨 — 선택지1/2 둘 다 결과는 동일하게 보유 골드를 100의 자리까지 차감
    // (예: 780 보유 → -700, 4890 보유 → -4800 / 10~99의 자리는 남김, AlertUI2/moneyPanel 로 표시).
    // 선택지2는 합의금이라 미리 conditionText 로 예고되지만, 선택지1(정면 반박)은 매출 하락이라는
    // 간접 손실이라 예고 없이 선택 직후 AlertPanel2로만 통보된다 — 결과 금액 자체는 완전히 동일.
    // 대상 직원은 onSetup에서 랜덤 선정(BossGossip과 동일 패턴, 특정 데스크 지정 없음) — 현재는 portrait만
    // 쓰이고 텍스트/효과에는 관여하지 않음.
    // 일반 랜덤 풀에는 등록하지 않음(Register의 pool.Add 없음) — RandomEventManager.TriggerTutorial2Event가
    // 결정적으로만 발동시킨다.

    // 보유 골드를 100의 자리까지 차감(10~99의 자리는 남김) — 100 미만 보유 시 0 차감.
    static int Tut2CostFor(int gold) => gold - gold % 100;

    static void ApplyTut2Cost(int cost, string message)
    {
        MoneyManager.Instance.ForceSpendGold(cost, saveImmediately: false);
        AlertUI.Instance.ShowMoney(message, -cost);
    }

    public static RandomEventChoiceData CreateTut2Event(Dictionary<string, RandomEventChoiceChartRow> chart)
    {
        EmployeeData targetEmp = null;
        RandomEventChoiceData evt = null;
        evt = new RandomEventChoiceData
        {
            type        = RandomEventType.Tut2Event,
            weight      = 1f,
            categoryMin = 1,
            categoryMax = 4,
            requiresPatrol        = false,
            requiredPatrolPointId = "",
            choices = new List<RandomEventChoiceOption>
            {
                // ── 선택지 1: 정면 반박 ────────────────────────
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        int cost = Tut2CostFor(MoneyManager.Instance.Gold);
                        ApplyTut2Cost(cost, $"논란으로 매출이 하락해 {cost:N0}G의 손해를 봤습니다");
                    }
                },
                // ── 선택지 2: 조용히 합의(100의 자리까지) ────────────────
                new RandomEventChoiceOption
                {
                    onChoose = () =>
                    {
                        int cost = Tut2CostFor(MoneyManager.Instance.Gold);
                        ApplyTut2Cost(cost, $"합의금으로 {cost:N0}G가 차감되었습니다");
                    }
                }
            }
        };
        Apply(evt, chart);

        // 능력치 디버프를 없앤 뒤로는 "{직원이름} 능력치 4주 동안 -10%" 결과 팝업 텍스트도 더 이상 사실이
        // 아니므로 표시하지 않는다(차트가 채워둔 값을 그대로 두면 안 일어난 일을 보여주게 됨).
        evt.choices[0].resultMent1 = "";
        evt.choices[1].resultMent1 = "";

        evt.onSetup = () =>
        {
            var employees = EmployeeManager.Instance.ownedEmployees;
            if (employees.Count == 0) { evt.cancelled = true; return; }
            targetEmp = employees[UnityEngine.Random.Range(0, employees.Count)];
            evt.portraitId       = "portrait_secretary";
            evt.targetEmployeeId = targetEmp.id;

            // 선택지2 예고 문구도 실제 차감액(100의 자리까지)에 맞춰 매번 갱신 — 보유 골드가 바뀌어도
            // 이벤트가 뜰 때마다 정확한 금액이 보이게 한다.
            evt.choices[1].conditionText = $"자금 -{Tut2CostFor(MoneyManager.Instance.Gold):N0} G";
        };
        return evt;
    }

    // 두 직원 싸움 계열 이벤트 공통 생성 (TangsuYukFight / AntiMintchoc / AcWar)
    // forcedEmp1/forcedEmp2: 둘 다 지정되면 역할 랜덤 선정을 건너뛰고 그대로 사용(튜토리얼 등 결정적 발동용).
    public static RandomEventChoiceData CreateTwoEmpFightEvent(
        RandomEventType type,
        Dictionary<string, RandomEventChoiceChartRow> chart,
        EmployeeData forcedEmp1 = null,
        EmployeeData forcedEmp2 = null)
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
                        int before = winner.satisfaction;
                        winner.ChangeSatisfaction(10);
                        InfoFeedUI.Instance?.ShowSatisfaction(winner, winner.satisfaction - before);
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
                        int before = winner.satisfaction;
                        winner.ChangeSatisfaction(10);
                        InfoFeedUI.Instance?.ShowSatisfaction(winner, winner.satisfaction - before);
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

        // [CDN fallback] "{해당직원이름} 편 들어주기"
        string label0Template  = evt.choices[0].buttonLabel ?? "";
        // [CDN fallback] "{해당직원이름} 편 들어주기" (반대 직원)
        string label1Template  = evt.choices[1].buttonLabel ?? "";
        // [CDN fallback] "{직원1파트} 팀장점수 10% 증가 / {직원1이름} 만족도 +10 / {직원2파트} 팀장점수 10% 감소 / {직원2이름} 만족도 -10"
        string system0Template = evt.choices[0].resultSystemMessage ?? "";
        // [CDN fallback] "{직원2파트} 팀장점수 10% 증가 / {직원2이름} 만족도 +10 / {직원1파트} 팀장점수 10% 감소 / {직원1이름} 만족도 -10"
        string system1Template = evt.choices[1].resultSystemMessage ?? "";
        // 결과 팝업(AlertUI4 2연속) — choice0: 1차=승자(emp1) 효과, 2차=패자(emp2) 효과 / choice1은 반대.
        string ment1_0Template = evt.choices[0].resultMent1   ?? ""; // "{직원1파트} 팀장점수 10% 증가"
        string ment2_0Template = evt.choices[0].resultMent2   ?? ""; // "{직원1이름} 만족도 +10"
        string ment1_0b_Template = evt.choices[0].resultMent1_2 ?? ""; // "{직원2파트} 팀장점수 10% 감소"
        string ment2_0b_Template = evt.choices[0].resultMent2_2 ?? ""; // "{직원2이름} 만족도 -10"
        string ment1_1Template = evt.choices[1].resultMent1   ?? "";
        string ment2_1Template = evt.choices[1].resultMent2   ?? "";
        string ment1_1b_Template = evt.choices[1].resultMent1_2 ?? "";
        string ment2_1b_Template = evt.choices[1].resultMent2_2 ?? "";
        // [CDN fallback] ["사장님! 진짜 제 마음을 어쩜 그렇게 잘 알아주세요?...", "사장님이 제 편 안 들어주셨으면 저 오늘 진짜 사직서 쓸 뻔했잖아요...", "거봐요! 내가 맞다니까!"]
        var    happyDescs      = new List<string>(evt.choices[0].resultDescriptions);
        // [CDN fallback] ["말 걸지 마세요", "평생 기억하겠습니다…"]
        var    angryDescs      = new List<string>(evt.choices[1].resultDescriptions);
        // [CDN fallback] "이건 육아일까 회사일까"
        string happyTitle      = evt.choices[0].resultTitle ?? "";
        string angryTitle      = evt.choices[1].resultTitle ?? "";

        evt.onSetup = () =>
        {
            if (forcedEmp1 != null && forcedEmp2 != null)
            {
                emp1 = forcedEmp1;
                emp2 = forcedEmp2;
            }
            else
            {
                // 파견중(사무실 부재) 직원은 master_desk 로 강제이동이 불가(_characters 에 없음) → 싸움 대상에서 제외.
                bool Eligible(EmployeeData e) =>
                    DispatchManager.Instance == null || !DispatchManager.Instance.IsDispatched(e.id);

                var availableRoles = new List<EmployeeRole>();
                foreach (var r in new[] { EmployeeRole.Planner, EmployeeRole.Programmer, EmployeeRole.Artist })
                    if (EmployeeManager.Instance.ownedEmployees.Exists(e => e.role == r && Eligible(e)))
                        availableRoles.Add(r);

                if (availableRoles.Count < 2)
                {
                    evt.cancelled = true;
                    Debug.LogWarning("[EmployeeFight] 취소 — 서로 다른 역할의 (파견 제외) 직원이 2명 미만");
                    return;
                }

                for (int i = availableRoles.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    var tmp = availableRoles[i]; availableRoles[i] = availableRoles[j]; availableRoles[j] = tmp;
                }

                var pool1 = EmployeeManager.Instance.ownedEmployees.FindAll(e => e.role == availableRoles[0] && Eligible(e));
                var pool2 = EmployeeManager.Instance.ownedEmployees.FindAll(e => e.role == availableRoles[1] && Eligible(e));
                emp1 = pool1[UnityEngine.Random.Range(0, pool1.Count)];
                emp2 = pool2[UnityEngine.Random.Range(0, pool2.Count)];
            }

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

            // 결과 팝업 — choice0(emp1 편들기): 1차=emp1(승자) 효과, 2차=emp2(패자) 효과. choice1은 반대.
            string Sub(string t) => t
                .Replace("{직원1파트}", role1).Replace("{직원1이름}", emp1.employeeName)
                .Replace("{직원2파트}", role2).Replace("{직원2이름}", emp2.employeeName);

            evt.choices[0].resultMent1   = Sub(ment1_0Template);
            evt.choices[0].resultMent2   = Sub(ment2_0Template);
            evt.choices[0].resultMent1_2 = Sub(ment1_0b_Template);
            evt.choices[0].resultMent2_2 = Sub(ment2_0b_Template);

            evt.choices[1].resultMent1   = Sub(ment1_1Template);
            evt.choices[1].resultMent2   = Sub(ment2_1Template);
            evt.choices[1].resultMent1_2 = Sub(ment1_1b_Template);
            evt.choices[1].resultMent2_2 = Sub(ment2_1b_Template);

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
                int before = loser.satisfaction;
                loser.ChangeSatisfaction(-10);
                InfoFeedUI.Instance?.ShowSatisfaction(loser, loser.satisfaction - before);
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
                int before = loser.satisfaction;
                loser.ChangeSatisfaction(-10);
                InfoFeedUI.Instance?.ShowSatisfaction(loser, loser.satisfaction - before);
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
