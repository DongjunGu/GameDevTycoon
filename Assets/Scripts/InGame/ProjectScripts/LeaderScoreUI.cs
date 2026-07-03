using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeaderScoreUI : MonoBehaviour
{
    public static LeaderScoreUI Instance { get; private set; }

    [Header("직원 정보")]
    public GameObject leaderscorePanel;
    public TextMeshProUGUI leaderNameText;
    public TextMeshProUGUI leaderRoleText;
    public TextMeshProUGUI leaderGradeText;
    public TextMeshProUGUI leaderPotentialText;
    public Image categoryIcon;                // 현재 진행 중인 카테고리 (기획/개발/아트) — 텍스트 대신 아이콘
    public RoleIconSet categoryIconSet;        // 공용 역할 아이콘 세트

    [Header("회차 결과 (1~4회차 순서대로, 길이 4)")]
    public TextMeshProUGUI[] roundScoreTexts; // 각 회차 점수
    public TextMeshProUGUI dsText;            // 누적 ds (스트레스)
    public Slider dsSlider;                   // 누적 ds (0~100)
    public TextMeshProUGUI totalText;         // 팀장 점수 총합
    public Button confirmButton;

    [Header("스트레스 경고 (LeaderScoreEntirePanel)")]
    public Image stressWarningImage;          // LeaderScoreEntirePanel의 Image — 기본 alpha 0
    public float stressBlinkInterval = 0.3f;  // 깜빡임 on/off 각 구간 길이(초)
    private float _currentDs = 0f;

    [Header("연출")]
    public float rollDuration = 1f;   // 회차 점수/ds 상승 시간
    public float roundGap = 1f;       // 회차 간 간격

    [Header("회차 점수 팝콘 연출")]
    public GameObject iconPrefab;     // 회차 점수만큼 개수가 터지는 아이콘
    public Transform popcornPoint;    // 아이콘들이 터져나오는 지점
    public float popDuration = 1.5f;    // 터진 뒤 다음 단계(빨려들어가기) 전까지 총 대기 시간(= 착지 후 바닥에 머무는 시간 포함)
    public float popFlightDuration = 0.3f; // 그 중 실제로 포물선 그리며 날아가 바닥에 착지하기까지 걸리는 시간(빠르게 터져나감)
    public float popScatterRangeX = 400f; // 터질 때 X축으로 최대 ± 얼마나 흩어질지
    public float popFloorY = -140f;   // 떨어져서 착지하는 바닥 Y 좌표(로컬)
    public float popArcHeight = 220f; // 포물선 정점 높이감
    public float suckDuration = 0.15f; // categoryIcon으로 빨려들어가는 시간(= 총점/회차점수 상승 시간) — 짧을수록 빠르게 날아감

    [Header("팀장 캐릭터 프리뷰 (working 애니메이션)")]
    public Image characterImage;              // Resources/Characters/{portraitId} 프리팹의 working 스프라이트를 그대로 미러링
    public string previewLayerName = "PreviewLayer"; // 어떤 카메라에도 안 잡히게 격리할 레이어
    public Animator fireAnimator;             // FireAnim의 Animator — 수치 다 오르면 캐릭터 애니와 함께 정지

    private GameObject _previewInstance;
    private SpriteRenderer _previewSpriteRenderer;
    private Animator _previewAnimator;

    private System.Action _onComplete;

    // confirm 시 DevelopmentPanelUI 에 한 번에 적용할 팀장점수 (애니 종료 시 산출)
    private float _applyPlanning, _applyDevelop, _applyArt;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // fullRoundScores: 차감 전 회차 점수 / roundScores: 차감 반영 최종 회차 점수
    // cumDsAfter: 회차 종료 시점 누적 ds / overflowRound: 누적 ds 100 초과 회차(없으면 -1)
    public void Show(EmployeeData employee, LeaderType type,
                     float[] fullRoundScores, float[] roundScores, float[] cumDsAfter,
                     float total, int overflowRound, float cutFactor,
                     int hunsuBonus, LeaderType hunsuBonusTarget, System.Action onComplete)
    {
        _onComplete = onComplete;

        if (leaderNameText)      leaderNameText.text      = employee.employeeName;
        if (leaderRoleText)      leaderRoleText.text      = employee.RoleToString();
        if (leaderGradeText)     leaderGradeText.text     = employee.GradeToString();
        if (leaderPotentialText) leaderPotentialText.text = employee.PotentialToString();
        EmployeeRole categoryRole = type switch
        {
            LeaderType.Programmer => EmployeeRole.Programmer,
            LeaderType.Artist     => EmployeeRole.Artist,
            _                     => EmployeeRole.Planner
        };
        RoleIconSet.Apply(categoryIcon, categoryIconSet, categoryRole);

        // 초기화
        if (roundScoreTexts != null)
            foreach (var t in roundScoreTexts) if (t) t.text = "0";
        if (dsText) dsText.text = "0";
        if (dsSlider) { dsSlider.minValue = 0f; dsSlider.maxValue = 100f; dsSlider.value = 0f; }
        if (totalText) totalText.text = "0";
        _currentDs = 0f;
        if (stressWarningImage != null)
        {
            var swc = stressWarningImage.color;
            swc.a = 0f;
            stressWarningImage.color = swc;
        }

        if (confirmButton) confirmButton.interactable = false;
        leaderscorePanel.SetActive(true);
        GameTimeManager.Instance?.StopTime(); // 팀장 점수 연출 동안 시간 정지
        ModalGate.I.Register(this); // 점수 표시 중 다른 모달(상인 Alert 등) 차단
        SpawnCharacterPreview(employee);

        StartCoroutine(PlayCoroutine(type, fullRoundScores, roundScores, cumDsAfter,
                                     total, overflowRound, cutFactor, hunsuBonus, hunsuBonusTarget));
    }

    IEnumerator PlayCoroutine(LeaderType type,
                              float[] fullRoundScores, float[] roundScores, float[] cumDsAfter,
                              float total, int overflowRound, float cutFactor,
                              int hunsuBonus, LeaderType hunsuBonusTarget)
    {
        yield return new WaitForSeconds(0.5f);

        float dur = Mathf.Max(0.01f, rollDuration);
        float displayedTotal = 0f;
        float prevCumDs = 0f;

        for (int r = 0; r < 4; r++)
        {
            bool isOverflow = (overflowRound == r);
            float targetDs    = cumDsAfter[r];
            float targetRound = isOverflow ? 0f : fullRoundScores[r]; // 오버플로 회차는 0점

            float startTotal = displayedTotal;

            // Phase A — ds(스트레스)만 상승, 회차 점수는 아직 0으로 숨겨둠
            SetRoundText(r, 0f);
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float ds = Mathf.Lerp(prevCumDs, targetDs, t);
                if (dsText) dsText.text = Mathf.RoundToInt(ds).ToString();
                if (dsSlider) dsSlider.value = Mathf.Clamp(ds, 0f, 100f);
                _currentDs = ds;
                yield return null;
            }
            if (dsText) dsText.text = Mathf.RoundToInt(targetDs).ToString();
            if (dsSlider) dsSlider.value = Mathf.Clamp(targetDs, 0f, 100f);
            _currentDs = targetDs;
            prevCumDs = targetDs;

            if (isOverflow)
            {
                // 누적 ds 100 초과: 팝콘 연출 없이 바로 전 회차 점수 일괄 차감 연출 후 종료
                yield return StartCoroutine(ApplyCutCoroutine(fullRoundScores, roundScores, r));
                break;
            }

            // Phase B+C — 회차 점수만큼 아이콘이 팝콘처럼 펑 터진 뒤 categoryIcon으로 빨려들어가는 동안
            // 총점 + 회차 점수가 동시에 상승.
            yield return StartCoroutine(PopAndFlyCoroutine(Mathf.Max(0, Mathf.RoundToInt(targetRound)), r, startTotal, targetRound));
            displayedTotal = startTotal + targetRound;

            if (r < 3)
                yield return new WaitForSeconds(roundGap);
        }

        // 총합 최종 확정 + 역할 누적스탯 반영 (+ 훈수쟁이 보너스는 기획/아트로)
        if (totalText) totalText.text = Mathf.RoundToInt(total).ToString();

        float pl = type == LeaderType.Planner   ? total : 0f;
        float dv = type == LeaderType.Programmer ? total : 0f;
        float ar = type == LeaderType.Artist     ? total : 0f;
        if (hunsuBonus > 0)
        {
            if (hunsuBonusTarget == LeaderType.Planner) pl += hunsuBonus;
            else                                        ar += hunsuBonus;
        }
        // 패널 반영은 confirm 시점에 한 번에 (OnClickConfirm 에서 AddValuesInstant)
        _applyPlanning = pl;
        _applyDevelop  = dv;
        _applyArt      = ar;

        StopWorkingAnimations();

        yield return new WaitForSeconds(0.5f);
        if (confirmButton) confirmButton.interactable = true;
    }

    // 누적 ds 100 초과 시: 전 회차(0..overflowRound-1) 점수를 full → cut 값으로 내림
    IEnumerator ApplyCutCoroutine(float[] fullRoundScores, float[] roundScores, int overflowRound)
    {
        if (overflowRound <= 0) yield break;

        float dur = 0.5f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float sum = 0f;
            for (int k = 0; k < overflowRound; k++)
            {
                float v = Mathf.Lerp(fullRoundScores[k], roundScores[k], t);
                SetRoundText(k, v);
                sum += v;
            }
            if (totalText) totalText.text = Mathf.RoundToInt(sum).ToString();
            yield return null;
        }

        for (int k = 0; k < overflowRound; k++)
            SetRoundText(k, roundScores[k]);
    }

    void SetRoundText(int index, float value)
    {
        if (roundScoreTexts != null && index >= 0 && index < roundScoreTexts.Length && roundScoreTexts[index])
            roundScoreTexts[index].text = Mathf.RoundToInt(value).ToString();
    }

    // count개의 iconPrefab을 popcornPoint에서 팝콘처럼 펑 터뜨린 뒤, categoryIcon 위치로 빨려들어가게 하면서
    // 그와 동시에 총점(startTotal→startTotal+targetRound)과 해당 회차 점수(0→targetRound)를 함께 올린다.
    IEnumerator PopAndFlyCoroutine(int count, int roundIndex, float startTotal, float targetRound)
    {
        if (count <= 0 || iconPrefab == null || popcornPoint == null)
        {
            // 연출 리소스가 없으면 텍스트만 즉시 반영
            SetRoundText(roundIndex, targetRound);
            if (totalText) totalText.text = Mathf.RoundToInt(startTotal + targetRound).ToString();
            yield break;
        }

        var icons = new System.Collections.Generic.List<RectTransform>(count);

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(iconPrefab, popcornPoint);
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.position = popcornPoint.position;
            rt.localScale = Vector3.zero;

            // 자식 iconImage를 현재 진행 중인 직군(categoryIcon)과 같은 아이콘으로 교체
            var iconImage = go.transform.Find("iconImage");
            var iconImg = iconImage != null ? iconImage.GetComponent<Image>() : null;
            if (iconImg != null && categoryIcon != null) iconImg.sprite = categoryIcon.sprite;

            icons.Add(rt);
            StartCoroutine(PopPunch(rt, rt.localPosition));
        }

        yield return new WaitForSeconds(popDuration + 0.05f); // 다 터질 때까지 대기

        var startPositions = new System.Collections.Generic.List<Vector3>(icons.Count);
        foreach (var rt in icons) startPositions.Add(rt != null ? rt.position : Vector3.zero);

        Vector3 targetPos = categoryIcon != null ? categoryIcon.transform.position : popcornPoint.position;

        float dur = Mathf.Max(0.01f, suckDuration);
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float eased = t * t * t * t; // 강한 ease-in — 초반엔 거의 안 움직이다 순식간에 확 빨려들어감

            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] == null) continue;
                icons[i].position = Vector3.Lerp(startPositions[i], targetPos, eased);
                icons[i].localScale = Vector3.one * (1f - eased * 0.7f);
            }

            float rs = Mathf.Lerp(0f, targetRound, t);
            SetRoundText(roundIndex, rs);
            if (totalText) totalText.text = Mathf.RoundToInt(startTotal + rs).ToString();

            yield return null;
        }

        SetRoundText(roundIndex, targetRound);
        if (totalText) totalText.text = Mathf.RoundToInt(startTotal + targetRound).ToString();

        foreach (var rt in icons)
            if (rt != null) Destroy(rt.gameObject);
    }

    // 아이콘 하나가 popFlightDuration 동안 포물선을 그리며 (X는 랜덤 착지 지점으로, Y는 위로 튀었다가 내려오는 곡선으로)
    // 빠르게 터져나가 popFloorY 바닥에 착지한 뒤, 남은 시간(popDuration - popFlightDuration)은 그 자리에 가만히 있는다.
    // 스케일은 0 → overshoot → 1.
    IEnumerator PopPunch(RectTransform rt, Vector3 startLocalPos)
    {
        float targetX = startLocalPos.x + Random.Range(-popScatterRangeX, popScatterRangeX);
        Vector3 targetLocalPos = new Vector3(targetX, popFloorY, startLocalPos.z);

        float flightDur = Mathf.Clamp(popFlightDuration, 0.01f, Mathf.Max(0.01f, popDuration));
        float elapsed = 0f;
        while (elapsed < flightDur && rt != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDur);
            float scaleT = t < 0.6f ? Mathf.Lerp(0f, 1.15f, t / 0.6f) : Mathf.Lerp(1.15f, 1f, (t - 0.6f) / 0.4f);
            rt.localScale = Vector3.one * scaleT;

            float x = Mathf.Lerp(startLocalPos.x, targetLocalPos.x, t);
            float yLinear = Mathf.Lerp(startLocalPos.y, targetLocalPos.y, t);
            float y = yLinear + popArcHeight * 4f * t * (1f - t); // 포물선 — 튀어올랐다가 바닥에 착지
            rt.localPosition = new Vector3(x, y, targetLocalPos.z);

            yield return null;
        }

        if (rt != null)
        {
            rt.localScale = Vector3.one;
            rt.localPosition = targetLocalPos; // 착지 후 남은 시간은 그대로 바닥에 위치
        }
    }

    void LateUpdate()
    {
        // 실제 캐릭터 프리팹(SpriteRenderer)이 Animator로 바꾸는 스프라이트를 그대로 UI Image에 미러링.
        if (_previewSpriteRenderer != null && characterImage != null)
            characterImage.sprite = _previewSpriteRenderer.sprite;

        UpdateStressWarning();
    }

    // ds < 90: 완전히 안 보임(alpha 0) / 90~99: alpha 50~70 사이 깜빡임(경고) / 100: alpha 100~120 사이 깜빡임(위험).
    // 부드럽게 lerp하지 않고 두 값 사이를 사각파로 툭툭 전환해야 "깜빡이는" 느낌이 남 — 계속 반복, 안 멈춤.
    // 100 구간 깜빡임은 회차 연출이 끝나(StopWorkingAnimations 이후)도 멈추지 않음 — LateUpdate가 _currentDs만 보고
    // 계속 도는 구조라 코루틴 종료와 무관하게 계속 깜빡인다.
    void UpdateStressWarning()
    {
        if (stressWarningImage == null) return;

        float targetA;
        float interval = Mathf.Max(0.01f, stressBlinkInterval);
        bool blinkOn = Mathf.Repeat(Time.time, interval * 2f) < interval;
        if (_currentDs >= 100f)
            targetA = blinkOn ? 120f / 255f : 100f / 255f;
        else if (_currentDs >= 90f)
            targetA = blinkOn ? 70f / 255f : 50f / 255f;
        else
            targetA = 0f;

        var c = stressWarningImage.color;
        c.a = targetA;
        stressWarningImage.color = c;
    }

    // 선택된 팀장의 캐릭터 프리팹을 화면 밖에 인스턴스화해 working 애니메이션을 실제로 재생시키고,
    // 그 SpriteRenderer.sprite를 매 프레임 characterImage로 미러링한다 (LateUpdate).
    void SpawnCharacterPreview(EmployeeData employee)
    {
        ClearCharacterPreview();
        if (characterImage == null || employee == null) return;

        // CEO는 일반 직원과 달리 Resources/Characters/{portraitId}에 프리팹이 없고,
        // OfficeManager.SpawnCEO()와 동일하게 CEOManager.ceoPrefab(→ 없으면 OfficeManager.fallbackPrefab)을 사용한다.
        GameObject prefab;
        if (employee.isCEO)
        {
            prefab = CEOManager.Instance != null ? CEOManager.Instance.ceoPrefab : null;
            if (prefab == null) prefab = OfficeManager.Instance != null ? OfficeManager.Instance.fallbackPrefab : null;
        }
        else
        {
            if (string.IsNullOrEmpty(employee.portraitId)) return;
            prefab = Resources.Load<GameObject>($"Characters/{employee.portraitId}");
        }
        if (prefab == null) return;

        _previewInstance = Instantiate(prefab);
        _previewInstance.transform.position = new Vector3(9999f, 9999f, 0f); // 화면 밖 — 어떤 카메라에도 안 잡히게

        int layer = LayerMask.NameToLayer(previewLayerName);
        if (layer >= 0)
            foreach (var t in _previewInstance.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;

        // 게임플레이 로직(이동/파견/클릭 등) 제거 — 애니메이션만 필요
        foreach (var c in _previewInstance.GetComponentsInChildren<OfficeCharacter>())     { c.enabled = false; Destroy(c); }
        foreach (var c in _previewInstance.GetComponentsInChildren<CharacterMover>())      { c.enabled = false; Destroy(c); }
        foreach (var c in _previewInstance.GetComponentsInChildren<IsometricSorter>())     { c.enabled = false; Destroy(c); }
        foreach (var c in _previewInstance.GetComponentsInChildren<CharacterController>()) { c.enabled = false; Destroy(c); }
        foreach (var c in _previewInstance.GetComponentsInChildren<Rigidbody2D>())  Destroy(c);
        foreach (var c in _previewInstance.GetComponentsInChildren<Collider2D>())   Destroy(c);

        var charAnimator = _previewInstance.GetComponentInChildren<CharacterAnimator>();
        var rawAnimator  = _previewInstance.GetComponentInChildren<Animator>();
        if (charAnimator != null)
        {
            charAnimator.SetWorking(true);
            // CharacterAnimator.Update()는 GameTimeManager.IsRunning이 false면 animator.speed를 0으로 묶어버림 —
            // 팀장점수 연출은 시간을 멈춘 채 진행되므로, 여기서 꺼서 그 게이팅을 우회하고 아래서 speed를 직접 고정한다.
            charAnimator.enabled = false;
        }
        if (rawAnimator != null) rawAnimator.speed = 1f;
        _previewAnimator = rawAnimator;

        _previewSpriteRenderer = _previewInstance.GetComponentInChildren<SpriteRenderer>();
        characterImage.enabled = true;

        if (fireAnimator != null) fireAnimator.speed = 1f; // 새 팀장 프리뷰 시작 시 fire도 다시 재생
    }

    // 회차 점수가 다 오르고 나면(연출 완료) 캐릭터 working 애니와 FireAnimation 둘 다 정지(마지막 프레임에 고정).
    void StopWorkingAnimations()
    {
        if (_previewAnimator != null) _previewAnimator.speed = 0f;
        if (fireAnimator != null)     fireAnimator.speed = 0f;
    }

    void ClearCharacterPreview()
    {
        if (_previewInstance != null) Destroy(_previewInstance);
        _previewInstance = null;
        _previewSpriteRenderer = null;
        _previewAnimator = null;
    }

    public void OnClickConfirm()
    {
        // 팀장점수를 DevelopmentPanelUI 에 한 번에 반영 (실제값+표시값 즉시 동기화 → 점프 업).
        // _onComplete(→StartDeveloping) 의 SaveProject 전에 적용돼야 accum 에 저장됨.
        DevelopmentPanelUI.Instance.AddValuesInstant(_applyPlanning, _applyDevelop, _applyArt, 0f, 0f);
        _applyPlanning = _applyDevelop = _applyArt = 0f;

        ClearCharacterPreview();
        leaderscorePanel.SetActive(false);
        LeaderSelectUI.Instance.entireLeaderPanel.gameObject.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        _onComplete?.Invoke();
    }
}
