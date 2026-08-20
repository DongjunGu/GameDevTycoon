using System.Collections.Generic;
using UnityEngine;

// 도전과제 종류 — 팀장 점수 95존 / 팀장 점수 99존 / 파트 총점. 프로젝트 시작마다 셋 중 하나를 동일 확률로 제시.
public enum ChallengeKind { LeaderZone95, LeaderZone99, PartTotal }

// 도전과제 시스템 — 도전과제.xlsx/csv 명세 구현.
// 프로젝트 시작(기획 팀장 선택 직전)에 종류+도전 파트+보상 파트를 랜덤 확정하고 공지한다.
// 도전 파트의 팀장이 확정되는 순간(리더존) 또는 개발 100% 완료 시점(파트총점)에 목표와 실제값을 비교해 판정하고,
// 성공하면 보상 파트에 점수+만족도를 지급한다. DevelopmentManager 가 소유(GameObject 불필요, 순수 C# 클래스).
public class ChallengeManager
{
    // ── 잠재력 배수 MUL_pot — DevelopmentManager.GetLeaderPotentialP 와 동일 값(도전과제 전용 사본) ──
    static float MulPot(EmployeePotential p) => p switch
    {
        EmployeePotential.C => 0.962f,
        EmployeePotential.B => 1.000f,
        EmployeePotential.A => 1.038f,
        EmployeePotential.S => 1.068f,
        _ => 1f
    };

    // K(주스탯) — 팀장점수 계수. K_tick(주스탯) — 틱 계수. (기존 팀장점수/개발틱 공식과 동일 상수)
    static float K(int skill) => 0.8738f + 0.026409f * Mathf.Pow(Mathf.Max(0, skill), 0.9081f);
    static float KTick(int skill) => 1.7266f + 0.054105f * Mathf.Pow(Mathf.Max(0, skill), 0.9076f);

    const float MuH = 7.02f;
    const float SigmaH2 = 4.933044f;

    // CBAND(성수구간) 인덱스: 0=0~9, 1=10, 2=11~18, 3=19~20, 4=21~25
    static int CBand(int enhanceLevel)
    {
        int lv = Mathf.Clamp(enhanceLevel, 0, 25);
        if (lv <= 9) return 0;
        if (lv == 10) return 1;
        if (lv <= 18) return 2;
        if (lv <= 20) return 3;
        return 4;
    }
    static readonly float[] MuG    = { 15.095f, 15.197f, 14.958f, 14.610f, 14.302f };
    static readonly float[] SigmaG = { 2.6f, 2.3f, 2.05f, 2.5f, 2.2f };
    static readonly float[] ZLo    = { 0.19f, 0.22f, 0.35f, 0.44f, 0.44f };
    static readonly float[] ZHi    = { 0.45f, 0.49f, 0.64f, 0.73f, 0.73f };

    // 팀장점수(95/99존) 도전과제 전용 상수 — BASE_REF[단계 1~3], c[성수구간(5band), 티어(0=95/1=99)]
    static readonly float[] BaseRef = { 13.6f, 13.8f, 13.9f };
    static readonly float[,] CCorrection = {
        { 0.99f, 0.89f },
        { 0.97f, 0.88f },
        { 1.01f, 0.92f },
        { 1.00f, 0.93f },
        { 1.00f, 0.98f },
    };
    // f_target 범위 — 성수 3band(0=0~10/1=11~20/2=21~25) × 티어(0=95/1=99)
    static readonly float[,] FLo = { { 7.46f, 29.60f }, { 6.06f, 23.02f }, { 4.28f, 15.14f } };
    static readonly float[,] FHi = { { 11.19f, 44.40f }, { 9.10f, 34.53f }, { 6.43f, 22.72f } };
    static int FBand3(int enhanceLevel)
    {
        int lv = Mathf.Clamp(enhanceLevel, 0, 25);
        if (lv <= 10) return 0;
        if (lv <= 20) return 1;
        return 2;
    }
    static int StageOf(int enhanceLevel)
    {
        int lv = Mathf.Clamp(enhanceLevel, 0, 25);
        if (lv <= 9) return 1;
        if (lv <= 18) return 2;
        return 3;
    }

