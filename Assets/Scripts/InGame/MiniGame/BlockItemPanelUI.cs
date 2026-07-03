using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 창의성 미니게임 상단 블록 아이템 패널.
// 보유 중인 블록 아이템(랜덤/전설)만 한 번에 하나씩 표시하고 SlideBtn 으로 순환한다.
// - 열릴 때 보유한 첫 블록(랜덤 우선)부터 표시. 하나도 없으면 비활성화 대신 우측으로 슬라이드해 접음(빈 "0개"도 안 띄움).
// - Text: 아이템 이름 / CountText: 잔여 수량(N개) / BlockItemImage: 이미지(에셋 없으면 비움)
// - UseBtn: 현재 표시 중인 아이템 사용
public class BlockItemPanelUI : MonoBehaviour
{
    public static BlockItemPanelUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] Image           itemImage;  // BlockItemImage
    [SerializeField] Image           frameImage; // 등급 프레임 (ItemSlotUI와 동일)
    [SerializeField] ItemGradeSet    gradeSet;
    [SerializeField] TextMeshProUGUI nameText;   // Text
    [SerializeField] Button          slideBtn;   // 다음 아이템으로 전환
    [SerializeField] Button          useBtn;     // 현재 아이템 사용
    [SerializeField] TextMeshProUGUI countText;  // 잔여량 (숫자만)

    [Header("접힘/펼침 슬라이드 (전용 버튼은 추후 별도 연결 — OnClickToggleSlide 를 그 버튼 OnClick 에 연결)")]
    [Tooltip("우측으로 슬라이드될 대상 — 비우면 이 오브젝트의 RectTransform")]
    [SerializeField] RectTransform slideTarget;
    [Tooltip("접힘 상태 표시용 화살표. 비우면 slideBtn 자식 \"ArrowImage\" 자동 탐색")]
    [SerializeField] Image arrowImage;
    [Tooltip("우측으로 이동할 거리(px). 0 이하면 slideTarget 자기 너비만큼 이동")]
    [SerializeField] float slideDistance = 0f;
    [SerializeField] float slideDuration = 0.3f;

    // 표시할 블록 아이템 (순서대로 순환). 처음 = 랜덤 블록.
    static readonly string[] BlockItemIds = { "blockRandom", "blockLegendary" };
    int _index;

    bool _slideCollapsed;
    Coroutine _slideCo;
    float _slideOriginX;
    bool _slideOriginCaptured;

    void Awake()
    {
        Instance = this;
        if (slideBtn != null) slideBtn.onClick.AddListener(OnClickSlide);
        if (useBtn   != null) useBtn.onClick.AddListener(OnClickUse);
    }

    void OnEnable()
    {
        _index = FirstOwnedIndex(); // 보유한 첫 블록(랜덤 우선)부터. 없으면 Refresh 에서 숨김.
        Refresh();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    string CurrentItemId => BlockItemIds[_index];

    void OnClickSlide()
    {
        if (ItemManager.Instance == null) return;
        // 보유 중인 "다음" 블록 아이템으로만 이동. 넘어갈 보유 아이템이 없으면 그대로(안 넘어감).
        int n = BlockItemIds.Length;
        for (int step = 1; step < n; step++)
        {
            int next = (_index + step) % n;
            if (ItemManager.Instance.GetCount(BlockItemIds[next]) > 0)
            {
                _index = next;
                Refresh();
                return;
            }
        }
    }

    // 접힘/펼침 토글 — 전용 버튼(추후 추가) OnClick 에 연결. 우측으로 슬라이드(접힘) ↔ 원위치(펼침) + 화살표 X축 반전.
    public void OnClickToggleSlide() => SetCollapsed(!_slideCollapsed);

    // collapsed=true → 우측으로 슬라이드(접힘), false → 원위치(펼침). 이미 목표 상태면 무시(중복 애니메이션 방지).
    void SetCollapsed(bool collapsed)
    {
        if (slideTarget == null) slideTarget = (RectTransform)transform;
        if (!_slideOriginCaptured)
        {
            _slideOriginX = slideTarget.anchoredPosition.x;
            _slideOriginCaptured = true;
        }
        if (_slideCollapsed == collapsed) return;

        _slideCollapsed = collapsed;

        float dist = slideDistance > 0f ? slideDistance : slideTarget.rect.width;
        float targetX = _slideOriginX + (_slideCollapsed ? dist : 0f);

        if (_slideCo != null) StopCoroutine(_slideCo);
        _slideCo = StartCoroutine(SlideRoutine(targetX));

        UpdateArrow();
    }

    IEnumerator SlideRoutine(float targetX)
    {
        float startX = slideTarget.anchoredPosition.x;
        float dur = Mathf.Max(0.01f, slideDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            var p = slideTarget.anchoredPosition;
            p.x = Mathf.Lerp(startX, targetX, k);
            slideTarget.anchoredPosition = p;
            yield return null;
        }
        var final = slideTarget.anchoredPosition;
        final.x = targetX;
        slideTarget.anchoredPosition = final;
        _slideCo = null;
    }

    // 접힘 상태에 따라 화살표 X축 반전(스케일 부호) — EmployeeListUI.SetStatArrow 와 동일 방식(축만 X로).
    void UpdateArrow()
    {
        var img = ResolveArrowImage();
        if (img == null) return;
        var rt = img.rectTransform;
        var s  = rt.localScale;
        float magX = Mathf.Abs(s.x);
        rt.localScale = new Vector3(_slideCollapsed ? -magX : magX, s.y, s.z);
    }

    Image ResolveArrowImage()
    {
        if (arrowImage != null) return arrowImage;
        if (slideBtn != null)
        {
            var t = slideBtn.transform.Find("ArrowImage");
            if (t != null) arrowImage = t.GetComponent<Image>();
        }
        return arrowImage;
    }

    void OnClickUse()
    {
        if (ItemManager.Instance == null) return;
        if (ItemManager.Instance.GetCount(CurrentItemId) <= 0) return;
        ItemManager.Instance.UseItemNoTarget(CurrentItemId);
        Refresh();
    }

    // 보유한 첫 블록 인덱스(랜덤 우선). 보유 없으면 0.
    int FirstOwnedIndex()
    {
        if (ItemManager.Instance != null)
            for (int i = 0; i < BlockItemIds.Length; i++)
                if (ItemManager.Instance.GetCount(BlockItemIds[i]) > 0) return i;
        return 0;
    }

    bool AnyOwned()
    {
        if (ItemManager.Instance == null) return false;
        foreach (var id in BlockItemIds)
            if (ItemManager.Instance.GetCount(id) > 0) return true;
        return false;
    }

    public void Refresh()
    {
        // 보유한 블록 아이템이 하나도 없으면 비활성화 대신 우측으로 슬라이드해 접음(다시 생기면 자동 펼침).
        bool anyOwned = AnyOwned();
        SetCollapsed(!anyOwned);
        if (!anyOwned) return;

        // 현재 항목이 0개면 보유한 항목으로 이동 — 항상 보유 항목만 표시
        if (ItemManager.Instance.GetCount(CurrentItemId) <= 0)
            _index = FirstOwnedIndex();

        string itemId = CurrentItemId;
        ItemChartLoader.Cache.TryGetValue(itemId, out var row);

        int  count = ItemManager.Instance.GetCount(itemId);
        bool owned = count > 0;

        // 이름
        if (nameText != null)
            nameText.text = row != null ? row.name : "";

        // 이미지 — 소지 중일 때만 활성화, 이미지 에셋 없으면 비워둠
        if (itemImage != null)
        {
            itemImage.gameObject.SetActive(owned);
            if (owned)
            {
                Sprite sp = row != null && !string.IsNullOrEmpty(row.imageId)
                    ? Resources.Load<Sprite>($"Items/{row.imageId}")
                    : null;
                itemImage.sprite  = sp;
                itemImage.enabled = sp != null; // 이미지 없으면 비움
            }
        }

        // 등급 프레임 — ItemSlotUI와 동일 (ItemGradeSet.Apply)
        if (frameImage != null)
        {
            frameImage.gameObject.SetActive(owned);
            if (owned && row != null)
                ItemGradeSet.Apply(frameImage, gradeSet, row.grade);
        }

        // 잔여량 (숫자만)
        if (countText != null)
            countText.text = $"{count}";

        // 사용 버튼 — 소지 중일 때만
        if (useBtn != null)
            useBtn.interactable = owned;
    }
}
