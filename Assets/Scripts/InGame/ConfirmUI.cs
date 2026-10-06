using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ConfirmUI : MonoBehaviour
{
    public static ConfirmUI Instance { get; private set; }

    [Header("Panel")]
    public GameObject confirmPanel;

    [Header("UI")]
    public TextMeshProUGUI messageText;

    [Header("Buttons")]
    public Button confirmButton;
    public Button cancelButton;
    public TextMeshProUGUI confirmButtonText;
    public TextMeshProUGUI cancelButtonText;

    [Header("다이아 결제 확인 (선택) — 비우면 confirmPanel 아래 'DiaConfirmButton' 자동 탐색")]
    public Button diaConfirmButton;
    [Tooltip("DiaConfirmButton/Label — 차감할 다이아 수량")]
    public TMP_Text diaConfirmLabel;

    private System.Action _onConfirm;
    private System.Action _onCancel;

    // 아웃게임의 다른 dim(EmployeeDetailPanel 110) 보다 위
    const int DimSortingOrder = 120;
    private bool _useScreenDim;
    private GameObject _dim;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        confirmPanel.SetActive(false);
        // 인게임은 ModalLayer(ModalBlocker 공유 dim)가 뒤를 막는다. 없는 씬(아웃게임)은 ScreenDim 으로 직접 깐다.
        _useScreenDim = confirmPanel.GetComponent<ModalLayer>() == null;

        confirmButton.onClick.AddListener(OnClickConfirm);
        cancelButton.onClick.AddListener(OnClickCancel);

        if (diaConfirmButton == null)
            foreach (var b in confirmPanel.GetComponentsInChildren<Button>(true))
                if (b.name.Trim() == "DiaConfirmButton") { diaConfirmButton = b; break; } // 씬 이름 끝에 공백이 있어 Trim
        if (diaConfirmButton != null)
        {
            if (diaConfirmLabel == null)
            {
                var label = diaConfirmButton.transform.Find("Label");
                diaConfirmLabel = label != null ? label.GetComponent<TMP_Text>() : diaConfirmButton.GetComponentInChildren<TMP_Text>(true);
            }
            diaConfirmButton.onClick.AddListener(OnClickConfirm);
        }
    }

    // confirmInteractable=false 면 확인 버튼을 표시는 하되 비활성(터치 불가)으로 띄운다. 매 호출마다 리셋되므로 기본값 true 콜러는 영향 없음.
    // diamondCost > 0 이면 일반 확인 버튼 대신 DiaConfirmButton 을 띄우고 Label 에 비용을 표시한다 (다이아 결제 확인용).
    public void Show(string message, System.Action onConfirm, System.Action onCancel = null,
                     string confirmText = "확인", string cancelText = "취소", bool confirmInteractable = true,
                     int diamondCost = 0)
    {
        GameTimeManager.Instance?.StopTime();
        ModalGate.I.Register(this);
        messageText.text       = message;
        _onConfirm             = onConfirm;
        _onCancel              = onCancel;
        if (confirmButtonText != null) confirmButtonText.text = confirmText;
        if (cancelButtonText  != null) cancelButtonText.text  = cancelText;
        if (confirmButton     != null) confirmButton.interactable = confirmInteractable;

        // 매 호출마다 리셋 — 다이아 버튼이 없는 씬(인게임)에선 항상 일반 확인 버튼
        bool useDia = diamondCost > 0 && diaConfirmButton != null;
        SetButtonSlotActive(confirmButton, !useDia);
        SetButtonSlotActive(diaConfirmButton, useDia);
        if (useDia)
        {
            diaConfirmButton.interactable = confirmInteractable;
            if (diaConfirmLabel != null) diaConfirmLabel.text = diamondCost.ToString("N0");
        }
        confirmPanel.SetActive(true);
        if (_useScreenDim) ScreenDim.Show(ref _dim, confirmPanel, DimSortingOrder);
    }

    // GlobalButtonClickBounce 가 한 번이라도 눌린 버튼을 __ClickBounceWrapper 로 감싸므로,
    // 버튼만 끄면 래퍼가 레이아웃 슬롯을 계속 차지한다 — 감싸졌으면 래퍼째로 토글.
    static void SetButtonSlotActive(Button btn, bool active)
    {
        if (btn == null) return;
        var parent = btn.transform.parent;
        if (parent != null && parent.name == "__ClickBounceWrapper") parent.gameObject.SetActive(active);
        else btn.gameObject.SetActive(active);
    }

    public void OnClickConfirm()
    {
        confirmPanel.SetActive(false);
        ScreenDim.Hide(_dim);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        _onConfirm?.Invoke();
    }

    public void OnClickCancel()
    {
        confirmPanel.SetActive(false);
        ScreenDim.Hide(_dim);
        GameTimeManager.Instance?.StartTime();
        ModalGate.I.Unregister(this);
        _onCancel?.Invoke();
    }
}