    // 보상률 범위(%) — 수준(0=95/1=총점/2=99) 별. 보상률 = Uniform(Lo,Hi) × OUTGAME_MUL
    static readonly float[] RewardRateLo = { 3.00f, 6.00f, 12.00f };
    static readonly float[] RewardRateHi = { 5.00f, 10.00f, 20.00f };
    const float OutgameMul = 1f;

    // ── 상태 ──
    bool _active;
    ChallengeKind _kind;
    LeaderType _challengePart;
    LeaderType _rewardPart;

    bool _targetComputed;
    float _targetValue;

    bool _rewardComputed;
    float _rewardScore;

    bool _resolved;
    bool _succeeded;
    bool _rewardApplied;

    // ── 외부(테스트 UI 등) 조회용 읽기전용 상태 ──
    public bool IsActive => _active;
    public ChallengeKind Kind => _kind;
    public LeaderType ChallengePart => _challengePart;
    public LeaderType RewardPart => _rewardPart;
    public bool TargetComputed => _targetComputed;
    public float TargetValue => _targetValue;
    public bool RewardComputed => _rewardComputed;
    public float RewardScore => _rewardScore;
    public bool Resolved => _resolved;
    public bool Succeeded => _succeeded;
    public bool RewardApplied => _rewardApplied;
    public static string PartDisplayName(LeaderType t) => PartName(t);
    public static string PartSpriteAsset(LeaderType t) => t switch
    {
        LeaderType.Planner => "PlanIconLSpriteAsset",
        LeaderType.Programmer => "DevIconLSpriteAsset",
        LeaderType.Artist => "ArtIconLSpriteAsset",
        _ => ""
    };
    public static string PartSpriteName(LeaderType t) => t switch
    {
        LeaderType.Planner => "planL",
        LeaderType.Programmer => "devL",
        LeaderType.Artist => "artL",
        _ => ""
    };
    // 도전 파트의 현재 실제값 — DevelopmentPanelUI 파트 점수(진행 중에도 조회 가능).
    public float GetChallengeCurrentValue() => GetCurrentValueForPart(_challengePart);

    // 임의 파트의 현재 실제값 — 도전 파트가 아닌 다른 두 파트도 조회할 때 사용(예: MissionAlertUI가 3파트를
    // 한번에 나란히 보여줄 때). Kind와 무관하게 항상 DevelopmentPanelUI 쪽 값(GetActualPartTotal)만 본다 —
    // ClaimReward()가 Kind 무관 항상 그쪽에만 보상을 더하므로, 여기서도 같은 값을 봐야 보상 수령 후
    // CurrentScorePanel이 실제로 오른 걸 보여준다(2026-08-20, 리더존일 때 GetActualLeaderTotal을 따로
    // 보던 걸 통일 — 보상이 반영 안 되는 값을 보여주고 있었음).
    public float GetCurrentValueForPart(LeaderType part) => GetActualPartTotal(part);

    // 프로젝트 개발 시작(기획 팀장 선택 직전) — 종류/도전 파트/보상 파트를 랜덤 확정하고 공지 알림을 띄운다.
    // onClosed: 알림을 닫은 뒤 이어갈 콜백(기획 팀장 선택 패널 오픈 등).
    public void RollNew(System.Action onClosed)
    {
        _active = true;
        _kind = (ChallengeKind)Random.Range(0, 3);
        _challengePart = PickChallengePart();
        var others = new List<LeaderType>();
        foreach (LeaderType t in System.Enum.GetValues(typeof(LeaderType)))
            if (t != _challengePart) others.Add(t);
        _rewardPart = others[Random.Range(0, others.Count)];

        // "그 파트 팀장 스탯"은 공식으로 뽑힌 사람이 아니라 그 역할 보유 직원 중 최고 스탯(GetBestCandidate)을
        // 쓰기로 했으므로, 아무도 아직 리더로 선택되지 않은 판 시작 시점에 목표·보상을 전부 미리 확정할 수 있다.
        _targetValue = _kind == ChallengeKind.PartTotal
            ? CalcPartTotalTarget(_challengePart)
            : CalcLeaderZoneTarget(_challengePart, _kind);
        _targetComputed = true;

        _rewardScore = CalcRewardScore();
        _rewardComputed = true;

        _resolved = false; _succeeded = false; _rewardApplied = false;

        AlertUI.Instance?.Show(BuildAnnounceText(), onClosed);
    }

