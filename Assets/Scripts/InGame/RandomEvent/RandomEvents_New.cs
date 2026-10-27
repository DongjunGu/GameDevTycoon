using System.Collections.Generic;
using UnityEngine;

// 이벤트_밸런싱_가이드.md 신규 이벤트 (⑪~⑮, ⑰, ⑲~㉑, ㉔ + 선택지 없는 12종).
// 텍스트는 전부 뒤끝 차트(RandomEventChoice / RandomEvent) — 차트에 행이 없으면 빈 이벤트가 뜨지 않게 등록 자체를 안 한다.
//
// 플레이스홀더: {직원이름} {파트} {주수} {비용} {점수}
// 확률 결과가 둘로 갈리는 선택지 — 결과 A: reply1 / ment1~3 (popupType)
//                                  결과 B: resultDescription2 → 답변, systemMessage2 / systemMessage3 → 결과팝업 멘트1/2
public static class RandomEvents_New
{
    // ── 공용 ──────────────────────────────────────────────────────
    static string R(string t, (string k, string v)[] kv)
    {
        if (string.IsNullOrEmpty(t)) return t;
        foreach (var (k, v) in kv) t = t.Replace(k, v);
        return t;
    }

    // 선택지 데이터의 모든 텍스트 필드에 플레이스홀더 치환
    static void Fill(RandomEventChoiceData e, params (string k, string v)[] kv)
    {
        e.title = R(e.title, kv); e.description = R(e.description, kv);
        e.dialogue2 = R(e.dialogue2, kv); e.question = R(e.question, kv);
        foreach (var c in e.choices)
        {
            c.buttonLabel = R(c.buttonLabel, kv); c.reply1 = R(c.reply1, kv); c.reply2 = R(c.reply2, kv);
            c.resultMent1 = R(c.resultMent1, kv); c.resultMent2 = R(c.resultMent2, kv); c.resultMent3 = R(c.resultMent3, kv);
            c.resultSystemMessage = R(c.resultSystemMessage, kv);
            for (int i = 0; i < c.resultDescriptions.Count; i++) c.resultDescriptions[i] = R(c.resultDescriptions[i], kv);
        }
    }

    // 확률 결과 B 텍스트로 교체 (결과 A 는 차트 기본값 그대로)
    static void UseOutcomeB(RandomEventChoiceData e, int choice, int popupType, params (string k, string v)[] kv)
    {
        var cache = RandomEventChoiceChartLoader.Cache;
        if (cache == null || !cache.TryGetValue(e.type.ToString(), out var row) || row.choices.Count <= choice) return;
        var src = row.choices[choice];
        var c = e.choices[choice];
        c.reply1          = R(src.resultDescription2, kv);
        c.resultMent1     = R(src.resultSystemMessage2, kv);
        c.resultMent2     = R(src.resultSystemMessage3, kv);
        c.resultMent3     = "";
        c.resultPopupType = popupType;
    }

    static (string, string) Name(EmployeeData e) => ("{직원이름}", e != null ? e.employeeName : "");
    static (string, string) Part(EmployeeRole r) => ("{파트}", EventBalance.PartName(r));
    static (string, string) Weeks(int w) => ("{주수}", w.ToString());
    static (string, string) Cost(int g) => ("{비용}", g.ToString("N0"));
    static (string, string) Material(EmployeeRole r) =>
        ("{자료}", r switch { EmployeeRole.Programmer => "개발노트", EmployeeRole.Artist => "아트 리소스", _ => "기획서" });
    static (string, string) Job(EmployeeRole r) =>
        ("{직군}", r switch { EmployeeRole.Programmer => "개발자", EmployeeRole.Artist => "디자이너", _ => "PD" });
    static (string, string) Score(float s) => ("{점수}", Mathf.RoundToInt(Mathf.Abs(s)).ToString("N0"));

    static RandomEventChoiceData NewChoice(RandomEventType type, int choiceCount)
    {
        var e = new RandomEventChoiceData { type = type, weight = 1f, categoryMin = 1, categoryMax = 4 };
        for (int i = 0; i < choiceCount; i++) e.choices.Add(new RandomEventChoiceOption { onChoose = () => { } });
        return e;
    }

