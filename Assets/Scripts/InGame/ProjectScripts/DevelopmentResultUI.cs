using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

public class DevelopmentResultUI : MonoBehaviour
{
    public static DevelopmentResultUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject resultPanel;
    public GameObject shadowBG;    // resultPanel 과 항상 같이 켜지고 꺼지는 배경 딤
    public GameObject editNamePanel;

    [Header("UI")]
    public TextMeshProUGUI planningText;
    public TextMeshProUGUI developText;
    public TextMeshProUGUI artText;
    public TextMeshProUGUI creativityText;
    [Header("신기록 뱃지 (과거 완료작 최고치 대비 초과 시에만 표시)")]
    public GameObject planningNewRecordText;
    public GameObject developNewRecordText;
    public GameObject artNewRecordText;
    public GameObject creativityNewRecordText;
    [Header("Project Info")]
    public TextMeshProUGUI scaleResultText;
    public TextMeshProUGUI genreResultText;
    public TextMeshProUGUI platformResultText;

    [Header("Contribution (기여도 1등)")]
    public Image contributorPortrait;          // Row2/PortraitPanel/portraitImage

    [Header("Contribution Detail (전체 순위)")]
    public GameObject rightPanel;              // 기본 상태 패널 (RightPanel) — 상세 볼 때 비활성
    public GameObject contributionDetailPanel; // 순위 상세 패널 (RightPanelDetail) — RightPanel 과 서로 반대로 토글
    public Button contributionCheckBtn;        // 상세 열기/닫기 겸용 토글 버튼 (ContributionCheckBtn)
    public TextMeshProUGUI contributionCheckBtnText; // 버튼 라벨 — "기여도 확인" ↔ "돌아가기" 토글
    public GameObject rowPrefab;               // RowPanel 프리팹 (numberPanel/namePanel/contriPanel)
    public Transform contributionContent;      // 행이 생성될 부모 (Content)
    [Tooltip("RowPanel 배경 등급색 — DispatchSlotUI 와 동일 공용 GradeSpriteSet 에셋 재사용")]
    public GradeSpriteSet rowBgGradeSet;
    private readonly List<GameObject> _rowPool = new();
    const string ContributionCheckLabel = "기여도 확인";
    const string ContributionBackLabel  = "돌아가기";
    [Header("Row 등장 연출 (기여도 상세 - 오른쪽에서 왼쪽으로)")]
    public float rowSlideDistance = 1100f;
    public float rowSlideDuration = 0.35f;
    public float rowSlideStagger = 0.08f;
    private VerticalLayoutGroup _contentLayoutGroup;
    private Sequence _rowSlideSeq;
    [Header("Project Name")]
    public TextMeshProUGUI projectNameText;
    public TMP_InputField projectNameInput;
    [Tooltip("9글자 제한(characterLimit)에 걸렸을 때만 표시하는 경고 문구 (InputFieldImage/w)")]
    public GameObject projectNameWarningText;
    const int ProjectNameMaxLength = 9;

    private float _lastPopularityMultiplier;
    private float _lastMarketingMultiplier;
    public float LastPopularityMultiplier => _lastPopularityMultiplier;
    public float LastMarketingMultiplier => _lastMarketingMultiplier;
    public float LastPlanning => _lastPlanning;
    public float LastDevelop => _lastDevelop;
    public float LastArt => _lastArt;
    public float LastCreativity => _lastCreativity;
    public float LastBug => _lastBug;
    private float _lastPlanning;
    private float _lastDevelop;
    private float _lastArt;
    private float _lastCreativity;
    private float _lastBug;
    private string _lastProjectName = "프로젝트명";
    public string LastProjectName => _lastProjectName;
    string GetScaleString(ProjectScale scale) => scale switch
    {
        ProjectScale.Small => "소형",
        ProjectScale.Medium => "중형",
        ProjectScale.Large => "대형",
        _ => ""
    };

