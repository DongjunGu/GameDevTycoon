using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 테크트리 UI 개편(2026-08) — 탭 전환식 단일 카테고리 표시 대신, TechCategoryPanel(카테고리 라벨 5개) +
// TechNodePanel(TechNodeChildPanel1~5, 카테고리별 Node1..N 버튼이 씬에 고정 배치) 그리드를 한 화면에서
// 스크롤로 훑어보는 구조로 변경. 노드는 Instantiate/Destroy 하지 않고(씬에 이미 만들어져 있음) 상태(색상)만
// 갱신한다. 클릭 시 우측 TechTreeDetailPanel(아이콘/이름/카테고리/설명/필요포인트/해금버튼)에 상세 표시 —
// 기존 별도 팝업(TechTreeConfirmUI, ConfirmUI 오브젝트에 얹혀있던 컴포넌트)은 삭제되고 이걸로 대체됨.
public class TechTreeUI : MonoBehaviour
{
    public static TechTreeUI Instance { get; private set; }

    [Header("Panel — TechTreePanel(그리드+디테일+닫기버튼 전체) 자체를 토글")]
    public GameObject techPanel;

    [Header("Points")]
    public TextMeshProUGUI currentPointsText; // "보유 포인트 N"
    [Tooltip("디버그 +N 버튼 (테스트용)")]
    public Button debugAddPointsButton;
    public int    debugAddAmount = 10;

    [Header("Node Grid — TechNodeChildPanel1~5, 순서 고정: [광고, 채용, 만족도, 돈, 창의성]")]
    public GameObject[] techNodeChildPanels; // 5개. 각 자식 Button(Node1..N)이 카테고리 내 차트 row 순서와 1:1 매칭
    [Tooltip("노드 해금/미해금 상태 스프라이트 — 노드 Image에 그대로 교체")]
    public Sprite nodeUnlockedSprite;
    public Sprite nodeLockedSprite;
    [Tooltip("TechCategoryPanel/TechCategory1~5/TechUnlockCountText — techNodeChildPanels와 동일 순서, \"해금수/총수\" 표시")]
    public TextMeshProUGUI[] techUnlockCountTexts;

    [Header("Detail Panel — 기본 비활성, 노드 클릭 시 활성화")]
    public GameObject        techTreeDetailPanel;
    public Image             techIconImage;          // 아이콘 (현재 에셋 없음 — 항상 비활성)
    public TextMeshProUGUI   techNodeNameText;
    public TextMeshProUGUI   techDetailCategoryText;
    public TextMeshProUGUI   techDescriptionText;
    public TextMeshProUGUI   techCostText;
    public Button            techUnlockBtn;   // 미해금 — "해금" 버튼
    public TextMeshProUGUI   techUnlockText;
    public GameObject        techLockIcon;
    public GameObject        techUnlockedBtn; // 해금 완료 — techUnlockBtn 과 자리 대체(둘이 반대로 토글)

    // TechNodeChildPanel1~5 의 카테고리 배정 순서 (사용자 지정: 광고/채용/만족도/돈/창의성)
    private static readonly TechCategory[] PanelCategoryOrder =
    {
        TechCategory.Ad,
        TechCategory.Hiring,
        TechCategory.Satisfaction,
        TechCategory.Money,
        TechCategory.Creativity,
    };

