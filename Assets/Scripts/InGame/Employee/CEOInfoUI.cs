using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인게임 CEO 정보 패널 컨트롤러 — 아웃게임에서 적용된 메타 상태를 읽기 전용으로 보여준다.
//  • LeftPanel  : 장착 특성 슬롯 (OwnedTraitManager)         — 아웃게임 TraitPanel.LeftPanel 미러
//  • PieceRight : CEO 능력치 + 강화 단계 (CEOManager→ActiveStone) — 아웃게임 PiecePanel.PieceRight 미러
//                 (8~10단계 카테고리 보너스 리스트는 PieceRight 에 붙은 StoneBonusListUI 가 자체 갱신)
//
// 구조: 이 스크립트가 붙은 CEOInfoUI 는 '항상 활성', 실제로 열고 닫는 건 자식 panelRoot(CEOInfoPanel) 토글.
//   - Open()        : panelRoot 활성 + (옵션)시간 정지 + Refresh
//   - OnClickClose(): panelRoot 비활성 + (옵션)시간 재개   (닫기 버튼 onClick 에 연결)
// 메타값이라 런 중 바뀌진 않지만, 열 때와 매니저 OnChanged(패널 열려있을 때) 시 갱신한다.
// 읽기 전용이라 특성 장착/해제·강화 버튼은 두지 않는다.
public class CEOInfoUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("열고 닫을 패널 (CEOInfoPanel). 씬에는 '비활성' 으로 저장. 이 스크립트가 붙은 루트는 항상 활성.")]
    public GameObject panelRoot;
    public Button closeButton;
    [Tooltip("패널이 열려있는 동안 게임 시간을 멈춘다 (다른 인게임 모달과 동일).")]
    public bool stopTimeWhileOpen = true;

    [Header("Trait — LeftPanel (장착 특성, 읽기 전용)")]
    [Tooltip("아웃게임 TraitPanel.LeftPanel 의 장착 슬롯들 그대로. 보통 길이 3 (OwnedTraitManager.EquipSlotCount).")]
    public TraitSlotUI[] traitSlots;
    [Tooltip("\"선택된 특성 (n/3)\" 텍스트 (선택)")]
    public TMP_Text equippedCountText;

    [Header("Trait Popup (슬롯 클릭 시 이름/설명 표시)")]
    [Tooltip("슬롯 클릭 시 띄울 팝업 루트(TraitPopUp). 씬에는 비활성으로 저장. 패널을 누르면 닫힘.")]
    public GameObject traitPopUp;
    public TMP_Text traitPopUpNameText;
    public TMP_Text traitPopUpDescText;
    [Tooltip("팝업을 누르면 닫히게 하는 버튼. 비우면 traitPopUp 의 Button 자동 사용.")]
    public Button traitPopUpButton;

    [Header("Piece — PieceRight: 현재 능력치 (base + 단계 보너스)")]
    public TMP_Text planningStatText;
    public TMP_Text developStatText;
    public TMP_Text artStatText;

    [Header("Piece — PieceRight: 강화 단계 (PieceLeft 에서 가져온 단계 숫자)")]
    public TMP_Text planningStageText;
    public TMP_Text developStageText;
    public TMP_Text artStageText;

    private bool _subscribed;

    void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClickClose);
        }

        // 팝업 패널 자체를 누르면 닫히게 (traitPopUpButton 미할당 시 traitPopUp 의 Button 사용)
        if (traitPopUpButton == null && traitPopUp != null)
            traitPopUpButton = traitPopUp.GetComponent<Button>();
        if (traitPopUpButton != null)
        {
            traitPopUpButton.onClick.RemoveAllListeners();
            traitPopUpButton.onClick.AddListener(CloseTraitPopUp);
        }
    }

    // 루트는 항상 활성 → OnEnable 은 사실상 1회. 매니저 OnChanged 구독만 (시간 정지·Refresh 는 Open 에서).
    void OnEnable()  => Subscribe();
    void OnDisable() => Unsubscribe();

    // 외부 열기 트리거(버튼 등)에서 호출. panelRoot + 닫기버튼(전체화면 백드롭)을 함께 켠다.
    // ⚠️ closeButton 은 전체화면 raycast 라 항상 켜두면 뒤 패널(채용/강화/아이템 등)을 다 막는다 → 반드시 열 때만 켤 것.
    public void Open()
    {
        if (panelRoot != null && panelRoot.activeSelf) return; // 이미 열림
        Subscribe();                                           // 매니저가 늦게 준비된 경우 대비
        if (stopTimeWhileOpen)
        {
            GameTimeManager.Instance?.StopTime();
            ModalGate.I.Register(this);
        }
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        if (panelRoot != null) panelRoot.SetActive(true);
        CloseTraitPopUp(); // 팝업은 닫힌 상태로 시작
        Refresh();
    }

    // 닫기 버튼 onClick. panelRoot + 닫기버튼을 함께 끈다 (전체화면 백드롭이 남아 뒤 UI 를 막지 않도록).
    public void OnClickClose()
    {
        if (panelRoot != null && !panelRoot.activeSelf) return; // 이미 닫힘
        if (panelRoot != null) panelRoot.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(false);
        CloseTraitPopUp();
        if (stopTimeWhileOpen)
        {
            GameTimeManager.Instance?.StartTime();
            ModalGate.I.Unregister(this);
        }
    }

    void Subscribe()
    {
        if (_subscribed) return;
        if (CEOManager.Instance == null && OwnedTraitManager.Instance == null) return; // 둘 다 없으면 다음 기회에
        if (CEOManager.Instance != null)        CEOManager.Instance.OnChanged += OnMetaChanged;
        if (OwnedTraitManager.Instance != null) OwnedTraitManager.Instance.OnChanged += OnMetaChanged;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed) return;
        if (CEOManager.Instance != null)        CEOManager.Instance.OnChanged -= OnMetaChanged;
        if (OwnedTraitManager.Instance != null) OwnedTraitManager.Instance.OnChanged -= OnMetaChanged;
        _subscribed = false;
    }

    // 패널이 열려있을 때만 갱신 (닫혀있으면 다음 Open 에서 어차피 Refresh).
    void OnMetaChanged()
    {
        if (panelRoot != null && panelRoot.activeInHierarchy) Refresh();
    }

    public void Refresh()
    {
        RefreshTraits();
        RefreshPiece();
    }

    // 장착 특성 슬롯 — 표시 + 클릭 시 팝업. 슬롯 클릭(OnClicked) 을 구독해 이름/설명 팝업을 띄운다.
    void RefreshTraits()
    {
        if (traitSlots != null)
            for (int i = 0; i < traitSlots.Length; i++)
            {
                if (traitSlots[i] == null) continue;
                traitSlots[i].Bind(i);                    // Bind 끝에서 Refresh 까지 수행
                traitSlots[i].OnClicked -= OnSlotClicked; // 중복 구독 방지
                traitSlots[i].OnClicked += OnSlotClicked;
            }

        if (equippedCountText != null && OwnedTraitManager.Instance != null)
        {
            int n = 0;
            for (int i = 0; i < OwnedTraitManager.EquipSlotCount; i++)
                if (!string.IsNullOrEmpty(OwnedTraitManager.Instance.GetEquipped(i))) n++;
            equippedCountText.text = $"선택된 특성 ({n}/{OwnedTraitManager.EquipSlotCount})";
        }
    }

    // 슬롯 클릭 — 장착된 특성이면 TraitPopUp 에 이름/설명 표시. 빈 슬롯/미장착은 무동작.
    void OnSlotClicked(int slotIndex)
    {
        var mgr = OwnedTraitManager.Instance;
        string traitId = mgr != null ? mgr.GetEquipped(slotIndex) : null;
        if (string.IsNullOrEmpty(traitId)) return;

        string traitName = traitId, desc = string.Empty;
        var cache = TraitChartLoader.Cache;
        if (cache != null && cache.TryGetValue(traitId, out var row))
        {
            traitName = row.name;
            desc      = row.description;
        }

        if (traitPopUpNameText != null) traitPopUpNameText.text = traitName;
        if (traitPopUpDescText != null) traitPopUpDescText.text = desc;
        if (traitPopUp != null) traitPopUp.SetActive(true);
    }

    // 팝업 닫기 — 팝업 패널(traitPopUpButton) 클릭 시 호출.
    public void CloseTraitPopUp()
    {
        if (traitPopUp != null) traitPopUp.SetActive(false);
    }

    // CEO 능력치/단계 — CEOManager (활성 돌 위임). 아웃게임 PiecePanel.PieceRight 와 동일 표시.
    void RefreshPiece()
    {
        var mgr = CEOManager.Instance;
        if (mgr == null) return;

        if (planningStatText != null) planningStatText.text = mgr.GetPlanning().ToString();
        if (developStatText  != null) developStatText.text  = mgr.GetDevelop().ToString();
        if (artStatText      != null) artStatText.text      = mgr.GetArt().ToString();

        if (planningStageText != null) planningStageText.text = $"{mgr.PlanningStage}단계";
        if (developStageText  != null) developStageText.text  = $"{mgr.DevelopStage}단계";
        if (artStageText      != null) artStageText.text      = $"{mgr.ArtStage}단계";
    }
}