    string GetGenreString(ProjectGenre genre) => genre switch
    {
        ProjectGenre.RPG              => "RPG",
        ProjectGenre.FPS              => "FPS",
        ProjectGenre.Arcade           => "아케이드",
        ProjectGenre.HealingSimulation => "힐링시뮬레이션",
        ProjectGenre.Horror           => "공포",
        ProjectGenre.Idle             => "방치형",
        ProjectGenre.RTS              => "실시간전략",
        ProjectGenre.VisualNovel      => "미연시",
        ProjectGenre.Sports           => "스포츠",
        ProjectGenre.Puzzle           => "퍼즐",
        _ => ""
    };

    string GetPlatformString(ProjectPlatform platform) => platform switch
    {
        ProjectPlatform.Mobile => "모바일",
        ProjectPlatform.PC => "PC",
        ProjectPlatform.Nintendo => "닌텐도",
        ProjectPlatform.Console => "플레이스테이션",
        _ => ""
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        SetResultPanelActive(false);
        editNamePanel.SetActive(false);
        SetContributionDetailShown(false);
        if (planningNewRecordText   != null) planningNewRecordText.SetActive(false);
        if (developNewRecordText    != null) developNewRecordText.SetActive(false);
        if (artNewRecordText        != null) artNewRecordText.SetActive(false);
        if (creativityNewRecordText != null) creativityNewRecordText.SetActive(false);
        if (contributionCheckBtn != null)
        {
            contributionCheckBtn.onClick.RemoveAllListeners();
            contributionCheckBtn.onClick.AddListener(OnClickContributionCheck);
        }

        if (projectNameInput != null)
        {
            projectNameInput.characterLimit = ProjectNameMaxLength; // 영어/한글 모두 1글자=1자로 카운트되므로 동일 기준 적용
            projectNameInput.onValueChanged.AddListener(OnProjectNameInputChanged);
        }
        if (projectNameWarningText != null) projectNameWarningText.SetActive(false);
    }

    void OnDestroy()
    {
        _rowSlideSeq?.Kill();
    }

    // characterLimit에 걸려 9글자를 못 넘어가는 순간(=입력이 꽉 찬 순간) 경고 문구를 띄운다.
    void OnProjectNameInputChanged(string value)
    {
        if (projectNameWarningText != null)
            projectNameWarningText.SetActive(value.Length >= ProjectNameMaxLength);
    }

    // RightPanel ↔ RightPanelDetail 반대로 토글 + 버튼 라벨 갱신 ("기여도 확인" ↔ "돌아가기")
    void SetContributionDetailShown(bool shown)
    {
        if (rightPanel != null) rightPanel.SetActive(!shown);
        if (contributionDetailPanel != null) contributionDetailPanel.SetActive(shown);
        if (contributionCheckBtnText != null)
            contributionCheckBtnText.text = shown ? ContributionBackLabel : ContributionCheckLabel;
    }

    // ContributionCheckBtn 클릭 — 상세 표시 중이면 되돌리고, 아니면 순위를 채워서 보여준다.
    public void OnClickContributionCheck()
    {
        bool shown = contributionDetailPanel != null && contributionDetailPanel.activeSelf;
        if (shown)
        {
            SetContributionDetailShown(false);
            return;
        }

        if (contributionDetailPanel == null || rowPrefab == null || contributionContent == null) return;

        // 기여도 내림차순(GetContributionRanking 이 이미 내림차순 정렬) — 해당 직원 수만큼만 행 생성, 빈 패딩 없음.
        var ranking = DevelopmentManager.Instance.GetContributionRanking();
        EnsureRowPool(ranking.Count);

        for (int i = 0; i < _rowPool.Count; i++)
        {
            var row = _rowPool[i];
            if (row == null) continue;
            if (i < ranking.Count)
            {
                var grade = ranking[i].emp != null ? ranking[i].emp.grade : EmployeeGrade.Normal;
                bool isCEO = ranking[i].emp != null && ranking[i].emp.isCEO;
                row.SetActive(true);
                SetContributionRow(row, i + 1, ranking[i].name, $"{ranking[i].percent:F0}%", grade, isCEO, ranking[i].portraitId);
            }
            else row.SetActive(false); // 이전에 더 많은 행을 표시했을 때 풀에 남아있던 여분
        }

        // Content가 RightPanelDetail 아래 비활성 상태에서는 ForceRebuildLayoutImmediate가 아무것도 계산하지
        // 않아(비활성 계층은 리빌드 대상에서 제외됨) 목표 위치를 잘못 캡처하게 된다 — 반드시 먼저 활성화한 뒤
        // 슬라이드를 재생해야 한다.
        SetContributionDetailShown(true);
        PlayRowSlideIn();
    }