    // CEO 제외 그 파트 직원이 1명이라도 있는 파트 중에서만 랜덤 — 예: 기획/개발엔 있고 아트엔 없으면 기획/개발
    // 중에서만 뽑는다. 아무 파트에도 직원이 없으면(초반) 완전 랜덤 — 이 경우 목표/보상은 CEO 스탯으로 계산된다.
    static LeaderType PickChallengePart()
    {
        var eligible = new List<LeaderType>();
        foreach (LeaderType t in System.Enum.GetValues(typeof(LeaderType)))
            if (HasNonCeoEmployee(t)) eligible.Add(t);

        if (eligible.Count > 0) return eligible[Random.Range(0, eligible.Count)];

        var all = (LeaderType[])System.Enum.GetValues(typeof(LeaderType));
        return all[Random.Range(0, all.Length)];
    }

    static bool HasNonCeoEmployee(LeaderType t)
    {
        var role = ToRole(t);
        foreach (var e in EmployeeManager.Instance.ownedEmployees)
            if (!e.isCEO && e.role == role) return true;
        return false;
    }

    static string PartName(LeaderType t) => t switch
    {
        LeaderType.Planner => "기획",
        LeaderType.Programmer => "개발",
        LeaderType.Artist => "아트",
        _ => ""
    };
    string KindName() => _kind switch
    {
        ChallengeKind.LeaderZone95 => "팀장 점수 95존",
        ChallengeKind.LeaderZone99 => "팀장 점수 99존",
        ChallengeKind.PartTotal    => "파트 총점",
        _ => ""
    };
    string BuildAnnounceText()
    {
        if (_kind == ChallengeKind.LeaderZone95 || _kind == ChallengeKind.LeaderZone99)
            return $"{PartName(_challengePart)}팀장 점수 {Mathf.RoundToInt(_targetValue)}점 달성하기";

        return $"{PartName(_challengePart)} 총점{Mathf.RoundToInt(_targetValue)}점 이상 달성하기";
    }

    static EmployeeRole ToRole(LeaderType t) => t switch
    {
        LeaderType.Planner => EmployeeRole.Planner,
        LeaderType.Programmer => EmployeeRole.Programmer,
        LeaderType.Artist => EmployeeRole.Artist,
        _ => EmployeeRole.Planner
    };

    // part 기준 스탯 조회 — emp.role이 아니라 part로 어떤 스탯을 볼지 정한다. 일반 직원은 role이 항상 part와
    // 같으니 결과가 같지만, CEO 폴백(role은 고정 Planner인데 세 스탯을 다 보유)일 때 이게 있어야 정확히 계산된다.
    static int GetStatForPart(EmployeeData emp, LeaderType part) => part switch
    {
        LeaderType.Planner => emp.EffectivePlanningSkill,
        LeaderType.Programmer => emp.EffectiveDevelopSkill,
        LeaderType.Artist => emp.EffectiveArtSkill,
        _ => 0
    };

