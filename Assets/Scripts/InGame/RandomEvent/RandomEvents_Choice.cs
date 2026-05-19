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

                    int cost = Mathf.Max(1, (int)(EmployeeManager.Instance.GetTotalSalary() * 0.03f));
                    birthdayEvt.choices[1].buttonLabel += $"-{cost}G";

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

                            MoneyManager.Instance.ForceSpendGold(equipCostSnapshot, saveImmediately: false);

                            int buffWeeks = UnityEngine.Random.Range(4, 9);
                            emp.ApplyStatBuff(buffWeeks, 20);

                            string weeks = buffWeeks.ToString();
                            equipEvt.choices[0].resultSystemMessage =
                                equipEvt.choices[0].resultSystemMessage?.Replace("{주수}", weeks);
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
            // [CDN fallback] "{해당직원이름} 능력치 {주수}주 동안 +20%"
            string equipSystem0Template = equipEvt.choices[0].resultSystemMessage ?? "";

            // description2는 아티스트 전용 (chart row에서 직접 참조)
            RandomEventChoiceChartRow eqChartRow = null;
            chart?.TryGetValue("EquipmentUpgrade", out eqChartRow);
            // [CDN fallback] "사장님 컴퓨터 새로 하나 바꿔주시죠"
            string equipDescDefaultTemplate = equipEvt.description ?? "";
            // [CDN fallback] "사장님 태블릿 하나 새로 사주시죠"
            string equipDescArtistTemplate  = !string.IsNullOrEmpty(eqChartRow?.description2)
                ? eqChartRow.description2 : "";

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

                equipEvt.description = (equipEmp.role == EmployeeRole.Artist && !string.IsNullOrEmpty(equipDescArtistTemplate))
                    ? equipDescArtistTemplate
                    : equipDescDefaultTemplate;

                equipCostSnapshot = Mathf.Max(1, (int)(EmployeeManager.Instance.GetTotalSalary() * 0.03f));

                equipEvt.choices[0].buttonLabel =
                    equipLabel0Template.Replace("{N}", equipCostSnapshot.ToString());
                equipEvt.choices[0].resultSystemMessage =
                    equipSystem0Template.Replace("{해당직원이름}", equipEmp.employeeName);
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
                    // ── 선택지 2: 삼겹살 (50% 확률로 만족도 +5) ──
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            MoneyManager.Instance.ForceSpendGold(dinnerCost5, saveImmediately: false);

                            bool happy = UnityEngine.Random.value < 0.5f;
                            if (happy)
                                EmployeeManager.Instance.ChangeAllSatisfaction(5);

                            dinnerEvt.choices[1].resultDescriptions.Clear();
                            dinnerEvt.choices[1].resultDescriptions.Add(
                                happy ? dinnerDesc2Happy : dinnerDesc2Meh);

                            // 시스템 문구를 차트에서 참조 (happy/not-happy 분기)
                            RandomEventChoiceChartRow dcRow = null;
                            chart?.TryGetValue("CompanyDinner", out dcRow);
                            var dc2 = dcRow?.choices.Count > 1 ? dcRow.choices[1] : null;
                            string happySys = (dc2?.resultSystemMessage  ?? "").Replace("{비용}", dinnerCost5.ToString());
                            string mehSys   = (dc2?.resultSystemMessage2 ?? "").Replace("{비용}", dinnerCost5.ToString());
                            dinnerEvt.choices[1].resultSystemMessage = happy ? happySys : mehSys;
                        }
                    },
                    // ── 선택지 3: 소고기 ─────────────────────────
                    new RandomEventChoiceOption
                    {
                        onChoose = () =>
                        {
                            MoneyManager.Instance.ForceSpendGold(dinnerCost10, saveImmediately: false);
                            EmployeeManager.Instance.ChangeAllSatisfaction(10);
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

            dinnerEvt.onSetup = () =>
            {
                int total    = EmployeeManager.Instance.GetTotalSalary();
                dinnerCost5  = Mathf.Max(1, (int)(total * 0.05f));
                dinnerCost10 = Mathf.Max(1, (int)(total * 0.10f));

                dinnerEvt.choices[1].buttonLabel = dinnerLabel1Template.Replace("{N}", dinnerCost5.ToString());
                dinnerEvt.choices[2].buttonLabel = dinnerLabel2Template.Replace("{N}", dinnerCost10.ToString());
                dinnerEvt.choices[1].resultDescriptions.Clear();
                if (!string.IsNullOrEmpty(dinnerDesc2Happy)) dinnerEvt.choices[1].resultDescriptions.Add(dinnerDesc2Happy);
                if (!string.IsNullOrEmpty(dinnerDesc2Meh))   dinnerEvt.choices[1].resultDescriptions.Add(dinnerDesc2Meh);
                dinnerEvt.choices[2].resultSystemMessage = dinnerSystem2Template.Replace("{비용}", dinnerCost10.ToString());
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
                            emp.ChangeSatisfaction(-10);
                            OfficeManager.Instance?.ShowStatPopup(emp.id, "만족도 -10", new Color(0.4f, 0.6f, 1f));
                        }
                    }
                }
            };
            Apply(bossGossipEvt, chart);

            // [CDN fallback] "사장님 {직원이름}이 사장님 뒷담을 하는걸 제가 들었어요 어떻게 할까요?"
            string gossipDescTemplate    = bossGossipEvt.description ?? "";
            // [CDN fallback] "{직원이름} 만족도 -10"
            string gossipSystem1Template = bossGossipEvt.choices[1].resultSystemMessage ?? "";

            bossGossipEvt.onSetup = () =>
            {
                var employees = EmployeeManager.Instance.ownedEmployees;
                if (employees.Count == 0) { bossGossipEvt.cancelled = true; return; }
                gossiperEmp = employees[UnityEngine.Random.Range(0, employees.Count)];

                bossGossipEvt.description =
                    gossipDescTemplate.Replace("{직원이름}", gossiperEmp.employeeName);
                bossGossipEvt.choices[1].resultSystemMessage =
                    gossipSystem1Template.Replace("{직원이름}", gossiperEmp.employeeName);
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
            string ytSysHigh = ytC1?.resultSystemMessage  ?? "";
            string ytSysMid  = ytC1?.resultSystemMessage2 ?? "";
            string ytSysLow  = ytC1?.resultSystemMessage3 ?? "";

            youtuberEvt.choices[0].onChoose = () =>
            {
                int pop = ProjectSetupUI.SelectedGenrePopularity;
                youtuberEvt.choices[0].resultDescriptions.Clear();

                if (pop >= 3)
                {
                    mgr.YoutuberSalesBonus = 1.05f;
                    if (!string.IsNullOrEmpty(ytDescHigh)) youtuberEvt.choices[0].resultDescriptions.Add(ytDescHigh);
                    if (!string.IsNullOrEmpty(ytSysHigh))  youtuberEvt.choices[0].resultSystemMessage = ytSysHigh;
                }
                else if (pop == 2)
                {
                    mgr.YoutuberSalesBonus = 1.0f;
                    if (!string.IsNullOrEmpty(ytDescMid)) youtuberEvt.choices[0].resultDescriptions.Add(ytDescMid);
                    if (!string.IsNullOrEmpty(ytSysMid))  youtuberEvt.choices[0].resultSystemMessage = ytSysMid;
                }
                else
                {
                    mgr.YoutuberSalesBonus = 0.95f;
                    if (!string.IsNullOrEmpty(ytDescLow)) youtuberEvt.choices[0].resultDescriptions.Add(ytDescLow);
                    if (!string.IsNullOrEmpty(ytSysLow))  youtuberEvt.choices[0].resultSystemMessage = ytSysLow;
                }
            };

            youtuberEvt.onSetup = () =>
            {
                youtuberEvt.choices[0].resultDescriptions.Clear();
                youtuberEvt.choices[0].resultSystemMessage = "";
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

                            int delayWeeks = ProjectSetupUI.SelectedScale switch
                            {
                                ProjectScale.Small  => 1,
                                ProjectScale.Medium => 2,
                                ProjectScale.Large  => 3,
                                _ => 1
                            };
                            float secondsPerWeek = ProjectSetupUI.SelectedScale switch
                            {
                                ProjectScale.Small  => 5f,
                                ProjectScale.Medium => 4.2f,
                                ProjectScale.Large  => 3.9f,
                                _ => 5f
                            };
                            DevelopmentManager.Instance.ExtendDevelopmentDuration(delayWeeks * 2 * secondsPerWeek);

                            string weeks = delayWeeks.ToString();
                            earlyLeaveEvt.choices[1].resultSystemMessage =
                                earlyLeaveEvt.choices[1].resultSystemMessage?.Replace("{주수}", weeks);
                        }
                    }
                }
            };
            Apply(earlyLeaveEvt, chart);

            // [CDN fallback] "해당직원 만족도 +5 / 개발 기간 +{주수}주"
            string earlyLeaveSystem2Template = earlyLeaveEvt.choices[1].resultSystemMessage ?? "";

            earlyLeaveEvt.onSetup = () =>
            {
                var employees = EmployeeManager.Instance.ownedEmployees;
                if (employees.Count == 0) { earlyLeaveEvt.cancelled = true; return; }
                earlyLeaveEmp = employees[UnityEngine.Random.Range(0, employees.Count)];
                earlyLeaveEvt.portraitId       = earlyLeaveEmp.portraitId;
                earlyLeaveEvt.targetEmployeeId = earlyLeaveEmp.id;

                earlyLeaveEvt.choices[1].resultSystemMessage = earlyLeaveSystem2Template;
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

            // [CDN fallback] "후… 일단 {주수}주는 더 걸릴 것 같네요"
            string hackyDesc2Template   = hackyEvt.choices[1].resultDescriptions.Count > 0
                ? hackyEvt.choices[1].resultDescriptions[0] : "";
            // [CDN fallback] "개발 기간 +{주수}주 연장"
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

    // 두 직원 싸움 계열 이벤트 공통 생성 (TangsuYukFight / AntiMintchoc / AcWar)
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

        // [CDN fallback] "{해당직원이름} 편 들어주기"
        string label0Template  = evt.choices[0].buttonLabel ?? "";
        // [CDN fallback] "{해당직원이름} 편 들어주기" (반대 직원)
        string label1Template  = evt.choices[1].buttonLabel ?? "";
        // [CDN fallback] "{직원1파트} 팀장점수 10% 증가 / {직원1이름} 만족도 +10 / {직원2파트} 팀장점수 10% 감소 / {직원2이름} 만족도 -10"
        string system0Template = evt.choices[0].resultSystemMessage ?? "";
        // [CDN fallback] "{직원2파트} 팀장점수 10% 증가 / {직원2이름} 만족도 +10 / {직원1파트} 팀장점수 10% 감소 / {직원1이름} 만족도 -10"
        string system1Template = evt.choices[1].resultSystemMessage ?? "";
        // [CDN fallback] ["사장님! 진짜 제 마음을 어쩜 그렇게 잘 알아주세요?...", "사장님이 제 편 안 들어주셨으면 저 오늘 진짜 사직서 쓸 뻔했잖아요...", "거봐요! 내가 맞다니까!"]
        var    happyDescs      = new List<string>(evt.choices[0].resultDescriptions);
        // [CDN fallback] ["말 걸지 마세요", "평생 기억하겠습니다…"]
        var    angryDescs      = new List<string>(evt.choices[1].resultDescriptions);
        // [CDN fallback] "이건 육아일까 회사일까"
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
