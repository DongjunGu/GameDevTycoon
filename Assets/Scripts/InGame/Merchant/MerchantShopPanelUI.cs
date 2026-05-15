using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상인 패널 — e5.png 패턴.
// 상단: "아이템 일람 N/Total" + 좌우 화살표
// 중단: 아이템 이미지 + 설명
// 하단: "보유 N  가격 NG"
// 하단 끝: 구입 버튼
// 닫기 X (선택)
//
// 가격은 일단 0G 고정 (ItemChartRow 에 price 컬럼 없음). 추후 price 컬럼 추가 시 row.price 로 교체.
public class MerchantShopPanelUI : MonoBehaviour
{
    [Header("Header")]
    public TMP_Text counterText; // "아이템 일람 1/3"
    public Button prevButton;
    public Button nextButton;
    public Button closeButton; // 선택. 없으면 구입/시간만 닫음

    [Header("Body")]
    public Image itemImage;
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public TMP_Text ownedText;     // "보유 N"
    public TMP_Text priceText;     // "가격 NG"

    [Header("Buy")]
    public Button buyButton;

    [Header("Defaults")]
    [Tooltip("아이템 sprite Resources 경로 prefix — imageId 와 결합. 예: 'Items/{imageId}'")]
    public string imageResourcePrefix = "Items/";

    List<string> _itemIds = new();
    int _index = 0;
    System.Action _onClosed;

    void Awake()
    {
        if (prevButton != null) { prevButton.onClick.RemoveAllListeners(); prevButton.onClick.AddListener(OnClickPrev); }
        if (nextButton != null) { nextButton.onClick.RemoveAllListeners(); nextButton.onClick.AddListener(OnClickNext); }
        if (buyButton  != null) { buyButton.onClick.RemoveAllListeners();  buyButton.onClick.AddListener(OnClickBuy); }
        if (closeButton != null){ closeButton.onClick.RemoveAllListeners();closeButton.onClick.AddListener(OnClickClose); }
        // 시작 비활성은 씬에서 이미 inactive 로 둠. 여기서 SetActive(false) 하면 Open 시 무한 재비활성 버그.
    }

    public void Open(List<string> itemIds, System.Action onClosed)
    {
        _itemIds = new List<string>(itemIds ?? new List<string>());
        _onClosed = onClosed;
        _index = 0;
        gameObject.SetActive(true);
        Refresh();
    }

    void OnClickClose()
    {
        var cb = _onClosed;
        _onClosed = null;
        gameObject.SetActive(false);
        cb?.Invoke();
    }

    void OnClickPrev()
    {
        if (_itemIds.Count == 0) return;
        _index = (_index - 1 + _itemIds.Count) % _itemIds.Count;
        Refresh();
    }

    void OnClickNext()
    {
        if (_itemIds.Count == 0) return;
        _index = (_index + 1) % _itemIds.Count;
        Refresh();
    }

    void OnClickBuy()
    {
        if (_itemIds.Count == 0) return;
        var id = _itemIds[_index];
        var row = GetRow(id);
        if (row == null) return;

        int price = GetPrice(row);
        // 가격 차감 (메모리만 — 패널 닫힐 때 MerchantManager.OnShopClosed 가 SaveMoney 일괄 호출)
        if (price > 0)
        {
            if (MoneyManager.Instance == null || !MoneyManager.Instance.SpendGold(price, saveImmediately: false))
            {
                Debug.Log("[Merchant] 골드 부족 — 구입 실패");
                return;
            }
        }

        // 인벤토리도 메모리만 — 닫힐 때 ItemManager.Save 일괄 호출
        ItemManager.Instance?.AddItemNoSave(id, 1);
        Debug.Log($"[Merchant] 구입(메모리): {row.name} (1개) -{price}G");

        // 목록에서 제거
        _itemIds.RemoveAt(_index);
        if (_itemIds.Count == 0)
        {
            // 다 사면 자동 닫기 → OnShopClosed 콜백에서 batched save
            OnClickClose();
            return;
        }
        if (_index >= _itemIds.Count) _index = _itemIds.Count - 1;
        Refresh();
    }

    void Refresh()
    {
        bool hasItems = _itemIds.Count > 0;
        if (prevButton != null) prevButton.interactable = hasItems && _itemIds.Count > 1;
        if (nextButton != null) nextButton.interactable = hasItems && _itemIds.Count > 1;
        if (buyButton  != null) buyButton.interactable  = hasItems;

        if (!hasItems)
        {
            if (counterText != null) counterText.text = "아이템 일람 0/0";
            if (nameText != null) nameText.text = "";
            if (descriptionText != null) descriptionText.text = "";
            if (ownedText != null) ownedText.text = "";
            if (priceText != null) priceText.text = "";
            if (itemImage != null) { itemImage.sprite = null; itemImage.enabled = false; }
            return;
        }

        var id = _itemIds[_index];
        var row = GetRow(id);
        if (row == null) return;

        if (counterText != null) counterText.text = $"아이템 일람 {_index + 1}/{_itemIds.Count}";
        if (nameText != null) nameText.text = row.name;
        if (descriptionText != null) descriptionText.text = row.description;
        if (ownedText != null) ownedText.text = $"보유 {(ItemManager.Instance?.GetCount(id) ?? 0)}";
        int price = GetPrice(row);
        if (priceText != null) priceText.text = $"가격 {price:N0}G";

        if (itemImage != null)
        {
            Sprite sp = null;
            if (!string.IsNullOrEmpty(row.imageId))
                sp = Resources.Load<Sprite>(imageResourcePrefix + row.imageId);
            itemImage.sprite = sp;
            itemImage.enabled = (sp != null);
            itemImage.preserveAspect = true;
        }
    }

    static ItemChartRow GetRow(string id)
    {
        var cache = ItemChartLoader.Cache;
        if (cache == null) return null;
        cache.TryGetValue(id, out var row);
        return row;
    }

    // 가격은 ItemChartRow.price 사용. 차트에 없으면 1G 폴백.
    static int GetPrice(ItemChartRow row) => row != null && row.price > 0 ? row.price : 1;
}
