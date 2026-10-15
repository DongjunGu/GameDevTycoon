// 뒤끝 펑션 "gacha" — 상점 뽑기 서버 처리 (다이아 차감 + 추첨 + 천장 + 지급)
// Visual Studio 뒤끝펑션 템플릿 프로젝트의 Function.cs 를 이 파일로 교체해서 빌드/배포한다.
// (템플릿이 using 을 더 갖고 있으면 그대로 두면 됨)
//
// 클라이언트 호출: Backend.BFunc.InvokeFunction("gacha", param)
//   param: kind("employee"|"trait"), special(bool), count(1|10)
// 반환(JSON 문자열):
//   성공 { ok:true, diamond, pityEmployeeSpecial, pityTraitSpecial, cardsJson|ownedJson, draws:[{id,grade}] }
//   실패 { ok:false, error:"NotEnoughDiamond" | ... }
//
// 등급은 뒤끝 확률(Probability) 테이블로 뽑고, 같은 등급 안에서는 차트(EmployeeMasterData / Trait)에서 균등.
// 확률 테이블 컬럼: grade (직원 0=Normal/1=Rare/2=Epic, 특성 "C"/"B"/"A"/"S") + percent
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using BackEnd;
using BackEnd.Content;
using LitJson;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BackendFunction
{
    public class BFunc
    {
        // ── 뒤끝 콘솔 확률 테이블 fileId — 콘솔에서 만든 뒤 값 채울 것 ──
        const string ProbEmployeeNormal  = "20264";
        const string ProbEmployeeSpecial = "20267";
        const string ProbTraitNormal     = "20265";
        const string ProbTraitSpecial    = "20266";

        // ── 가격(다이아) / 천장 — 클라이언트 ShopPanelUI 표시값과 맞출 것 ──
        const int EmployeeNormalCost   = 60;
        const int EmployeeSpecialCost  = 250;
        const int TraitNormalCost      = 100;
        const int TraitSpecialCost     = 400;
        const int EmployeeSpecialPity  = 10; // N회째 에픽 확정
        const int TraitSpecialPity     = 30; // N회째 S 확정

        const int EpicGrade = 2;  // EmployeeGrade.Epic
        const int TraitS    = 3;  // TraitGrade.S (C=0,B=1,A=2,S=3)

        static readonly Random Rng = new Random();

        public Stream Function(Stream stream, ILambdaContext context)
        {
            try
            {
                Backend.Initialize(ref stream);
            }
            catch (Exception e)
            {
                return Fail("initialize " + e);
            }

            try
            {
                return Backend.StringToStream(Run().ToJson());
            }
            catch (Exception e)
            {
                return Fail(e.Message);
            }
        }

        JsonData Run()
        {
            // 상점 열 때 클라가 보내는 예열 호출 — 콜드 스타트 + 차트 다운로드를 뽑기 전에 미리 소모
            if (Backend.HasKey("kind") && Backend.Content["kind"].ToString() == "ping")
            {
                Task.WaitAll(Task.Run(() => LoadChart("EmployeeMasterData", "employeeId", null)),
                             Task.Run(() => LoadChart("Trait", "traitId", "grade")));
                var pong = new JsonData();
                pong["ok"] = true;
                return pong;
            }

            if (!Backend.HasKey("kind") || !Backend.HasKey("special") || !Backend.HasKey("count"))
                return Error("BadParam");

            string kind  = Backend.Content["kind"].ToString();
            bool special = Backend.Content["special"].ToString().ToLower() == "true";
            int count    = int.TryParse(Backend.Content["count"].ToString(), out var c) ? c : 0;
            bool isEmp   = kind == "employee";
            if ((!isEmp && kind != "trait") || (count != 1 && count != 10)) return Error("BadParam");

            int unit = isEmp ? (special ? EmployeeSpecialCost : EmployeeNormalCost)
                             : (special ? TraitSpecialCost    : TraitNormalCost);
            int cost = unit * count;

            // Initialize 가 요청 스트림에서 채워주는 값 — 비어 있을 때만 서버 조회
            string owner = Backend.UserInDate;
            if (string.IsNullOrEmpty(owner))
                owner = Backend.BMember.GetUserInfo().GetReturnValuetoJSON()["row"]["inDate"].ToString();

            var sw = Stopwatch.StartNew();
            var ms = new List<string>();
            void Mark(string step) { ms.Add(step + "=" + sw.ElapsedMilliseconds); }

            // 0) 서로 독립인 조회 4개를 동시에 — 다이아 차감 전이라 여기서 실패해도 손해 없음
            string fileId = isEmp ? (special ? ProbEmployeeSpecial : ProbEmployeeNormal)
                                  : (special ? ProbTraitSpecial    : ProbTraitNormal);
            var curTask   = Task.Run(() => GetRow("OutGameCurrency", owner));
            var ownedTask = Task.Run(() => GetRow(isEmp ? "OwnedCard" : "OwnedTrait", owner));
            var probTask  = Task.Run(() => Backend.Probability.GetProbabilitys(fileId, count));
            var chartTask = Task.Run(() => isEmp ? LoadChart("EmployeeMasterData", "employeeId", null)
                                                 : LoadChart("Trait", "traitId", "grade"));
            Task.WaitAll(curTask, ownedTask, probTask, chartTask);
            Mark("prefetch");

            var cur = curTask.Result;
            if (cur == null) return Error("NoCurrencyRow");
            string curInDate = cur["inDate"].ToString();
            if (Int(cur, "diamond") < cost) return Error("NotEnoughDiamond");

            var probBro = probTask.Result;
            if (!probBro.IsSuccess()) return Error("ProbabilityFailed " + probBro);
            var grades = new List<int>(count);
            var elements = probBro.GetFlattenJSON()["elements"];
            for (int i = 0; i < elements.Count; i++)
                grades.Add(ParseGrade(elements[i]["grade"].ToString()));

            var chart = chartTask.Result;
            if (chart.Count == 0) return Error(isEmp ? "EmptyEmployeeChart" : "EmptyTraitChart");
            var row = ownedTask.Result;
            if (row == null) return Error(isEmp ? "NoOwnedCardRow" : "NoOwnedTraitRow");

            // 1) 다이아 차감 — 계산 업데이트로 원자적 감소 후 음수면 환불 (동시 요청으로 초과 사용 방지)
            var sub = new Param();
            sub.AddCalculation("diamond", GameInfoOperator.subtraction, cost);
            var subBro = Backend.GameData.UpdateWithCalculationV2("OutGameCurrency", curInDate, owner, sub);
            if (!subBro.IsSuccess()) return Error("DeductFailed " + subBro);
            Mark("deduct");

            cur = GetRow("OutGameCurrency", owner);
            Mark("recheck");
            if (cur == null || Int(cur, "diamond") < 0)
            {
                var refund = new Param();
                refund.AddCalculation("diamond", GameInfoOperator.addition, cost);
                Backend.GameData.UpdateWithCalculationV2("OutGameCurrency", curInDate, owner, refund);
                return Error("NotEnoughDiamond");
            }

            // 2) 등급 → 항목 (차트에서 같은 등급 균등) + 천장
            var draws  = new JsonData(); draws.SetJsonType(JsonType.Array);
            int pityE  = Int(cur, "pityEmployeeSpecial");
            int pityT  = Int(cur, "pityTraitSpecial");
            var result = new JsonData();
            Param grant;
            string grantTable;

            if (isEmp)
            {
                var pool = chart.ConvertAll(x => x.id);
                var cards = ParseArray(Str(row, "cardsJson"));

                foreach (int rolled in grades)
                {
                    int g = rolled;
                    if (special)
                    {
                        // 천장: 에픽 없이 (pity-1)회 연속이면 이번 회차는 에픽 확정
                        if (g != EpicGrade && pityE + 1 >= EmployeeSpecialPity) g = EpicGrade;
                        pityE = g == EpicGrade ? 0 : pityE + 1;
                    }
                    string emp = pool[Rng.Next(pool.Count)];
                    AddCount(cards, x => Str(x, "e") == emp && Int(x, "g") == g && Int(x, "s") == 0,
                             () => { var n = new JsonData(); n["e"] = emp; n["g"] = g; n["s"] = 0; n["n"] = 0; return n; });
                    draws.Add(Draw(emp, g));
                }

                grant = new Param();
                grant.Add("cardsJson", cards.ToJson());
                grantTable = "OwnedCard";
                result["cardsJson"] = cards.ToJson();
            }
            else
            {
                // 등급별 버킷 (C=0 ~ S=3)
                var byGrade = new List<string>[4];
                for (int g = 0; g < 4; g++) byGrade[g] = new List<string>();
                foreach (var kv in chart)
                    byGrade[kv.grade].Add(kv.id);

                var owned = ParseArray(Str(row, "ownedJson"));

                foreach (int rolled in grades)
                {
                    int g = rolled;
                    if (special)
                    {
                        // 천장: S 없이 (pity-1)회 연속이면 이번 회차는 S 확정
                        if (g != TraitS && byGrade[TraitS].Count > 0 && pityT + 1 >= TraitSpecialPity) g = TraitS;
                        pityT = g == TraitS ? 0 : pityT + 1;
                    }
                    // 차트에 그 등급 특성이 없으면 아래 등급으로 (확률 테이블과 차트 불일치 방어)
                    while (g > 0 && byGrade[g].Count == 0) g--;
                    if (byGrade[g].Count == 0) return Refund(curInDate, owner, cost, "EmptyTraitChart");

                    string t = byGrade[g][Rng.Next(byGrade[g].Count)];
                    AddCount(owned, x => Str(x, "t") == t,
                             () => { var n = new JsonData(); n["t"] = t; n["n"] = 0; return n; });
                    draws.Add(Draw(t, g));
                }

                grant = new Param();
                grant.Add("ownedJson", owned.ToJson());
                grantTable = "OwnedTrait";
                result["ownedJson"] = owned.ToJson();
            }

            // 3) 지급 + 천장 저장 동시 — ponytail: 동시 요청이면 천장 카운트는 마지막 쓰기가 이김. 문제되면 계산 업데이트로 교체
            string rowInDate = row["inDate"].ToString();
            var grantTask = Task.Run(() => Backend.GameData.UpdateV2(grantTable, rowInDate, owner, grant));
            Task pityTask = Task.CompletedTask;
            if (special)
            {
                var pp = new Param();
                if (isEmp) pp.Add("pityEmployeeSpecial", pityE); else pp.Add("pityTraitSpecial", pityT);
                pityTask = Task.Run(() => Backend.GameData.UpdateV2("OutGameCurrency", curInDate, owner, pp));
            }
            Task.WaitAll(grantTask, pityTask);
            Mark("grant");
            if (!grantTask.Result.IsSuccess()) return Refund(curInDate, owner, cost, "GrantFailed " + grantTask.Result);

            result["ms"] = string.Join(" ", ms);
            result["ok"] = true;
            result["diamond"] = Int(cur, "diamond");
            result["pityEmployeeSpecial"] = pityE;
            result["pityTraitSpecial"] = pityT;
            result["draws"] = draws;
            return result;
        }

        // ── 헬퍼 ──

        static JsonData GetRow(string table, string owner)
        {
            var where = new Where();
            where.Equal("owner_inDate", owner);
            var bro = Backend.GameData.Get(table, where);
            if (!bro.IsSuccess()) return null;
            var rows = bro.FlattenRows();
            return rows.Count > 0 ? rows[0] : null;
        }

        // 웜 인스턴스 재사용 시 차트 재다운로드 생략. 키 = 차트명 + 차트 fileId (콘솔에서 차트 교체하면 fileId 바뀌어 자동 갱신)
        static readonly Dictionary<string, List<(string id, int grade)>> ChartCache = new Dictionary<string, List<(string, int)>>();

        // 차트 목록(어떤 차트가 어떤 fileId 인지) — 60초 재사용. 콘솔에서 차트 교체 시 최대 60초 뒤 반영
        static List<ContentTableItem> _tables;
        static DateTime _tablesAt;
        static readonly object TablesLock = new object();

        static List<ContentTableItem> GetChartTables()
        {
            lock (TablesLock)
            {
                if (_tables != null && (DateTime.UtcNow - _tablesAt).TotalSeconds < 60) return _tables;
                var bro = Backend.CDN.Content.Table.Get();
                if (!bro.IsSuccess()) return _tables; // 실패 시 이전 목록이라도 사용
                _tables = bro.GetContentTableItemList();
                _tablesAt = DateTime.UtcNow;
                return _tables;
            }
        }

        // 차트 전체 행의 idCol 목록. gradeCol 주면 등급도 같이 (특성 S/A/B/C → 3/2/1/0)
        static List<(string id, int grade)> LoadChart(string chartName, string idCol, string gradeCol)
        {
            var list = new List<(string, int)>();
            var tables = GetChartTables();
            if (tables == null) return list;
            ContentTableItem item = null;
            foreach (var t in tables) if (t.chartName == chartName) { item = t; break; }
            if (item == null) return list;

            string key = chartName + "/" + item.selectedChartFileId;
            lock (ChartCache) if (ChartCache.TryGetValue(key, out var cached)) return cached;

            // 필요한 차트 하나만 받음 (전체 차트 다운로드가 가장 느린 구간이었음)
            var contentBro = Backend.CDN.Content.Get(new List<ContentTableItem> { item });
            if (!contentBro.IsSuccess()) return list;
            var dic = contentBro.GetContentDictionarySortByChartId();
            if (!dic.ContainsKey(item.chartId)) return list;

            var rows = dic[item.chartId].contentJson;
            for (int i = 0; i < rows.Count; i++)
            {
                string id = Str(rows[i], idCol);
                if (string.IsNullOrEmpty(id)) continue;
                list.Add((id, gradeCol == null ? 0 : ParseGrade(Str(rows[i], gradeCol))));
            }
            if (list.Count > 0) lock (ChartCache) ChartCache[key] = list;
            return list;
        }

        // 직원 확률은 숫자(0/1/2), 특성은 C/B/A/S
        static int ParseGrade(string s)
        {
            s = (s ?? "").Trim().ToUpper();
            switch (s)
            {
                case "C": return 0;
                case "B": return 1;
                case "A": return 2;
                case "S": return 3;
            }
            return int.TryParse(s, out var g) ? g : 0;
        }

        static JsonData ParseArray(string json)
        {
            JsonData arr = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { arr = JsonMapper.ToObject(json); } catch { arr = null; }
            }
            if (arr == null || !arr.IsArray) { arr = new JsonData(); arr.SetJsonType(JsonType.Array); }
            return arr;
        }

        static void AddCount(JsonData arr, Func<JsonData, bool> match, Func<JsonData> create)
        {
            for (int i = 0; i < arr.Count; i++)
                if (match(arr[i])) { arr[i]["n"] = Int(arr[i], "n") + 1; return; }
            var item = create();
            item["n"] = 1;
            arr.Add(item);
        }

        static JsonData Draw(string id, int grade)
        {
            var d = new JsonData();
            d["id"] = id;
            d["grade"] = grade;
            return d;
        }

        static string Str(JsonData r, string k) => r != null && r.ContainsKey(k) && r[k] != null ? r[k].ToString() : "";
        static int Int(JsonData r, string k) => int.TryParse(Str(r, k), out var v) ? v : 0;

        static JsonData Refund(string curInDate, string owner, int cost, string error)
        {
            var p = new Param();
            p.AddCalculation("diamond", GameInfoOperator.addition, cost);
            Backend.GameData.UpdateWithCalculationV2("OutGameCurrency", curInDate, owner, p);
            return Error(error);
        }

        static JsonData Error(string error)
        {
            var e = new JsonData();
            e["ok"] = false;
            e["error"] = error;
            return e;
        }

        static Stream Fail(string error) => Backend.StringToStream(Error(error).ToJson());
    }
}