    // "그 파트 팀장 스탯" = 이번 프로젝트에 공식으로 뽑힌 사람이 아니라, 그 역할 보유 직원 중 주스탯이
    // 제일 높은 사람의 스탯. 이 정의 덕분에 목표·보상 전부 아무도 리더로 선택되기 전(판 시작 시점)에 확정 가능.
    // 그 파트에 CEO 제외 직원이 아무도 없으면 CEO로 대체(있다면).
    static EmployeeData GetBestCandidate(LeaderType t)
    {
        var role = ToRole(t);
        EmployeeData best = null;
        foreach (var e in EmployeeManager.Instance.ownedEmployees)
        {
            if (e.isCEO || e.role != role) continue;
            if (best == null || e.GetEffectiveMainStat() > best.GetEffectiveMainStat()) best = e;
        }
        // CEO는 ownedEmployees에 안 들어있다(EmployeeManager.CEO가 별도 보관) — 위에서 못 찾았으면 여기서 폴백.
        return best ?? EmployeeManager.Instance.CEO;
    }

    // μ(팀장 기여 + 틱 기여), σ 동시 산출 — 파트총점 도전과제 목표 및 보상 점수 산출에 공용으로 쓰인다.
    static (float mu, float sigma) CalcMuSigma(LeaderType part, EmployeeData leader)
    {
        if (leader == null) return (0f, 0f);
        var role = ToRole(part);
        int leaderSkill = GetStatForPart(leader, part);
        int cband = CBand(leader.enhancementLevel);

        float leaderK = K(leaderSkill) * MulPot(leader.potential);
        float leaderMu = leaderK * MuG[cband];
        float leaderVar = Mathf.Pow(leaderK * SigmaG[cband], 2f);

        float tickSum = 0f, tickSqSum = 0f;
        foreach (var emp in EmployeeManager.Instance.ownedEmployees)
        {
            if (emp.isCEO || emp.role != role) continue;
            float kt = KTick(emp.GetEffectiveMainStat());
            tickSum += kt;
            tickSqSum += kt * kt;
        }

        float mu = leaderMu + tickSum * MuH;
        float sigma = Mathf.Sqrt(leaderVar + tickSqSum * SigmaH2);
        return (mu, sigma);
    }

    // 팀장점수(95/99존) 도전과제 목표 = K×P×BASE_REF[단계]×c[성수구간,티어]×(1+f_target/100)
    static float CalcLeaderZoneTarget(LeaderType part, ChallengeKind kind)
    {
        var leader = GetBestCandidate(part);
        if (leader == null) return float.MaxValue;
        int skill = GetStatForPart(leader, part);
        int stage = StageOf(leader.enhancementLevel);
        int cband5 = CBand(leader.enhancementLevel);
        int tier = kind == ChallengeKind.LeaderZone99 ? 1 : 0;

        float baseRef = BaseRef[Mathf.Clamp(stage - 1, 0, 2)];
        float c = CCorrection[cband5, tier];

        int fband = FBand3(leader.enhancementLevel);
        float fTarget = Random.Range(FLo[fband, tier], FHi[fband, tier]);

        return K(skill) * MulPot(leader.potential) * baseRef * c * (1f + fTarget / 100f);
    }

    // 파트 총점 도전과제 목표 = μ + z·σ
    static float CalcPartTotalTarget(LeaderType part)
    {
        var leader = GetBestCandidate(part);
        if (leader == null) return float.MaxValue;
        var (mu, sigma) = CalcMuSigma(part, leader);
        int cband = CBand(leader.enhancementLevel);
        float z = Random.Range(ZLo[cband], ZHi[cband]);
        return mu + z * sigma;
    }

    float CalcRewardScore()
    {
        var (mu, _) = CalcMuSigma(_rewardPart, GetBestCandidate(_rewardPart));
        int tierIdx = _kind switch
        {
            ChallengeKind.LeaderZone95 => 0,
            ChallengeKind.PartTotal    => 1,
            ChallengeKind.LeaderZone99 => 2,
            _ => 0
        };
        float rate = Random.Range(RewardRateLo[tierIdx], RewardRateHi[tierIdx]) * OutgameMul;
        return mu * rate / 100f;
    }

