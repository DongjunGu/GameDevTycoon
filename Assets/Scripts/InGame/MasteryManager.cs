using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 장르별 숙련도(Mastery) 5단계 — 초보/아마추어/프로/베테랑/거장. 새 런마다 전부 초보로 초기화.
public enum MasteryTier { Novice, Amateur, Pro, Veteran, Master }

// 장르별 숙련도 관리 — 완료 프로젝트의 판정점수(평론가 점수, 랜덤 변동 제외)로 승급 판정.
// GenrePopularityManager/GenreFatigueManager와 같은 자리(LoadingScene 매니저 번들)에 부착.
public class MasteryManager : MonoBehaviour
{
    public static MasteryManager Instance { get; private set; }

    public event System.Action OnMasteryChanged;

    private Dictionary<ProjectGenre, MasteryTier> _tiers = new();

    // 승급 성사 후 아직 플레이어에게 안 보여준 알림 — 마케팅→출시(판매 시작) 시점에 AlertUI1로 1회 표시.
    // 새 런/재접속에도 살아남아야 해서 masteryJson 저장 문자열에 함께 실린다(ShowPendingPromotionThen 참고).
    private struct PendingPromo { public ProjectGenre genre; public MasteryTier from; public MasteryTier to; }
    private PendingPromo? _pendingPromo;
    public bool HasPendingPromotion => _pendingPromo.HasValue;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitAllNovice();
    }

    void InitAllNovice()
    {
        foreach (ProjectGenre g in System.Enum.GetValues(typeof(ProjectGenre)))
            _tiers[g] = MasteryTier.Novice;
    }

    public MasteryTier GetTier(ProjectGenre genre)
        => _tiers.TryGetValue(genre, out var t) ? t : MasteryTier.Novice;

    // 최종 변환 점수에 곱해지는 배율 — DevelopmentResultUI에서 장르 인기/피로도와 같은 지점에 적용.
    public float GetMultiplier(ProjectGenre genre) => GetTier(genre) switch
    {
        MasteryTier.Novice  => 1.00f,
        MasteryTier.Amateur => 1.03f,
        MasteryTier.Pro     => 1.06f,
        MasteryTier.Veteran => 1.10f,
        MasteryTier.Master  => 1.20f,
        _ => 1.00f
    };

    // 승급 도전 구간(도전 하한, 확정선). 베테랑(→거장)은 별도 규칙이라 여기 없음(TryPromote 참고).
    static (float floor, float confirm)? PromotionRange(MasteryTier from) => from switch
    {
        MasteryTier.Novice  => (25f, 40f),
        MasteryTier.Amateur => (45f, 60f),
        MasteryTier.Pro     => (70f, 85f),
        _ => null
    };

    // 판정점수(=CriticReviewUI.CalcCriticScore 결과, 랜덤 변동 -5~+5 제외) 기준 승급 판정.
    // 승급 성사 시 true. 이미 거장이면 항상 false(더 승급 없음).
    public bool TryPromote(ProjectGenre genre, float judgmentScore)
    {
        var current = GetTier(genre);

        if (current == MasteryTier.Veteran)
        {
            // 거장 승급 — 이전 승급조건과 다름: 판정점수 100 이상일 때만 3% 확률.
            if (judgmentScore < 100f) return false;
            if (Random.value < 0.03f)
            {
                _tiers[genre] = MasteryTier.Master;
                _pendingPromo = new PendingPromo { genre = genre, from = MasteryTier.Veteran, to = MasteryTier.Master };
                OnMasteryChanged?.Invoke();
                return true;
            }
            return false;
        }

        var range = PromotionRange(current);
        if (range == null) return false; // Master 등 더 승급 없음
        float floor = range.Value.floor, confirm = range.Value.confirm;

        bool promote;
        if (judgmentScore >= confirm)
        {
            promote = true; // 확정선 이상 100% 승급
        }
        else if (judgmentScore >= floor)
        {
            float chance = 0.10f + 0.50f * (judgmentScore - floor) / (confirm - floor);
            promote = Random.value < chance;
        }
        else
        {
            promote = false;
        }

        if (promote)
        {
            var to = current + 1;
            _tiers[genre] = to;
            _pendingPromo = new PendingPromo { genre = genre, from = current, to = to };
            OnMasteryChanged?.Invoke();
        }
        return promote;
    }

    // 대기 중인 승급 알림이 있으면 AlertUI1("{전등급} -> {후등급}로\n승급했습니다.")로 1회 표시 후 클리어+저장,
    // 없으면 즉시 next() 호출. 마케팅 종료(판매 시작) 시점의 실시간 흐름과, 그 시점 재접속 복원 양쪽에서 재사용.
    public void ShowPendingPromotionThen(System.Action next)
    {
        if (!_pendingPromo.HasValue) { next?.Invoke(); return; }
        var p = _pendingPromo.Value;
        _pendingPromo = null;
        GameTimeManager.Instance?.SaveGameTime(); // 클리어를 즉시 저장 — 재접속 시 중복 표시 방지
        string msg = $"{TierToString(p.from)} -> {TierToString(p.to)}로\n승급했습니다.";
        AlertUI.Instance.Show(msg, () => next?.Invoke());
    }

    // 새 런 시작 — 전 장르 초보로 초기화.
    public void ResetForNewRun()
    {
        InitAllNovice();
        _pendingPromo = null;
        OnMasteryChanged?.Invoke();
    }

    // ── 저장/복원 (GameTimeManager.SaveGameTime/LoadGameTime 의 UserGameTime.masteryJson 컬럼에 편입) ──
    // "genreInt:tierInt,genreInt:tierInt,...|pendingGenre:pendingFrom:pendingTo" — '|' 뒤는 대기 중인
    // 승급 알림(없으면 '|' 자체가 생략됨). 뒤끝 예약 키 회피 관례상 컬럼명은 masteryJson 이지만 내용은 단순 포맷.
    public string GetSaveString()
    {
        var sb = new StringBuilder();
        bool first = true;
        foreach (var kv in _tiers)
        {
            if (!first) sb.Append(',');
            sb.Append((int)kv.Key).Append(':').Append((int)kv.Value);
            first = false;
        }
        if (_pendingPromo.HasValue)
        {
            var p = _pendingPromo.Value;
            sb.Append('|').Append((int)p.genre).Append(':').Append((int)p.from).Append(':').Append((int)p.to);
        }
        return sb.ToString();
    }

    public void LoadSaveString(string data)
    {
        InitAllNovice();
        _pendingPromo = null;
        if (!string.IsNullOrEmpty(data))
        {
            var sections = data.Split('|');

            foreach (var pair in sections[0].Split(','))
            {
                var parts = pair.Split(':');
                if (parts.Length != 2) continue;
                if (int.TryParse(parts[0], out int gInt) && int.TryParse(parts[1], out int tInt))
                    _tiers[(ProjectGenre)gInt] = (MasteryTier)tInt;
            }

            if (sections.Length > 1 && !string.IsNullOrEmpty(sections[1]))
            {
                var parts = sections[1].Split(':');
                if (parts.Length == 3
                    && int.TryParse(parts[0], out int pg)
                    && int.TryParse(parts[1], out int pf)
                    && int.TryParse(parts[2], out int pt))
                {
                    _pendingPromo = new PendingPromo
                    {
                        genre = (ProjectGenre)pg, from = (MasteryTier)pf, to = (MasteryTier)pt
                    };
                }
            }
        }
        OnMasteryChanged?.Invoke();
    }

    public static string TierToString(MasteryTier tier) => tier switch
    {
        MasteryTier.Amateur => "아마추어",
        MasteryTier.Pro     => "프로",
        MasteryTier.Veteran => "베테랑",
        MasteryTier.Master  => "거장",
        _                   => "초보",
    };
}