    static bool AddIfCharted(List<RandomEventChoiceData> pool, RandomEventChoiceData e,
                             Dictionary<string, RandomEventChoiceChartRow> chart)
    {
        if (chart == null || !chart.ContainsKey(e.type.ToString())) return false;
        RandomEventChoiceChartLoader.Apply(e, e.type.ToString(), chart);
        pool.Add(e);
        return true;
    }

    static void AddIfCharted(List<RandomEventData> pool, RandomEventData e,
                             Dictionary<string, RandomEventChartRow> chart)
    {
        if (chart == null || !chart.ContainsKey(e.type.ToString())) return;
        RandomEventChartLoader.Apply(e, chart);
        pool.Add(e);
    }

    static EmployeeData Get(string id) => EmployeeManager.Instance.GetEmployee(id);

    // 가이드 §3-7 E(s) — s성 → s+1성 강화 기대 비용 (강화비용 + 하락 재시도 포함)
    static readonly int[] ExpectedEnhanceStep =
    {
        250, 267, 571, 615, 1000, 1000, 1455, 1818, 2600, 3200, 4000, 7733, 8891,
        11461, 13589, 15995, 33714, 39680, 58465, 73642, 88656, 196349, 281394, 984510, 3849020,
    };

    // ── 선택지 이벤트 ─────────────────────────────────────────────
    public static void RegisterChoice(List<RandomEventChoiceData> pool, RandomEventManager mgr,
                                      Dictionary<string, RandomEventChoiceChartRow> chart)
    {
        // ⑪ 중고 거래 (의자) — 산다: 돈 −R × 2.5%, 70% 개인 버프 +1블록 / 30% 개인 디버프 −½블록 · 안 산다: X
        {
            var e = NewChoice(RandomEventType.UsedChair, 2);
            EmployeeData t = null; int cost = 0;
            e.onSetup = () =>
            {
                t = EventBalance.PickPresent();
                if (t == null) { e.cancelled = true; return; }
                e.portraitId = t.portraitId; e.targetEmployeeId = t.id;
                cost = EventBalance.Money(2.5f);
                EventBalance.SetCostChoice(e.choices[0], cost);
                Fill(e, Name(t), Cost(cost));
            };
            e.choices[0].onChoose = () =>
            {
                MoneyManager.Instance.ForceSpendGold(cost, saveImmediately: false);
                if (Random.value < 0.7f) Fill(e, Weeks(EventBalance.Buff(t)));
                else UseOutcomeB(e, 0, 3, Name(t), Cost(cost), Weeks(EventBalance.Debuff(t, 0.5f)));
            };
            AddIfCharted(pool, e, chart);
        }

        // ⑫ 주인없는 고양이 — 키운다: 50% 전 직원 만족도 +5 / 50% 해당 직원 파트 −μ × 5.5/8.5/10/14% · 미안해: 해당 직원 −5
        {
            var e = NewChoice(RandomEventType.StrayCat, 2);
            EmployeeData t = null;
            e.onSetup = () =>
            {
                t = EventBalance.PickPresent();
                if (t == null) { e.cancelled = true; return; }
                e.portraitId = t.portraitId; e.targetEmployeeId = t.id;
                Fill(e, Name(t), Part(t.role));
            };
            e.choices[0].onChoose = () =>
            {
                if (Random.value < 0.5f) EventBalance.ChangeAllSat(5);
                else
                {
                    float d = EventBalance.AddPart(t.role, -EventBalance.ByStage(5.5f, 8.5f, 10f, 14f));
                    UseOutcomeB(e, 0, 2, Name(t), Part(t.role), Score(d));
                }
            };
            e.choices[1].onChoose = () => EventBalance.ChangeSat(Get(t?.id), -5);
            AddIfCharted(pool, e, chart);
        }

        // ⑬ 전설의 프로젝트 USB — 꽂아본다: 50% 랜덤 직원 잠재력 +1단계(S면 개인 버프 +1블록) / 50% 개인 버프 +1블록 · 버린다: X
        {
            var e = NewChoice(RandomEventType.LegendUsb, 2);
            EmployeeData t = null;
            e.onSetup = () =>
            {
                t = EventBalance.PickPresent();
                if (t == null) { e.cancelled = true; return; }
                e.infoEmployeeIds = new List<string> { t.id };
                Fill(e, Name(t), Material(t.role));
            };
            e.choices[0].onChoose = () =>
            {
                string before = t.PotentialToString();
                if (Random.value < 0.5f && EventBalance.RaisePotential(t))
                    Fill(e, ("{이전잠재력}", before), ("{잠재력}", t.PotentialToString()));
                else
                    UseOutcomeB(e, 0, 2, Name(t), Material(t.role), Weeks(EventBalance.Buff(t)));
            };
            AddIfCharted(pool, e, chart);
        }

        // ⑭ 의문의 방문객 (0~25%) — 쫓아낸다: X · 음료수 대접: 50% 도둑(아이템 1개, 없으면 돈 −R × 5%) / 50% 투자자(돈 +R × 7%)
        {
            var e = NewChoice(RandomEventType.MysteryVisitor, 2);
            e.categoryMax = 1;
            e.choices[0].onChoose = () => { if (Random.value < 0.5f) UseOutcomeB(e, 0, 2); };
            e.choices[1].onChoose = () =>
            {
                if (Random.value < 0.5f)
                {
                    int gain = EventBalance.Money(7f);
                    MoneyManager.Instance.AddGold(gain);
                    Fill(e, Cost(gain));
                    return;
                }
                string itemId = ItemManager.Instance?.PickRandomItemId();
                string lost;
                if (!string.IsNullOrEmpty(itemId))
                {
                    ItemManager.Instance.StealSpecificItem(itemId);
                    lost = ItemChartLoader.Cache != null && ItemChartLoader.Cache.TryGetValue(itemId, out var row) ? row.name : itemId;
                }
                else
                {
                    int loss = EventBalance.Money(5f);
                    MoneyManager.Instance.ForceSpendGold(loss, saveImmediately: false);
                    lost = $"{loss:N0} G";
                }
                UseOutcomeB(e, 1, 2, ("{잃은것}", lost));
            };
            AddIfCharted(pool, e, chart);
        }

        // ⑮ 로또 번호 (소형, 가중치 0.5) — 찍어준다: 만족도 +10, 5% 당첨 → 퇴사 · 일이나 해라: 만족도 −10
        {
            var e = NewChoice(RandomEventType.LottoNumber, 2);
            EmployeeData t = null;
            e.onSetup = () =>
            {
                t = EventBalance.PickPresent();
                if (t == null) { e.cancelled = true; return; }
                e.portraitId = t.portraitId; e.targetEmployeeId = t.id;
                Fill(e, Name(t));
            };
            e.choices[0].onChoose = () =>
            {
                var emp = Get(t?.id);
                if (emp == null) return;
                if (Random.value < 0.05f)
                {
                    UseOutcomeB(e, 0, 3, Name(emp));
                    EmployeeManager.Instance.FireEmployee(emp);
                    HUDUI.Instance?.RefreshAll();
                }
                else EventBalance.ChangeSat(emp, 10);
            };
            e.choices[1].onChoose = () => EventBalance.ChangeSat(Get(t?.id), -10);
            AddIfCharted(pool, e, chart);
        }

        // ⑰ 외주 신청 — 맡긴다: 해당 파트 +μ × 10%, 돈 −R × 4.5% · 우리끼리: X  (해당 파트 = 직원 있는 파트 중 랜덤)
        {
            var e = NewChoice(RandomEventType.Outsourcing, 2);
            EmployeeRole part = EmployeeRole.Planner; int cost = 0;
            e.onSetup = () =>
            {
                var p = EventBalance.PickPartWithEmployee();
                if (p == null) { e.cancelled = true; return; }
                part = p.Value;
                cost = EventBalance.Money(4.5f);
                EventBalance.SetCostChoice(e.choices[0], cost);
                Fill(e, Part(part), Job(part), Cost(cost));
            };
            e.choices[0].onChoose = () =>
            {
                MoneyManager.Instance.ForceSpendGold(cost, saveImmediately: false);
                Fill(e, Score(EventBalance.AddPart(part, 10f)));
            };
            AddIfCharted(pool, e, chart);
        }

        // ⑲ 면담 압박 — 빡세게: 개인 버프 +1블록(3단계만 L × 0.8), 만족도 −15 · 다독인다: 만족도 +10
        // 비서가 보고(차트 portrait_secretary), 선택 후 답변은 대상 직원 초상으로 교체
        {
            var e = NewChoice(RandomEventType.PressureTalk, 2);
            EmployeeData t = null;
            e.onSetup = () =>
            {
                t = EventBalance.PickPresent();
                if (t == null) { e.cancelled = true; return; }
                e.infoEmployeeIds = new List<string> { t.id };
                foreach (var c in e.choices) c.resultPortraitId = t.portraitId;
                Fill(e, Name(t));
            };
            e.choices[0].onChoose = () =>
            {
                var emp = Get(t?.id);
                if (emp == null) return;
                EventBalance.ChangeSat(emp, -15);
                Fill(e, Weeks(EventBalance.Buff(emp, EventBalance.Stage == 3 ? 0.8f : 1f)));
            };
            e.choices[1].onChoose = () => EventBalance.ChangeSat(Get(t?.id), 10);
            AddIfCharted(pool, e, chart);
        }

        // ⑳ 완벽주의자 (야근) — 전원 야근: 전 직원 −5, 해당 파트 +μ × 4.5/7.5/9.5/13.5% · 정시 퇴근: X  (해당 파트 = 랜덤 1파트)
        {
            var e = NewChoice(RandomEventType.Perfectionist, 2);
            EmployeeRole part = EmployeeRole.Planner;
            e.onSetup = () =>
            {
                var p = EventBalance.PickPartWithEmployee();
                if (p == null) { e.cancelled = true; return; }
                part = p.Value;
                Fill(e, Part(part));
            };
            e.choices[0].onChoose = () =>
            {
                EventBalance.ChangeAllSat(-5);
                Fill(e, Score(EventBalance.AddPart(part, EventBalance.ByStage(4.5f, 7.5f, 9.5f, 13.5f))));
            };
            AddIfCharted(pool, e, chart);
        }

        // ㉑ 문제 직원 (빌런, 사무실 2단계 이상) — 당장 해고: 대상 퇴사 · 참는다: 전 직원 −5
        // 대상 = 만족도 최하위, 같으면 성수 낮은 직원
        {
            var e = NewChoice(RandomEventType.ProblemEmployee, 2);
            EmployeeData t = null;
            e.onSetup = () =>
            {
                var list = EventBalance.Present();
                if (EventBalance.Stage < 2 || list.Count < 2) { e.cancelled = true; return; }
                list.Sort((a, b) => a.satisfaction != b.satisfaction
                    ? a.satisfaction.CompareTo(b.satisfaction)
                    : a.enhancementLevel.CompareTo(b.enhancementLevel));
                t = list[0];
                e.infoEmployeeIds = new List<string> { t.id };
                Fill(e, Name(t));
            };
            e.choices[0].onChoose = () =>
            {
                var emp = Get(t?.id);
                if (emp == null) return;
                EmployeeManager.Instance.FireEmployee(emp);
                HUDUI.Instance?.RefreshAll();
            };
            e.choices[1].onChoose = () => EventBalance.ChangeAllSat(-5);
            AddIfCharted(pool, e, chart);
        }

        // ㉔ 초심 회복 훈련 — 기초 훈련: 잠재력 +1단계, 강화 단계 −1 · 스트레스 주지 말자: X
        // 대상 자동 선정: 잠재력 S 제외, E(성수−1) 이 R × 5~10% 인 직원
        {
            var e = NewChoice(RandomEventType.BackToBasics, 2);
            EmployeeData t = null;
            e.onSetup = () =>
            {
                float lo = EventBalance.RRef() * 0.05f, hi = EventBalance.RRef() * 0.10f;
                var list = EventBalance.Present(x =>
                    x.potential < EmployeePotential.S && x.enhancementLevel >= 1
                    && x.enhancementLevel - 1 < ExpectedEnhanceStep.Length
                    && ExpectedEnhanceStep[x.enhancementLevel - 1] >= lo
                    && ExpectedEnhanceStep[x.enhancementLevel - 1] <= hi);
                if (list.Count == 0) { e.cancelled = true; return; }
                t = list[Random.Range(0, list.Count)];
                e.infoEmployeeIds = new List<string> { t.id };
                Fill(e, Name(t));
            };
            e.choices[0].onChoose = () =>
            {
                var emp = Get(t?.id);
                if (emp == null || emp.enhancementLevel <= 0) return;
                Fill(e, ("{이전잠재력}", emp.PotentialToString()), ("{이전강화}", emp.enhancementLevel.ToString()));
                // 순서 주의 — 강화 되돌림이 현재 잠재력 보너스로 스탯을 빼므로, 되돌린 뒤에 잠재력을 올린다
                int lv = emp.enhancementLevel;
                EmployeeManager.Instance.ReverseEnhancement(emp, lv);
                emp.enhancementLevel = lv - 1;
                EventBalance.RaisePotential(emp);
                EmployeeListUI.Instance?.RefreshSlotLevelText(emp.id);
                Fill(e, ("{잠재력}", emp.PotentialToString()), ("{강화}", emp.enhancementLevel.ToString()));
            };
            AddIfCharted(pool, e, chart);
        }

        // ⑩ 의문의 택배상자 — 개봉: 50% 랜덤 아이템 1개 + 돈 +R × 1/2.5/3.5/5.5% / 50% 전 직원 −5 · 버린다: X
        // 아이템 후보 = 현재 사무실 단계에 등장하는 아이템 (엑셀 미지정 — 임시 기준)
        {
            var e = NewChoice(RandomEventType.MysteryBox, 2);
            e.choices[0].onChoose = () =>
            {
                if (Random.value < 0.5f) { EventBalance.ChangeAllSat(-5); UseOutcomeB(e, 0, 2); return; }
                int gain = EventBalance.Money(EventBalance.ByStage(1f, 2.5f, 3.5f, 5.5f));
                MoneyManager.Instance.AddGold(gain);
                string itemName = "";
                var items = new List<ItemChartRow>();
                if (ItemChartLoader.Cache != null)
                    foreach (var row in ItemChartLoader.Cache.Values)
                        if (row.appearStages != null && row.appearStages.Contains(EventBalance.Stage.ToString())) items.Add(row);
                if (items.Count > 0)
                {
                    var pick = items[Random.Range(0, items.Count)];
                    ItemManager.Instance?.AddItem(pick.itemId);
                    itemName = pick.name;
                }
                Fill(e, ("{아이템}", itemName), Cost(gain));
            };
            AddIfCharted(pool, e, chart);
        }

        // ⑯ 스카우트 제의 — 정에 호소(만족도 60 이상): 만족도 −10 · 보너스: 돈 −R × 20/7.5/5/6%, 만족도 +10 · 보내준다: 퇴사
        // 보너스 = 그 직원의 Q × 0.8 (EventBalance.EmployeeQ)
        {
            var e = NewChoice(RandomEventType.EmployeeScout, 3);
            EmployeeData t = null; int cost = 0;
            e.onSetup = () =>
            {
                t = EventBalance.PickPresent();
                if (t == null) { e.cancelled = true; return; }
                e.portraitId = t.portraitId; e.targetEmployeeId = t.id;
                e.choices[0].conditionText = "만족도 60 이상";
                e.choices[0].conditionIsItem = true;
                e.choices[0].disabled = t.satisfaction < 60;
                cost = Mathf.Max(1, Mathf.RoundToInt(EventBalance.EmployeeQ(t) * 0.8f));
                EventBalance.SetCostChoice(e.choices[1], cost);
                Fill(e, Name(t), Cost(cost));
            };
            e.choices[0].onChoose = () => EventBalance.ChangeSat(Get(t?.id), -10);
            e.choices[1].onChoose = () =>
            {
                MoneyManager.Instance.ForceSpendGold(cost, saveImmediately: false);
                EventBalance.ChangeSat(Get(t?.id), 10);
            };
            e.choices[2].onChoose = () =>
            {
                var emp = Get(t?.id);
                if (emp == null) return;
                EmployeeManager.Instance.FireEmployee(emp);
                HUDUI.Instance?.RefreshAll();
            };
            AddIfCharted(pool, e, chart);
        }

        // ⑱ 사내 공모전 상금 — 1등 몰아주기: 말한 직원 잠재력 +1단계 · 나눠주기: 전 직원 +5
        // 대상 = 말한 직원 (엑셀 미지정), 잠재력 S 는 대상에서 제외
        {
            var e = NewChoice(RandomEventType.ContestPrize, 2);
            EmployeeData t = null;
            e.onSetup = () =>
            {
                t = EventBalance.PickPresent(x => x.potential < EmployeePotential.S);
                if (t == null) { e.cancelled = true; return; }
                e.portraitId = t.portraitId; e.targetEmployeeId = t.id;
                Fill(e, Name(t));
            };
            e.choices[0].onChoose = () =>
            {
                var emp = Get(t?.id);
                if (emp == null) return;
                string before = emp.PotentialToString();
                EventBalance.RaisePotential(emp);
                Fill(e, ("{이전잠재력}", before), ("{잠재력}", emp.PotentialToString()));
            };
            e.choices[1].onChoose = () => EventBalance.ChangeAllSat(5);
            AddIfCharted(pool, e, chart);
        }

        // ㉒ 직감적 돌진 (0~50%) — 풀파워: 다음 팀장 점수 3번 [강]만, 직원 1명 개인 버프 +½블록 · 천천히: X
        // ㉓ 안전제일   (0~50%) — 풀파워: X · 살살: 다음 팀장 점수 3번 [약]만, 직원 1명 만족도 +10
        // 비서가 말하는 이벤트라 "해당 직원" = 랜덤 1명 (엑셀 미지정), 대상 정보줄로 표시
        foreach (var (type, aim, pick) in new[] { (RandomEventType.GutRush, LeaderScoreAim.High, 0),
                                                  (RandomEventType.SafetyFirst, LeaderScoreAim.Low, 1) })
        {
            var e = NewChoice(type, 2);
            e.categoryMax = 2;
            EmployeeData t = null;
            e.onSetup = () =>
            {
                t = EventBalance.PickPresent();
                if (t == null) { e.cancelled = true; return; }
                e.infoEmployeeIds = new List<string> { t.id };
                Fill(e, Name(t));
            };
            e.choices[pick].onChoose = () =>
            {
                var emp = Get(t?.id);
                if (emp == null) return;
                mgr.ForceLeaderAim(aim, 3);
                if (aim == LeaderScoreAim.High) Fill(e, Weeks(EventBalance.Buff(emp, 0.5f)));
                else EventBalance.ChangeSat(emp, 10);
            };
            AddIfCharted(pool, e, chart);
        }
    }