    static float GetActualLeaderTotal(LeaderType part) => part switch
    {
        LeaderType.Planner => DevelopmentManager.Instance.LeaderPlanningBonusTotal,
        LeaderType.Programmer => DevelopmentManager.Instance.LeaderDevelopBonusTotal,
        LeaderType.Artist => DevelopmentManager.Instance.LeaderArtBonusTotal,
        _ => 0f
    };
    static float GetActualPartTotal(LeaderType part) => part switch
    {
        LeaderType.Planner => DevelopmentPanelUI.Instance.GetPlanning(),
        LeaderType.Programmer => DevelopmentPanelUI.Instance.GetDevelop(),
        LeaderType.Artist => DevelopmentPanelUI.Instance.GetArt(),
        _ => 0f
    };

    // 파트총점 도전과제는 원래 개발 100% 완료 시점(OnDevelopmentComplete)에만 판정했지만, 목표를 개발
    // 도중에 이미 넘겼다면 완료까지 기다리지 않고 그 즉시 확정한다 — 매 틱(AccumulateByType) 뒤에 호출.
    // 반환값 true = "이번 호출로 새로 성공 확정"됨(호출자가 MissionAlertUI를 그 자리에서 띄우는 신호).
    public bool TryResolvePartTotalEarly()
    {
        if (!_active || _resolved || _kind != ChallengeKind.PartTotal) return false;
        if (GetActualPartTotal(_challengePart) < _targetValue) return false;
        Resolve(true);
        return true;
    }

    // 해당 파트 팀장점수 총합이 실제로 확정된 직후(DevelopmentManager 가 호출) — 팀장점수(95/99존) 도전과제만
    // 여기서 판정한다(목표는 RollNew에서 판 시작 시점에 이미 확정돼 있음). 파트총점은 OnDevelopmentComplete에서 판정.
    public void OnLeaderScoreFinalized(LeaderType type)
    {
        if (!_active || _resolved) return;
        if (type != _challengePart || _kind == ChallengeKind.PartTotal) return;

        Resolve(GetActualLeaderTotal(type) >= _targetValue);
    }

    // 개발 100% 완료 시점(DevelopmentManager.OnDevelopmentComplete) — 파트총점 도전과제만 여기서 판정한다.
    public void OnDevelopmentComplete()
    {
        if (!_active || _resolved) return;
        if (_kind != ChallengeKind.PartTotal) return;

        Resolve(GetActualPartTotal(_challengePart) >= _targetValue);
    }

    // 성공/실패 판정만 조용히 확정. MissionAlertUI를 언제 띄울지는 DevelopmentManager가 결정한다 —
    // 팀장점수(95/99존)는 4회차 연출이 끝나 confirmBtn이 활성화되는 시점(OnRoundsVisualComplete)에
    // LeaderScoreUI 패널이 열려있는 채로 띄우고, 파트총점은 "개발 완료!" 얼럿 이후·"창의성을 올리세요!"
    // 얼럿 이전(ShowCreativityGame)에 띄운다.
    // 보상은 더 이상 자동 지급되지 않고 MissionAlertUI의 ReceiveRewardBtn을 눌러야만(ClaimReward) 지급된다.
    void Resolve(bool succeeded)
    {
        _resolved = true;
        _succeeded = succeeded;
    }

    // MissionAlertUI.ReceiveRewardBtn 클릭 시에만 호출 — 보상 점수 + 만족도를 실제로 지급.
    public void ClaimReward()
    {
        if (!_resolved || !_succeeded || _rewardApplied) return;
        _rewardApplied = true;

        switch (_rewardPart)
        {
            case LeaderType.Planner:    DevelopmentPanelUI.Instance.AddValuesInstant(_rewardScore, 0f, 0f, 0f); break;
            case LeaderType.Programmer: DevelopmentPanelUI.Instance.AddValuesInstant(0f, _rewardScore, 0f, 0f); break;
            case LeaderType.Artist:     DevelopmentPanelUI.Instance.AddValuesInstant(0f, 0f, _rewardScore, 0f); break;
        }

        ApplySatisfactionReward();

        // [[feedback_save_4set_pattern]] — 점수(프로젝트)/만족도(직원) 상태가 바뀌었으므로 4-set 저장.
        MoneyManager.Instance?.SaveMoney();
        GameTimeManager.Instance?.SaveGameTime();   // 내부에서 SaveAllEmployees() 호출
        ProjectSaveManager.Instance?.SaveProject(); // challengeSaveData(RewardApplied=true 포함)도 여기서 같이 저장됨
    }