    // RowPanel들이 Content(VerticalLayoutGroup)의 직계 자식으로 쌓여있는 한, 리빌드가 매번 anchoredPosition을
    // 강제로 되돌려서 위치 애니메이션이 통하지 않는다([[feedback_layoutgroup_child_position_animation]]).
    // 그래서 먼저 리빌드로 정상 배치(목표 위치)를 확정 → 캡처 → LayoutGroup을 잠깐 꺼서 리빌드를 막은 채로
    // 오른쪽 바깥(rowSlideDistance만큼)에서 목표 위치까지 순차적으로 슬라이드시킨다. 카드 개수/순서는 매번
    // 달라질 수 있으므로 여기서만 껐다가, 다음 열람 시 다시 켜고 리빌드해서 늘 최신 배치를 따라간다.
    void PlayRowSlideIn()
    {
        if (contributionContent == null) return;
        var contentRect = contributionContent as RectTransform;
        if (_contentLayoutGroup == null) _contentLayoutGroup = contributionContent.GetComponent<VerticalLayoutGroup>();

        if (_contentLayoutGroup != null) _contentLayoutGroup.enabled = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        var targets = new List<(RectTransform rt, Vector2 pos)>();
        foreach (var row in _rowPool)
        {
            if (row == null || !row.activeSelf) continue;
            var rt = row.GetComponent<RectTransform>();
            if (rt != null) targets.Add((rt, rt.anchoredPosition));
        }

        if (_contentLayoutGroup != null) _contentLayoutGroup.enabled = false;

        _rowSlideSeq?.Kill();
        _rowSlideSeq = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < targets.Count; i++)
        {
            var (rt, target) = targets[i];
            rt.anchoredPosition = target + new Vector2(rowSlideDistance, 0f);
            _rowSlideSeq.Insert(i * rowSlideStagger, rt.DOAnchorPos(target, rowSlideDuration).SetEase(Ease.OutCubic));
        }
    }

    // Content 아래 행 풀을 count 개 이상 확보 (모자라면 프리팹 생성)
    void EnsureRowPool(int count)
    {
        while (_rowPool.Count < count)
        {
            var go = Instantiate(rowPrefab);
            go.transform.SetParent(contributionContent, false); // 로컬 트랜스폼 보존(스케일 깨짐 방지)
            _rowPool.Add(go);
        }
    }

    void SetContributionRow(GameObject row, int rank, string name, string rate, EmployeeGrade grade, bool isCEO, string portraitId)
    {
        var numberText = row.transform.Find("numberPanel")?.GetComponentInChildren<TextMeshProUGUI>();
        var nameText   = row.transform.Find("namePanel")?.GetComponentInChildren<TextMeshProUGUI>();
        var rateText   = row.transform.Find("contriPanel")?.GetComponentInChildren<TextMeshProUGUI>();
        if (numberText != null) numberText.text = rank.ToString();
        if (nameText != null)   nameText.text   = name;
        if (rateText != null)   rateText.text   = rate;

        var portraitImage = row.transform.Find("portraitPanel/portraitPanelChild/portraitImage")?.GetComponent<Image>();
        if (portraitImage != null)
        {
            var sprite = !string.IsNullOrEmpty(portraitId) ? Resources.Load<Sprite>($"Portraits/Mini/{portraitId}") : null;
            portraitImage.sprite  = sprite;
            portraitImage.enabled = sprite != null;
        }

        GradeSpriteSet.Apply(row.GetComponent<Image>(), rowBgGradeSet, isCEO, grade);

        // 1~3등이면 등급색 배경 위에 등수 프레임/왕관을 덮어씀(RowRankVisual이 자체적으로 적용 여부 판단).
        row.GetComponent<RowRankVisual>()?.SetRank(rank);
    }

    public void Show(float planning, float develop, float art, float bug, float creativity)
    {
        // 기본 프로젝트명 = "프로젝트명" + (기존 출시작 수 + 1). 예: 출시작 2개 → "프로젝트명3"
        int releasedCount = CompletedProjectManager.Instance != null ? CompletedProjectManager.Instance.completedProjects.Count : 0;
        _lastProjectName = $"프로젝트명{releasedCount + 1}"; // ← 초기화
        projectNameText.text = _lastProjectName;
        _lastPlanning = planning;
        _lastDevelop = develop;
        _lastArt = art;
        _lastBug = bug;
        _lastCreativity = creativity;
        if (planningText != null)   planningText.text = $"{Mathf.RoundToInt(planning)}";
        if (developText != null)    developText.text = $"{Mathf.RoundToInt(develop)}";
        if (artText != null)        artText.text = $"{Mathf.RoundToInt(art)}";
        if (creativityText != null) creativityText.text = $"{Mathf.RoundToInt(creativity)}";

        // 신기록 뱃지 — 과거 완료작(이번 프로젝트는 아직 미포함) 중 최고치를 이번 값이 넘으면 표시.
        // 완료작이 하나도 없으면(첫 프로젝트) 비교 대상 자체가 없어 항상 신기록으로 취급.
        float bestPlanning = 0f, bestDevelop = 0f, bestArt = 0f, bestCreativity = 0f;
        if (CompletedProjectManager.Instance != null)
        {
            foreach (var proj in CompletedProjectManager.Instance.completedProjects)
            {
                if (proj.planning   > bestPlanning)   bestPlanning   = proj.planning;
                if (proj.develop    > bestDevelop)    bestDevelop    = proj.develop;
                if (proj.art        > bestArt)        bestArt        = proj.art;
                if (proj.creativity > bestCreativity) bestCreativity = proj.creativity;
            }
        }
        if (planningNewRecordText   != null) planningNewRecordText.SetActive(planning   > bestPlanning);
        if (developNewRecordText    != null) developNewRecordText.SetActive(develop    > bestDevelop);
        if (artNewRecordText        != null) artNewRecordText.SetActive(art        > bestArt);
        if (creativityNewRecordText != null) creativityNewRecordText.SetActive(creativity > bestCreativity);

        // Row1 — 규모/장르/플랫폼을 각 텍스트에 개별 출력
        if (scaleResultText != null)    scaleResultText.text    = $"{GetScaleString(ProjectSetupUI.SelectedScale)}";
        if (genreResultText != null)    genreResultText.text    = $"{GetGenreString(ProjectSetupUI.SelectedGenre)}";
        if (platformResultText != null) platformResultText.text = $"{GetPlatformString(ProjectSetupUI.SelectedPlatform)}";

        // Row2 — 기여도 1등 초상화 + 이름(퇴사면 "(퇴사)") + 기여도%
        var top = DevelopmentManager.Instance.GetTopContributor();
        if (!string.IsNullOrEmpty(top.name))
        {
            if (contributorPortrait != null && !string.IsNullOrEmpty(top.portraitId))
            {
                var sprite = Resources.Load<Sprite>($"Portraits/{top.portraitId}");
                if (sprite != null) contributorPortrait.sprite = sprite;
            }
        }

        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        SetContributionDetailShown(false); // 결과창 새로 열 때는 항상 기본(RightPanel) 상태로
        SetResultPanelActive(true);
    }

    // resultPanel 과 shadowBG(뒤 배경 딤)를 항상 함께 토글
    void SetResultPanelActive(bool active)
    {
        resultPanel.SetActive(active);
        if (shadowBG != null) shadowBG.SetActive(active);
    }

    // 이름변경 버튼
    public void OnClickEditName()
    {
        // 현재 프로젝트명을 input에 기본값으로 설정
        projectNameInput.text = projectNameText.text;
        OnProjectNameInputChanged(projectNameInput.text); // 재오픈 시에도 경고 표시 상태를 현재 값 기준으로 동기화
        editNamePanel.SetActive(true);

        // 전체 선택된 것처럼 보이게
        projectNameInput.Select();
        projectNameInput.ActivateInputField();
        projectNameInput.selectionAnchorPosition = 0;
        projectNameInput.selectionFocusPosition = projectNameInput.text.Length;
    }

    // 변경 버튼
    public void OnClickConfirmName()
    {
        string value = projectNameInput.text.Trim();
        if (!string.IsNullOrEmpty(value))
        {
            projectNameText.text = value;
            _lastProjectName = value;
        }
        editNamePanel.SetActive(false);
    }
    // 취소 버튼
    public void OnClickCancelName()
    {
        editNamePanel.SetActive(false);
    }

    public void OnClickClose()
    {
        SetResultPanelActive(false);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
    }
    public void OnClickRelease()
    {
        ProjectSaveManager.Instance.SetProjectName(_lastProjectName);
        SetResultPanelActive(false);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);

        float p = DevelopmentPanelUI.Instance.GetPlanning();
                    float d = DevelopmentPanelUI.Instance.GetDevelop();
                    float a = DevelopmentPanelUI.Instance.GetArt();
                    float c = DevelopmentPanelUI.Instance.GetCreativity();
                    float bRem = DevelopmentPanelUI.Instance.GetBug();

                    float rawScore = CalcRawScore(p, d, a, c, ProjectSetupUI.SelectedPlatform);

                    // 쏠린 케이스 감점 (2026-08-14 재조정) — 중형/대작만, 사무실 1단계는 적용 안 함(2단계부터).
                    // criticScore/sAdj 공통 적용.
                    // 불균형 배수 R = max(P,D,A) ÷ min(P,D,A)
                    // 감점% = MIN(40%, 40% × MAX(0, R − 임계값) ÷ 0.9)
                    // 임계값은 사무실 단계별 최대 인원(StageManager.MaxEmployeeCount)에 따라 다름 — GetCaseImbalanceThreshold 참고.
                    float casePenalty = 0f;
                    bool caseOfficeStageEligible = StageManager.Instance != null && StageManager.Instance.CurrentStage >= 2;
                    if (caseOfficeStageEligible && (ProjectSetupUI.SelectedScale == ProjectScale.Medium || ProjectSetupUI.SelectedScale == ProjectScale.Large))
                    {
                        float maxStat = Mathf.Max(p, Mathf.Max(d, a));
                        float minStat = Mathf.Min(p, Mathf.Min(d, a));
                        if (maxStat > 0f)
                        {
                            float threshold = GetCaseImbalanceThreshold(StageManager.Instance.MaxEmployeeCount);
                            // minStat이 0이면(스탯 하나가 완전히 방치된 최악의 쏠림) 배수가 무한대가 되므로,
                            // 상한(40%)에 정확히 닿도록 threshold+0.9를 대입해 나눗셈 없이 캡을 재현한다.
                            float ratio = minStat > 0f ? maxStat / minStat : threshold + 0.9f;
                            casePenalty = Mathf.Min(0.40f, 0.40f * Mathf.Max(0f, ratio - threshold) / 0.9f);
                        }
                    }

                    float criticScore = (rawScore * (1f - DevelopmentManager.Instance.BugPenalty)
                                      + DevelopmentManager.Instance.BugEventBonus) * (1f - casePenalty);

                    // 숙련도(Mastery) — 이번 프로젝트의 최종 점수엔 "판정 전" 현재 등급의 배율을 적용(먼저 캡처),
                    // 승급 판정은 그 뒤에 굴려서 다음 프로젝트부터 반영되게 한다.
                    ProjectGenre masteryGenre = ProjectSetupUI.SelectedGenre;
                    float masteryMultiplier = MasteryManager.Instance != null
                        ? MasteryManager.Instance.GetMultiplier(masteryGenre) : 1f;
                    if (MasteryManager.Instance != null)
                    {
                        // 판정점수 = 평론가 점수(랜덤 변동 -5~+5 제외) — CriticReviewUI 표시값과 같은 로그공식, 변동만 뺌.
                        int judgmentScore = CriticReviewUI.CalcCriticScore(criticScore);
                        MasteryManager.Instance.TryPromote(masteryGenre, judgmentScore);
                    }

        // ── 1. 평론가 패널 ──
    CriticReviewUI.Instance.Show(criticScore, () =>
    {
        // ── 2. 마케팅 ──
        AlertUI.Instance.Show("마케팅을 시작합니다.", () =>
        {
            MarketingUI.Instance.Show(() =>
            {
                float sAdj = (rawScore * (1f - DevelopmentManager.Instance.BugPenalty)
                           + DevelopmentManager.Instance.BugEventBonus) * (1f - casePenalty);

                sAdj = Mathf.Max(0f, sAdj);

                // float finalScore = CalcFinalScore(sAdj); // 로그 압축 비활성화
                float finalScore = sAdj * CalcPopularityMultiplier() * CalcFatigueMultiplier() * masteryMultiplier;
                float quality    = CalcQualityScore(finalScore);

                Debug.Log($"원천: {rawScore:F1} / 버그감점: {DevelopmentManager.Instance.BugPenalty * 100f:F1}% / 케이스감점: {casePenalty * 100f:F1}% / S_adj: {sAdj:F1} / 인지도배율: {CalcPopularityMultiplier():F2} / 피로도배율: {CalcFatigueMultiplier():F2} / 숙련도배율: {masteryMultiplier:F2} / 최종: {finalScore:F1} / 품질: {quality:F1}");

                ProjectSaveManager.Instance.SetQualityScore(quality, ProjectSetupUI.SelectedScale);
                DevelopmentManager.Instance.CurrentStage = ProjectStage.Marketing;
                // 마케팅까지 끝나고 판매 시작 전 — 랜덤 2인 patrol (CEO/비서 포함)
                OfficeManager.Instance?.TriggerDevelopmentCompletePatrol(
                    DevelopmentManager.Instance.developCompletePatrolPointA,
                    DevelopmentManager.Instance.developCompletePatrolPointB);
                // 개발이 끝나 더 이상 Developing/BugFixing이 아니므로 배경 patrol 스케줄러 재개.
                OfficeManager.Instance?.ResumeIdlePatrol();
                MoneyManager.Instance.SaveMoney();
                ProjectSaveManager.Instance.SaveProject();
                GameTimeManager.Instance.SaveGameTime();

                // 조기 저장 — 앱 종료로 인한 Sales 유실 방지. revenuePerPeriod 는 SalesUI.Show 호출 시점에 결정/저장됨.
                SalesSaveManager.Instance.SaveSales(
                    0, 0, null, quality, ProjectSetupUI.SelectedScale,
                    _lastProjectName,
                    ProjectSetupUI.SelectedScale, ProjectSetupUI.SelectedGenre, ProjectSetupUI.SelectedPlatform,
                    _lastPlanning, _lastDevelop, _lastArt, _lastCreativity, _lastBug
                );

                // 숙련도 승급 알림(있으면) → "판매 시작!" 순서. 재접속 시엔 ProjectSaveManager.RestoreIfNeeded 가
                // 같은 순서로 재현(masteryJson 에 대기 알림이 실려 있어 앱을 껐다 켜도 유실되지 않음).
                System.Action showSalesStart = () =>
                {
                    AlertUI.Instance.Show("판매 시작!", () =>
                    {
                        SalesUI.Instance.Show(quality, ProjectSetupUI.SelectedScale);
                    });
                };
                if (MasteryManager.Instance != null)
                    MasteryManager.Instance.ShowPendingPromotionThen(showSalesStart);
                else
                    showSalesStart();
            });
        });
    });
    }
    // 쏠린 케이스 감점 임계값 — 사무실 단계별 최대 인원(StageManager.MaxEmployeeCount, 씬 설정 {2,4,6,7})에
    // 대응. 1단계(2명)는 호출부에서 CurrentStage>=2로 이미 걸러지므로 여기 없음.
    static float GetCaseImbalanceThreshold(int maxEmployeeCount) => maxEmployeeCount switch
    {
        4 => 2.2f,
        6 => 1.6f,
        7 => 1.9f,
        _ => 1.9f, // 정의 안 된 인원수(향후 단계 추가 등) — 가장 마지막(최고단계) 임계값으로 폴백
    };

    float CalcRawScore(float p, float d, float a, float c, ProjectPlatform platform)
    {
        // n = 1 (기본 배율, 나중에 규모/장르에 따라 조정 가능)
        float n = 1f;

        switch (platform)
        {
            case ProjectPlatform.Mobile:
                return (1.5f * n * p) + (n * d) + (n * a) + (n * c);

            case ProjectPlatform.PC:
                return (n * p) + (1.5f * n * d) + (n * a) + (n * c);

            case ProjectPlatform.Nintendo:
                return (n * p) + (n * d) + (1.5f * n * a) + (n * c);

            case ProjectPlatform.Console:
                return (5f * Mathf.Min(p, Mathf.Min(d, a))) + (n * c);

            default:
                return 0f;
        }
    }

    float CalcFinalScore(float sAdj)
    {
        double maxExpected = 5000.0;
        double final = 100.0 * System.Math.Log(sAdj + 1) / System.Math.Log(maxExpected + 1);
        final = System.Math.Max(0, System.Math.Min(100, final));
        return (float)final;
    }
    float CalcQualityScore(float finalScore)
    {
        _lastPopularityMultiplier = CalcPopularityMultiplier();
        _lastMarketingMultiplier  = CalcMarketingMultiplier();

        float quality = finalScore * _lastMarketingMultiplier;

        Debug.Log($"인지도배율: {_lastPopularityMultiplier:F2} / 마케팅배율: {_lastMarketingMultiplier:F2} / 최종품질: {quality:F1}");

        // TODO: 테크트리 점수
        return quality;
    }

    float CalcPopularityMultiplier()
    {
        return ProjectSetupUI.SelectedGenrePopularity switch
        {
            1 => 0.95f,
            2 => 1.0f,
            3 => 1.05f,
            _ => 1.0f
        };
    }

    float CalcFatigueMultiplier()
    {
        int fatigue = ProjectSetupUI.SelectedGenreFatigue;
        return fatigue switch
        {
            0 => 1.0f,
            1 => 0.97f,
            2 => 0.94f,
            3 => 0.90f,
            _ => 0.90f
        };
    }

    float CalcMarketingMultiplier()
    {
        // 마케팅의 신 trait — 마케팅 개수/비용 무관하게 최고치(매출변화 +15% → M=1.15) 적용
        if (TraitEffectApplier.HasMarketingFree())
        {
            Debug.Log($"[마케팅] 마케팅의 신 발동 → M=1.15");
            return 1.15f;
        }

        // P = 마케팅비 / 전체 직원 연봉 (퍼센트 단위)
        int totalSalary = EmployeeManager.Instance != null ? EmployeeManager.Instance.GetTotalSalary() : 0;
        if (totalSalary <= 0) return 1.0f;

        int marketingCost = MarketingUI.Instance.GetTotalCost();
        float P = (float)marketingCost / totalSalary * 100f;

        // 매출변화(%) 구간별 선형 보간 → M = 1 + 매출변화/100
        float salesChange;
        if (P < 10f)
            salesChange = -15f;
        else if (P < 15f)
            salesChange = -15f + (P - 10f) * 2f; // -15% → -5%
        else if (P < 20f)
            salesChange = -5f + (P - 15f) * 2f;  // -5%  → +5%
        else if (P < 25f)
            salesChange = 5f + (P - 20f) * 2f;   // +5%  → +15%
        else
            salesChange = 15f;

        float M = 1f + salesChange / 100f;
        Debug.Log($"[마케팅] 전체연봉: {totalSalary} / 마케팅비: {marketingCost} / P: {P:F1}% / 매출변화: {salesChange:F1}% / M: {M:F3}");
        return M;
    }
}