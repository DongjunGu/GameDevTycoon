using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 하단 상태바의 직원별 슬롯 — 초상화 + 만족도 슬라이더
// - employeeId의 satisfaction을 자연스럽게 보간(MoveTowards) 표시
// - EmployeeCardUI의 색상 규칙 그대로 (81+ 초록 / 61+ 파랑 / 41+ 주황 / 빨강)
// - 슬롯 클릭 시 EmployeeCardUI 표시 (캐릭터 클릭과 동일)
public class EmployeeSatisfactionSlider : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    public Image portraitImage;
    public Slider slider;
    [Header("파견중 표시 (옵션)")]
    public GameObject dispatchedBadge;

    [Header("Animation")]
    [Tooltip("1초당 변화할 만족도 단위 (값이 클수록 빠름)")]
    public float animSpeed = 30f;

    private string _employeeId;
    public string EmployeeId => _employeeId;

    void Awake()
    {
        EnsureReferences();
    }

    // 인스펙터에 안 꽂혀도 자식에서 자동 탐색 + 슬라이더 기본 설정
    // 비활성 부모 아래에서 Instantiate된 경우 Awake 호출이 지연되므로
    // SetEmployee에서도 호출해 항상 reference 보장
    void EnsureReferences()
    {
        if (slider == null) slider = GetComponentInChildren<Slider>(true);
        if (portraitImage == null)
        {
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject == gameObject) continue;                         // 루트의 배경 Image 제외
                if (slider != null && img.transform.IsChildOf(slider.transform))    // 슬라이더 내부 Background/Fill 제외
                    continue;
                portraitImage = img;
                break;
            }
        }
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.interactable = false; // 드래그/조작 불가, 시각용
        }
    }

    public void SetEmployee(string employeeId)
    {
        _employeeId = employeeId;
        EnsureReferences();

        var emp = EmployeeManager.Instance?.GetEmployee(employeeId);
        if (emp == null) return;

        if (portraitImage != null && !string.IsNullOrEmpty(emp.portraitId))
        {
            var sprite = Resources.Load<Sprite>($"Portraits/{emp.portraitId}");
            if (sprite != null) portraitImage.sprite = sprite;
        }
        if (slider != null)
        {
            slider.value = emp.satisfaction; // 첫 표시 즉시 세팅 (애니메이션 없이)
            EmployeeCardUI.ApplySatisfactionColor(slider, emp.satisfaction);
        }

        RefreshDispatchVisual();
    }

    // 파견 시작/복귀 시 실시간으로 희미하게/원복 + badge 갱신 (슬롯은 재생성되지 않으므로 구독 필요)
    void OnEnable()
    {
        if (DispatchManager.Instance != null) DispatchManager.Instance.OnDispatchChanged += RefreshDispatchVisual;
        RefreshDispatchVisual();
    }

    void OnDisable()
    {
        if (DispatchManager.Instance != null) DispatchManager.Instance.OnDispatchChanged -= RefreshDispatchVisual;
    }

    void RefreshDispatchVisual()
    {
        // 상태바는 파견중이어도 카드 열람은 허용 (blockSelect=false)
        DispatchSlotVisual.Apply(this, dispatchedBadge, _employeeId, blockSelect: false);
    }

    void Update()
    {
        if (string.IsNullOrEmpty(_employeeId) || slider == null) return;
        var emp = EmployeeManager.Instance?.GetEmployee(_employeeId);
        if (emp == null) return;

        slider.value = Mathf.MoveTowards(
            slider.value,
            emp.satisfaction,
            animSpeed * Time.deltaTime
        );
        EmployeeCardUI.ApplySatisfactionColor(slider, Mathf.RoundToInt(slider.value));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(_employeeId)) return;
        EmployeeCardUI.Instance?.Show(_employeeId);
    }
}