    // ── 선택지 없는 이벤트 ────────────────────────────────────────
    static RandomEventData NewEvent(RandomEventType type) =>
        new RandomEventData { type = type, weight = 1f, categoryMin = 1, categoryMax = 4 };

    static string RS(string t, params (string k, string v)[] kv) => R(t ?? "", kv);

    public static void RegisterDev(List<RandomEventData> pool, RandomEventManager mgr,
                                   Dictionary<string, RandomEventChartRow> chart)
    {
        // 신년 운세 (1월만) — 50% 전 직원 만족도 +5 / 50% −5. systemMessage 의 {값} → "+5" / "-5"
        {
            var e = NewEvent(RandomEventType.NewYearFortune);
            int delta = 0;
            e.onSetup = () =>
            {
                if (GameTimeManager.Instance == null || GameTimeManager.Instance.Month != 1) { e.cancelled = true; return; }
                delta = Random.value < 0.5f ? 5 : -5;
                e.titleType = delta > 0 ? 2 : 0;
                if (delta < 0 && chart.TryGetValue(e.type.ToString(), out var row) && !string.IsNullOrEmpty(row.description2))
                    e.description = row.description2;
                e.systemMessage = RS(e.systemMessage, ("{값}", delta > 0 ? "+5" : "-5"));
            };
            e.onApply = () => EventBalance.ChangeAllSat(delta);
            AddIfCharted(pool, e, chart);
        }

        // 명당 / 의자 득템 / 최애 게임 — 해당 직원 만족도 +25 / +26 / +27 (기획 원문, 의도적으로 다름)
        foreach (var (type, sat) in new[] { (RandomEventType.LuckySpot, 25), (RandomEventType.ChairLoot, 26), (RandomEventType.FavoriteGame, 27) })
        {
            var e = NewEvent(type);
            e.onSetup = () =>
            {
                var t = EventBalance.PickPresent();
                if (t == null) { e.cancelled = true; return; }
                e.portraitId = t.portraitId; e.targetEmployeeId = t.id;
                e.systemMessage = RS(e.systemMessage, Name(t));
            };
            e.onApply = () => EventBalance.ChangeSat(Get(e.targetEmployeeId), sat);
            AddIfCharted(pool, e, chart);
        }

        // 컨디션 최상 — 해당 직원 개인 버프 +1블록 (+20%, 기간 L)
        {
            var e = NewEvent(RandomEventType.PeakCondition);
            e.onSetup = () =>
            {
                var t = EventBalance.PickPresent();
                if (t == null) { e.cancelled = true; return; }
                e.portraitId = t.portraitId; e.targetEmployeeId = t.id;
                e.systemMessage = RS(e.systemMessage, Name(t), Weeks(EventBalance.PersonalWeeks()));
            };
            e.onApply = () => EventBalance.Buff(Get(e.targetEmployeeId));
            AddIfCharted(pool, e, chart);
        }

        // 건물주 횡포 — 돈 −R × 5%
        {
            var e = NewEvent(RandomEventType.LandlordTyranny);
            int cost = 0;
            e.onSetup = () =>
            {
                cost = EventBalance.Money(5f);
                e.systemMessage = RS(e.systemMessage, Cost(cost));
            };
            e.onApply = () => MoneyManager.Instance.ForceSpendGold(cost);
            AddIfCharted(pool, e, chart);
        }

        // 기획 결함 / 키보드 샷건 / 태블릿 고장 — 해당 파트 −μ × 8~12% (해당 파트 직원 없으면 미등장)
        // 아이디어 폭발 / 기적의 오타 / 금손 모드 — 해당 파트 +μ × 8~12% (해당 파트 팀장 세션 이후에만)
        var partEvents = new (RandomEventType type, EmployeeRole role, int sign)[]
        {
            (RandomEventType.PlanningFlaw,    EmployeeRole.Planner,    -1),
            (RandomEventType.KeyboardShotgun, EmployeeRole.Programmer, -1),
            (RandomEventType.TabletBroken,    EmployeeRole.Artist,     -1),
            (RandomEventType.IdeaExplosion,   EmployeeRole.Planner,    +1),
            (RandomEventType.MiracleTypo,     EmployeeRole.Programmer, +1),
            (RandomEventType.GoldenHand,      EmployeeRole.Artist,     +1),
        };
        foreach (var (type, role, sign) in partEvents)
        {
            var e = NewEvent(type);
            float pct = 0f;
            e.onSetup = () =>
            {
                var t = EventBalance.PickPresent(x => x.role == role);
                bool leaderDone = DevelopmentManager.Instance != null && DevelopmentManager.Instance.GetLeaderBonusByRole(role) > 0f;
                if (t == null || (sign > 0 && !leaderDone)) { e.cancelled = true; return; }
                e.portraitId = t.portraitId; e.targetEmployeeId = t.id;
                pct = sign * Random.Range(8f, 12f);
                float preview = EventBalance.PartMu(role) * pct / 100f;
                e.systemMessage = RS(e.systemMessage, Name(t), Part(role), Score(preview));
            };
            e.onApply = () => EventBalance.AddPart(role, pct);
            AddIfCharted(pool, e, chart);
        }
    }
}