    // 95수준=랜덤 1명 +5 / 파트총점수준=랜덤 1명 +10 / 99수준=전 직원 +5. 표시용 설명 문자열을 반환.
    string ApplySatisfactionReward()
    {
        var pool = new List<EmployeeData>();
        foreach (var e in EmployeeManager.Instance.ownedEmployees)
            if (!e.isCEO) pool.Add(e);
        if (pool.Count == 0) return "";

        string line;
        switch (_kind)
        {
            case ChallengeKind.LeaderZone95:
            {
                var emp = pool[Random.Range(0, pool.Count)];
                int before = emp.satisfaction;
                emp.ChangeSatisfaction(5);
                InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
                line = "직원 만족도 +5";
                break;
            }
            case ChallengeKind.PartTotal:
            {
                var emp = pool[Random.Range(0, pool.Count)];
                int before = emp.satisfaction;
                emp.ChangeSatisfaction(10);
                InfoFeedUI.Instance?.ShowSatisfaction(emp, emp.satisfaction - before);
                line = "직원 만족도 +10";
                break;
            }
            case ChallengeKind.LeaderZone99:
                foreach (var e in pool) e.ChangeSatisfaction(5);
                InfoFeedUI.Instance?.ShowGlobalSatisfaction(5);
                line = "전 직원 만족도 +5";
                break;
            default:
                line = "";
                break;
        }
        EmployeeManager.Instance.SaveAllEmployees();
        return line;
    }

    // ── 저장/복원 ── 뒤끝은 원시 JSON을 안전히 못 다루므로(DevelopmentManager의 다른 복합 저장과 동일 관례)
    // '|' 구분자 문자열로 직렬화. 부동소수는 InvariantCulture.
    public string GetSaveData()
    {
        if (!_active) return "";
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return string.Join("|", new string[]
        {
            "1",
            ((int)_kind).ToString(ci),
            ((int)_challengePart).ToString(ci),
            ((int)_rewardPart).ToString(ci),
            _targetComputed ? "1" : "0",
            _targetValue.ToString("R", ci),
            _rewardComputed ? "1" : "0",
            _rewardScore.ToString("R", ci),
            _resolved ? "1" : "0",
            _succeeded ? "1" : "0",
            _rewardApplied ? "1" : "0",
        });
    }

    public void RestoreSaveData(string data)
    {
        _active = false;
        _targetComputed = _rewardComputed = _resolved = _succeeded = _rewardApplied = false;
        if (string.IsNullOrEmpty(data)) return;

        var ci = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            var f = data.Split('|');
            if (f.Length < 11 || f[0] != "1") return;
            _active         = true;
            _kind           = (ChallengeKind)int.Parse(f[1], ci);
            _challengePart  = (LeaderType)int.Parse(f[2], ci);
            _rewardPart     = (LeaderType)int.Parse(f[3], ci);
            _targetComputed = f[4] == "1";
            _targetValue    = float.Parse(f[5], System.Globalization.NumberStyles.Float, ci);
            _rewardComputed = f[6] == "1";
            _rewardScore    = float.Parse(f[7], System.Globalization.NumberStyles.Float, ci);
            _resolved       = f[8] == "1";
            _succeeded      = f[9] == "1";
            _rewardApplied  = f[10] == "1";
        }
        catch { _active = false; }
    }

    public void ResetForNewRun() { _active = false; }
}
