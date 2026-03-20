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
    public Button[] tabButtons; // 5개, 순서: 만족도/효율성/장르플랫폼/참신함/유틸리티

    [Header("Content")]
    public Transform nodeContent;    // HorizontalLayoutGroup
    public GameObject techNodePrefab;
    public GameObject arrowPrefab;

    private TechCategory _currentCategory = TechCategory.EmployeeSatisfaction;

    private static readonly TechCategory[] CategoryOrder =
    {
        TechCategory.EmployeeSatisfaction,
        TechCategory.EmployeeEfficiency,
        TechCategory.GenrePlatform,
        TechCategory.Novelty,
        TechCategory.Utility
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
            int captured = i;
            tabButtons[i].onClick.AddListener(() => OnClickTab(captured));
        }
    }

    public void Open()
    {
        GameTimeManager.Instance?.StopTime();
        techPanel.SetActive(true);
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
                : new Color(1f, 1f, 1f, 1f);       // 기본 탭
        }
    }

    void ShowCategory(TechCategory category)
    {

        // 기존 노드 제거
        foreach (Transform child in nodeContent)
            Destroy(child.gameObject);

        var nodes = TechTreeManager.Instance.allNodes
            .FindAll(n => n.category == category);
        nodes.Sort((a, b) => a.order.CompareTo(b.order));

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            // 노드 생성
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
                btn.interactable = false;
            }
            else if (canUnlock)
            {
                costText.text = $"{node.cost:N0}G";
                img.color = new Color(0.93f, 0.93f, 0.99f); // 보라
                if (badge != null) badge.SetActive(false);
                btn.interactable = true;
            }
            else
            {
                costText.text = $"{node.cost:N0}G";
                img.color = new Color(0.85f, 0.85f, 0.85f, 0.5f); // 회색
                if (badge != null) badge.SetActive(false);
                btn.interactable = false;
            }

            var captured = node;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnClickNode(captured));

            // 화살표 (마지막 노드 제외)
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
            if (btn == null) continue; // 화살표 건너뜀

            nodeIndex++;

            if (btn.interactable) // 해금 가능한 노드 발견
            {
                if (nodeIndex >= 3) // 전체 노드 중 3번째 이상이면
                    targetNode = child.GetComponent<RectTransform>();
                break;
            }
        }

        if (targetNode == null || scrollRect == null)
        {
            scrollRect.horizontalNormalizedPosition = 0f;
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

    void OnClickNode(TechNodeData node)
    {
        ConfirmUI.Instance.Show(
            $"{node.name}\n비용: {node.cost:N0}G\n해금하시겠습니까?",
            onConfirm: () =>
            {
                if (!MoneyManager.Instance.CanAfford(node.cost))
                {
                    AlertUI.Instance.Show("재화가 부족합니다.");
                    return;
                }
                TechTreeManager.Instance.Unlock(node);
                ShowCategory(_currentCategory);
            },
            onCancel: () => { },
            confirmText: "해금",
            cancelText: "취소"
        );
    }
}