    private TechNodeData _selectedNode;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        techPanel.SetActive(false);
        if (techTreeDetailPanel != null) techTreeDetailPanel.SetActive(false);
    }

    void Start()
    {
        if (debugAddPointsButton != null)
        {
            debugAddPointsButton.onClick.AddListener(() =>
            {
                Debug.Log($"[TechTreeUI] debugAddPointsButton 클릭됨 (TechTreeManager.Instance={(TechTreeManager.Instance != null)})");
                if (TechTreeManager.Instance == null) { Debug.LogWarning("[TechTreeUI] TechTreeManager.Instance가 null — 포인트 추가 불가"); return; }
                TechTreeManager.Instance.AddPoints(debugAddAmount);
            });
        }

        if (techUnlockBtn != null)
            techUnlockBtn.onClick.AddListener(OnClickUnlock);

        if (TechTreeManager.Instance != null)
        {
            TechTreeManager.Instance.OnPointsChanged += RefreshAll;
            TechTreeManager.Instance.OnUnlockChanged += RefreshAll;
        }
    }

    void OnDestroy()
    {
        if (TechTreeManager.Instance != null)
        {
            TechTreeManager.Instance.OnPointsChanged -= RefreshAll;
            TechTreeManager.Instance.OnUnlockChanged -= RefreshAll;
        }
    }

    void RefreshAll()
    {
        RefreshPointsLabel();
        if (techPanel == null || !techPanel.activeInHierarchy) return;

        BuildNodeGrid();
        if (_selectedNode != null) ShowDetail(_selectedNode); // 해금/포인트 변화 반영해 디테일도 같이 갱신
    }

    void RefreshPointsLabel()
    {
        if (currentPointsText == null || TechTreeManager.Instance == null) return;
        currentPointsText.text = $"보유 포인트 {TechTreeManager.Instance.CurrentPoints}";
    }

    public void Open()
    {
        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        techPanel.SetActive(true);
        _selectedNode = null;
        if (techTreeDetailPanel != null) techTreeDetailPanel.SetActive(false); // 노드 선택 전엔 비활성
        RefreshPointsLabel();
        BuildNodeGrid();
    }

    public void OnClickClose()
    {
        techPanel.SetActive(false);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
    }

    // 카테고리별 ChildPanel의 자식 버튼(Node1..N)들을 차트 row 순서와 매칭해 상태(색상)만 갱신.
    // 씬에 고정 배치된 노드를 그대로 재사용 — Instantiate/Destroy 없음.
    void BuildNodeGrid()
    {
        if (TechTreeManager.Instance == null || techNodeChildPanels == null) return;

        for (int p = 0; p < techNodeChildPanels.Length && p < PanelCategoryOrder.Length; p++)
        {
            var panel = techNodeChildPanels[p];
            if (panel == null) continue;
            var category = PanelCategoryOrder[p];
            var nodes = TechTreeManager.Instance.allNodes.FindAll(n => n.category == category);

            if (techUnlockCountTexts != null && p < techUnlockCountTexts.Length && techUnlockCountTexts[p] != null)
            {
                int unlockedCount = 0;
                foreach (var n in nodes) if (n.isUnlocked) unlockedCount++;
                techUnlockCountTexts[p].text = $"{unlockedCount}/{nodes.Count}";
            }

            int nodeIndex = 0;
            foreach (Transform child in panel.transform)
            {
                // GlobalButtonClickBounce가 클릭된 버튼을 __ClickBounceWrapper 자식으로 한 단계 감싸
                // 부모를 바꿔버리므로(sibling index는 유지됨), 직계에 Button이 없으면 한 단계 더 내려가 찾는다.
                var btn = child.GetComponent<Button>();
                if (btn == null) btn = child.GetComponentInChildren<Button>(true);
                if (btn == null) continue; // LineImage 등 순수 장식 요소는 스킵

                if (nodeIndex >= nodes.Count)
                {
                    SetButtonSlotActive(btn, false); // 데이터보다 슬롯이 많으면 남는 슬롯은 숨김
                    nodeIndex++;
                    continue;
                }

                var node = nodes[nodeIndex];
                SetButtonSlotActive(btn, true);
                ApplyNodeVisual(btn, node);

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnClickNode(node));

                nodeIndex++;
            }

            if (nodeIndex < nodes.Count)
                Debug.LogWarning($"[TechTreeUI] {category} 노드 {nodes.Count}개가 TechNodeChildPanel{p + 1} 슬롯({nodeIndex}개)보다 많음 — 초과분 미표시");
        }
    }

    // GlobalButtonClickBounce가 클릭된 버튼을 __ClickBounceWrapper로 감싸면 부모가 바뀌어, 버튼 자신만
    // SetActive해도 래퍼가 활성 상태로 남아 레이아웃 슬롯을 계속 차지한다(EmployeeListUI와 동일 패턴).
    static void SetButtonSlotActive(Button btn, bool active)
    {
        if (btn == null) return;
        var parent = btn.transform.parent;
        if (parent != null && parent.name == "__ClickBounceWrapper")
            parent.gameObject.SetActive(active);
        else
            btn.gameObject.SetActive(active);
    }

    void ApplyNodeVisual(Button btn, TechNodeData node)
    {
        var img = btn.GetComponent<Image>();
        if (img == null) return;

        if (node.isUnlocked)
        {
            if (nodeUnlockedSprite != null) img.sprite = nodeUnlockedSprite;
            img.color = Color.white;
        }
        else
        {
            if (nodeLockedSprite != null) img.sprite = nodeLockedSprite;
            img.color = TechTreeManager.Instance.CanUnlock(node)
                ? new Color(0.93f, 0.93f, 0.99f)       // 해금 가능 — 보라 틴트
                : new Color(0.85f, 0.85f, 0.85f, 0.5f); // 포인트 부족/선행 미해금 — 회색 틴트
        }
    }

    void OnClickNode(TechNodeData node)
    {
        _selectedNode = node;
        ShowDetail(node);
    }

    void ShowDetail(TechNodeData node)
    {
        if (techTreeDetailPanel != null) techTreeDetailPanel.SetActive(true); // 노드 클릭 시에만 활성화

        // 아이콘 — 현재 노드별 아이콘 에셋이 없어 슬롯만 비워둠(추후 에셋 추가 시 여기서 로드/할당).
        if (techIconImage != null) techIconImage.enabled = false;

        if (techNodeNameText     != null) techNodeNameText.text     = node.name;
        if (techDetailCategoryText != null) techDetailCategoryText.text = GetCategoryLabel(node.category);
        if (techDescriptionText  != null) techDescriptionText.text  = node.description;
        if (techCostText         != null) techCostText.text         = $"{node.requiredPoints} P";

        // 해금 완료 노드는 techUnlockBtn 대신 techUnlockedBtn을 보여준다(서로 반대로 토글).
        if (techUnlockBtn    != null) techUnlockBtn.gameObject.SetActive(!node.isUnlocked);
        if (techUnlockedBtn  != null) techUnlockedBtn.SetActive(node.isUnlocked);

        if (!node.isUnlocked)
        {
            if (techLockIcon   != null) techLockIcon.SetActive(true);
            if (techUnlockText != null) techUnlockText.text = "해금";
            // 포인트 부족이어도 버튼은 눌리게 두고(interactable 고정 true), 클릭 시 OnClickUnlock에서 부족 안내.
            if (techUnlockBtn  != null) techUnlockBtn.interactable = true;
        }
    }

    // TechTreeDetailPanel/TechBottomPanel/TechUnlockBtn 에 연결 — 공용 ConfirmUI로 확인 후 해금.
    // 해금 불가(포인트 부족 등) 상태에서도 버튼은 눌리므로, 그 경우 안내만 띄우고 리턴.
    void OnClickUnlock()
    {
        if (_selectedNode == null || _selectedNode.isUnlocked) return;

        if (!TechTreeManager.Instance.CanUnlock(_selectedNode))
        {
            // bypassGate:true — TechTreePanel 자신이 이미 ModalGate를 쥐고 있어(Open()에서 Register),
            // 안 그러면 큐에 쌓이기만 하고 TechTreePanel을 닫아야 게이트가 풀리면서 뒤늦게 표시된다.
            AlertUI.Instance?.Show("포인트가 부족합니다.", bypassGate: true);
            return;
        }

        var node = _selectedNode;
        if (ConfirmUI.Instance == null) { TechTreeManager.Instance.Unlock(node); return; }

        ConfirmUI.Instance.Show(
            $"{node.name}을(를) 해금하시겠습니까?\n(-{node.requiredPoints}P)",
            onConfirm: () =>
            {
                if (TechTreeManager.Instance.CanUnlock(node)) // 팝업 떠있는 동안 포인트/선행 변화 대비 재확인
                    TechTreeManager.Instance.Unlock(node);
                // OnUnlockChanged → RefreshAll 이 그리드/디테일 패널을 함께 갱신
            },
            onCancel: () => { },
            confirmText: "해금",
            cancelText:  "취소"
        );
    }

    static string GetCategoryLabel(TechCategory c) => c switch
    {
        TechCategory.Money        => "돈",
        TechCategory.Hiring       => "채용",
        TechCategory.Satisfaction => "만족도",
        TechCategory.Creativity   => "창의성",
        TechCategory.Ad           => "광고",
        _ => ""
    };
}
