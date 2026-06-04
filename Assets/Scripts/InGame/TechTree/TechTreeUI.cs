using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class TechTreeUI : MonoBehaviour
{
    public static TechTreeUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject techPanel;
    [Header("Scroll")]
    public ScrollRect scrollRect;

    [Header("Tabs")]
    public Button[] tabButtons; // 5개, 순서: 돈 / 채용 / 만족도 / 창의성 / 광고

    [Header("Content")]
    public Transform nodeContent;    // HorizontalLayoutGroup
    public GameObject techNodePrefab;
    public GameObject arrowPrefab;

    [Header("Points")]
    public TextMeshProUGUI currentPointsText; // "보유 N P" 또는 "N P" 표시
    [Tooltip("디버그 +N 버튼 (테스트용). 광고 row 가 들어오면 별도 획득 경로로 교체.")]
    public Button debugAddPointsButton;
    public int    debugAddAmount = 10;

    private TechCategory _currentCategory = TechCategory.Money;

    private static readonly TechCategory[] CategoryOrder =
    {
        TechCategory.Money,
        TechCategory.Hiring,
        TechCategory.Satisfaction,
        TechCategory.Creativity,
        TechCategory.Ad,
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        techPanel.SetActive(false);
    }

    void Start()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            // 광고(Ad) 카테고리 미구현 → 탭 자체를 숨김. 구현되면 이 분기만 제거.
            if (i < CategoryOrder.Length && CategoryOrder[i] == TechCategory.Ad)
            {
                tabButtons[i].gameObject.SetActive(false);
                continue;
            }
            int captured = i;
            tabButtons[i].onClick.AddListener(() => OnClickTab(captured));
        }

        if (debugAddPointsButton != null)
        {
            debugAddPointsButton.onClick.AddListener(() =>
            {
                TechTreeManager.Instance?.AddPoints(debugAddAmount);
            });
        }

        if (TechTreeManager.Instance != null)
        {
            TechTreeManager.Instance.OnPointsChanged += RefreshPointsAndCategory;
            TechTreeManager.Instance.OnUnlockChanged += RefreshPointsAndCategory;
        }
    }

    void OnDestroy()
    {
        if (TechTreeManager.Instance != null)
        {
            TechTreeManager.Instance.OnPointsChanged -= RefreshPointsAndCategory;
            TechTreeManager.Instance.OnUnlockChanged -= RefreshPointsAndCategory;
        }
    }

    void RefreshPointsAndCategory()
    {
        RefreshPointsLabel();
        if (techPanel != null && techPanel.activeInHierarchy)
            ShowCategory(_currentCategory);
    }

    void RefreshPointsLabel()
    {
        if (currentPointsText == null || TechTreeManager.Instance == null) return;
        currentPointsText.text = $"보유 포인트 {TechTreeManager.Instance.CurrentPoints}";
    }

    public void Open()
    {
        GameTimeManager.Instance?.StopTime();
        techPanel.SetActive(true);
        RefreshPointsLabel();
        RefreshTabs();
        ShowCategory(_currentCategory);
    }

    public void OnClickClose()
    {
        techPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
    }

    void OnClickTab(int index)
    {
        _currentCategory = CategoryOrder[index];
        RefreshTabs();
        ShowCategory(_currentCategory);
    }

    void RefreshTabs()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            var img = tabButtons[i].GetComponent<Image>();
            if (img == null) continue;
            img.color = CategoryOrder[i] == _currentCategory
                ? new Color(0.93f, 0.93f, 0.99f)  // 선택된 탭
                : new Color(1f, 1f, 1f, 1f);
        }
    }

    void ShowCategory(TechCategory category)
    {
        foreach (Transform child in nodeContent)
            Destroy(child.gameObject);

        // 차트 row 순서 = 해금 순서. 별도 정렬 X.
        var nodes = TechTreeManager.Instance.allNodes
            .FindAll(n => n.category == category);

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            var obj = Instantiate(techNodePrefab, nodeContent);
            var nameText = obj.transform.Find("NameText").GetComponent<TextMeshProUGUI>();
            var costText = obj.transform.Find("CostText").GetComponent<TextMeshProUGUI>();
            var badge = obj.transform.Find("UnlockBadge")?.gameObject;
            var btn = obj.GetComponent<Button>();
            var img = obj.GetComponent<Image>();

            nameText.text = node.name;

            bool canUnlock = TechTreeManager.Instance.CanUnlock(node);

            if (node.isUnlocked)
            {
                costText.text = "해금 완료";
                img.color = new Color(0.91f, 0.96f, 0.87f); // 초록
                if (badge != null) badge.SetActive(true);
                btn.interactable = true; // 해금된 노드도 클릭 시 설명 표시
            }
            else if (canUnlock)
            {
                costText.text = $"{node.requiredPoints} P";
                img.color = new Color(0.93f, 0.93f, 0.99f); // 보라
                if (badge != null) badge.SetActive(false);
                btn.interactable = true;
            }
            else
            {
                costText.text = $"{node.requiredPoints} P";
                img.color = new Color(0.85f, 0.85f, 0.85f, 0.5f); // 회색 (포인트 부족/선행 미해금)
                if (badge != null) badge.SetActive(false);
                btn.interactable = true; // 클릭하면 설명 팝업은 뜨되, 팝업 안 '해금' 버튼은 비활성
            }

            var captured = node;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnClickNode(captured));

            if (i < nodes.Count - 1 && arrowPrefab != null)
                Instantiate(arrowPrefab, nodeContent);
        }
        StartCoroutine(ResetScroll());
    }

    IEnumerator ResetScroll()
    {
        yield return null;
        yield return null;

        RectTransform targetNode = null;
        int nodeIndex = 0;

        foreach (Transform child in nodeContent)
        {
            var btn = child.GetComponent<Button>();
            if (btn == null) continue;

            nodeIndex++;

            if (btn.interactable)
            {
                if (nodeIndex >= 3)
                    targetNode = child.GetComponent<RectTransform>();
                break;
            }
        }

        if (targetNode == null || scrollRect == null)
        {
            if (scrollRect != null) scrollRect.horizontalNormalizedPosition = 0f;
            yield break;
        }

        yield return null;

        float contentWidth = nodeContent.GetComponent<RectTransform>().rect.width;
        float viewportWidth = scrollRect.viewport.rect.width;

        if (contentWidth <= viewportWidth)
        {
            scrollRect.horizontalNormalizedPosition = 0f;
            yield break;
        }

        float nodeX = targetNode.anchoredPosition.x;
        float scrollable = contentWidth - viewportWidth;
        float normalized = Mathf.Clamp01(nodeX / scrollable);

        scrollRect.horizontalNormalizedPosition = normalized;
    }

    // 노드 클릭 — 포인트 유무와 상관없이 설명 팝업을 띄운다.
    // 해금 가능(포인트 충분 + 선행 해금)하면 '해금' 버튼 활성, 아니면 표시는 하되 비활성(터치 불가).
    void OnClickNode(TechNodeData node)
    {
        // 이미 해금된 노드 — 설명만 표시(필요/보유 포인트는 공백), 해금 동작 없음.
        if (node.isUnlocked)
        {
            ConfirmUI.Instance.Show(
                $"{node.name}\n{node.description}",
                onConfirm: () => { },
                onCancel: () => { },
                confirmText: "확인",
                cancelText: "닫기"
            );
            return;
        }

        bool canUnlock = TechTreeManager.Instance.CanUnlock(node);

        ConfirmUI.Instance.Show(
            $"{node.name}\n{node.description}\n\n필요 포인트: {node.requiredPoints} P\n보유 포인트: {TechTreeManager.Instance.CurrentPoints} P",
            onConfirm: () =>
            {
                TechTreeManager.Instance.Unlock(node); // 버튼이 활성일 때만 호출됨 + Unlock 내부 CanUnlock 재확인
                // OnUnlockChanged → RefreshPointsAndCategory 에서 ShowCategory 재호출
            },
            onCancel: () => { },
            confirmText: "해금",
            cancelText: "취소",
            confirmInteractable: canUnlock
        );
    }
}
