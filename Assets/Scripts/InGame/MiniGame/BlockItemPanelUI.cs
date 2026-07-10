using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 창의성 미니게임 상단 블록 아이템 패널.
// blockRandom → ItemPanel1, blockLegendary → ItemPanel2 에 각각 항상 함께 표시한다(더 이상 순환 X).
// 패널을 클릭하면 선택(DimImage 활성화 + BlockItemImage 스프라이트 교체) — ItemPanel1/2 중 하나만
// 선택 가능. 선택된 상태에서 UseBtn 을 누르면 그 아이템을 사용한다. 둘 다 미보유면 이 오브젝트
// (BlockItemPanel) 자체를 비활성화.
// SlideBtn: 패널 전체를 오른쪽으로 슬라이드해 접었다 펼쳤다 하는 토글(자동 숨김과는 별개, 수동 조작).
public class BlockItemPanelUI : MonoBehaviour
{
    public static BlockItemPanelUI Instance { get; private set; }

    [System.Serializable]
    public class Slot
    {
        public string           itemId;
        [Tooltip("ItemPanel1/2 — 클릭 대상이자 owned 여부에 따라 상호작용 제어")]
        public Button           selectButton;
        public Image            itemImage;
        public Image            frameImage; // 등급 프레임 (ItemSlotUI와 동일)
        public ItemGradeSet     gradeSet;
        public TextMeshProUGUI  nameText;
        public TextMeshProUGUI  countText;  // 잔여량 (숫자만)
        [Tooltip("선택 시 활성화되는 Dim 오버레이")]
        public GameObject       dimImage;
        [Tooltip("선택 시 교체할 배경 이미지 컴포넌트")]
        public Image            blockItemImage;
        [Tooltip("선택 시 blockItemImage 에 적용할 스프라이트 (평소 스프라이트는 시작 시 자동 기억)")]
        public Sprite           selectedSprite;

        [System.NonSerialized] public Sprite normalSprite;
    }

    [Header("슬롯")]
    [SerializeField] Slot slot1; // blockRandom
    [SerializeField] Slot slot2; // blockLegendary

    [Header("공용")]
    [SerializeField] Button useBtn; // 선택된 아이템 사용

    [Header("접힘/펼침 슬라이드 (SlideBtn 전용)")]
    [Tooltip("우측으로 슬라이드될 대상 — 비우면 이 오브젝트의 RectTransform")]
    [SerializeField] RectTransform slideTarget;
    [SerializeField] Button slideBtn;
    [Tooltip("접힘 상태 표시용 화살표. 비우면 slideBtn 자식 \"ArrowImage\" 자동 탐색")]
    [SerializeField] Image arrowImage;
    [Tooltip("우측으로 이동할 거리(px). 0 이하면 slideTarget 자기 너비만큼 이동")]
    [SerializeField] float slideDistance = 0f;
    [SerializeField] float slideDuration = 0.3f;

    Slot[] _slots;
    int _selected = -1; // -1 = 선택 없음

    bool _slideCollapsed;
    Coroutine _slideCo;
    float _slideOriginX;
    bool _slideOriginCaptured;

    void Awake()
    {
        Instance = this;
        _slots = new[] { slot1, slot2 };

        for (int i = 0; i < _slots.Length; i++)
        {
            int idx = i;
            var s = _slots[i];
            if (s.blockItemImage != null) s.normalSprite = s.blockItemImage.sprite;
            if (s.selectButton != null)
                s.selectButton.onClick.AddListener(() => OnClickSelect(idx));
        }
        if (useBtn != null) useBtn.onClick.AddListener(OnClickUse);
        if (slideBtn != null) slideBtn.onClick.AddListener(OnClickToggleSlide);
    }

    void OnEnable() => Refresh();

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnClickSelect(int index)
    {
        if (ItemManager.Instance == null) return;
        if (ItemManager.Instance.GetCount(_slots[index].itemId) <= 0) return; // 미보유는 선택 불가
        _selected = (_selected == index) ? -1 : index; // 같은 걸 다시 누르면 선택 해제
        UpdateSelectionVisual();
    }

    void OnClickUse()
    {
        if (_selected < 0 || ItemManager.Instance == null) return;
        string itemId = _slots[_selected].itemId;
        if (ItemManager.Instance.UseItemNoTarget(itemId))
        {
            _selected = -1;
            Refresh();
        }
    }

    void UpdateSelectionVisual()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            var s = _slots[i];
            bool selected = i == _selected;
            if (s.dimImage != null) s.dimImage.SetActive(selected);
            if (s.blockItemImage != null)
                s.blockItemImage.sprite = selected && s.selectedSprite != null ? s.selectedSprite : s.normalSprite;
        }
        if (useBtn != null) useBtn.interactable = _selected >= 0;
    }

    public void Refresh()
    {
        if (ItemManager.Instance == null) return;

        bool anyOwned = false;
        for (int i = 0; i < _slots.Length; i++)
        {
            bool owned = ItemManager.Instance.GetCount(_slots[i].itemId) > 0;
            anyOwned |= owned;
            PopulateSlot(_slots[i], owned);
        }

        gameObject.SetActive(anyOwned);
        if (!anyOwned)
        {
            _selected = -1;
            return;
        }

        // 선택된 항목을 사용해 0개가 됐으면 선택 해제
        if (_selected >= 0 && ItemManager.Instance.GetCount(_slots[_selected].itemId) <= 0)
            _selected = -1;

        UpdateSelectionVisual();
    }

    void PopulateSlot(Slot s, bool owned)
    {
        ItemChartLoader.Cache.TryGetValue(s.itemId, out var row);
        int count = ItemManager.Instance.GetCount(s.itemId);

        if (s.nameText != null)
            s.nameText.text = row != null ? row.name : "";
        if (s.countText != null)
            s.countText.text = $"{count}";

        // 이미지 — 소지 중일 때만 활성화, 이미지 에셋 없으면 비워둠
        if (s.itemImage != null)
        {
            s.itemImage.gameObject.SetActive(owned);
            if (owned)
            {
                Sprite sp = row != null && !string.IsNullOrEmpty(row.imageId)
                    ? Resources.Load<Sprite>($"Items/{row.imageId}")
                    : null;
                s.itemImage.sprite  = sp;
                s.itemImage.enabled = sp != null;
            }
        }

        // 등급 프레임 — ItemSlotUI와 동일 (ItemGradeSet.Apply)
        if (s.frameImage != null)
        {
            s.frameImage.gameObject.SetActive(owned);
            if (owned && row != null)
                ItemGradeSet.Apply(s.frameImage, s.gradeSet, row.grade);
        }

        if (s.selectButton != null) s.selectButton.interactable = owned;
        if (s.dimImage != null && !owned) s.dimImage.SetActive(false);
    }

    // ── 접힘/펼침 슬라이드 ─────────────────────────────
    public void OnClickToggleSlide() => SetCollapsed(!_slideCollapsed);

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

    // 나와있는 상태(펼침) = 원래 방향, 슬라이드로 들어간 상태(접힘) = X축 반전
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
}
