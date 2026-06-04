using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 창의성 미니게임 상단 블록 아이템 패널.
// 보유 중인 블록 아이템(랜덤/전설)만 한 번에 하나씩 표시하고 SlideBtn 으로 순환한다.
// - 열릴 때 보유한 첫 블록(랜덤 우선)부터 표시. 하나도 없으면 패널 내용 숨김(빈 "0개"도 안 띄움).
// - Text: 아이템 이름 / CountText: 잔여 수량(N개) / BlockItemImage: 이미지(에셋 없으면 비움)
// - UseBtn: 현재 표시 중인 아이템 사용
public class BlockItemPanelUI : MonoBehaviour
{
    public static BlockItemPanelUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] Image           itemImage; // BlockItemImage
    [SerializeField] TextMeshProUGUI nameText;  // Text
    [SerializeField] Button          slideBtn;  // 다음 아이템으로 전환
    [SerializeField] Button          useBtn;    // 현재 아이템 사용
    [SerializeField] TextMeshProUGUI countText; // 잔여량 n개

    // 표시할 블록 아이템 (순서대로 순환). 처음 = 랜덤 블록.
    static readonly string[] BlockItemIds = { "blockRandom", "blockLegendary" };
    int _index;

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

    // 보유 아이템이 없을 때 패널 내용(이미지/이름/수량/버튼 + 배경)을 숨김.
    // 루트 GameObject 는 살려둬 다음 OnEnable/Refresh 가 정상 동작하도록 함.
    void SetContentActive(bool on)
    {
        if (nameText  != null) nameText.gameObject.SetActive(on);
        if (countText != null) countText.gameObject.SetActive(on);
        if (slideBtn  != null) slideBtn.gameObject.SetActive(on);
        if (useBtn    != null) useBtn.gameObject.SetActive(on);
        if (!on && itemImage != null) itemImage.gameObject.SetActive(false);
        var bg = GetComponent<Image>();
        if (bg != null) bg.enabled = on;
    }

    public void Refresh()
    {
        // 보유한 블록 아이템이 하나도 없으면 내용 숨김 (빈 "0개"도 안 띄움)
        bool anyOwned = AnyOwned();
        SetContentActive(anyOwned);
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

        // 잔여량
        if (countText != null)
            countText.text = $"{count}개";

        // 사용 버튼 — 소지 중일 때만
        if (useBtn != null)
            useBtn.interactable = owned;
    }
}
