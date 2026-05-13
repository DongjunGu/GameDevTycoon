using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// patrol 테스트용 버튼 UI.
/// 씬의 Canvas 하위 빈 GameObject에 추가 후 버튼 연결.
/// </summary>
public class PatrolTestUI : MonoBehaviour
{
    [Header("Random N명")]
    public Button btnPatrolRandom;
    public int randomCount = 1;

    [Header("직원획득")]
    public Button btnAquiredTest;

    [Header("다이얼로그 Patrol (랜덤 직원)")]
    public Button btnDialogPatrol;

    [Header("이벤트 테스트")]
    public TMP_Dropdown eventDropdown;
    public Button btnTestEvent;

    [Header("상인 방문")]
    public Button btnMerchantVisit;

    void Start()
    {
        btnPatrolRandom?.onClick.AddListener(OnClickRandom);
        btnAquiredTest?.onClick.AddListener(OnClickAquired);
        btnDialogPatrol?.onClick.AddListener(OnClickDialogPatrol);
        btnTestEvent?.onClick.AddListener(OnClickTestEvent);
        btnMerchantVisit?.onClick.AddListener(OnClickMerchantVisit);

        SetupEventDropdown();
    }

    void OnClickMerchantVisit()
    {
        MerchantManager.Instance?.TestVisit();
    }

    void SetupEventDropdown()
    {
        if (eventDropdown == null) return;

        eventDropdown.ClearOptions();
        var names = Enum.GetNames(typeof(RandomEventType));
        eventDropdown.AddOptions(new System.Collections.Generic.List<string>(names));
    }

    void OnClickRandom()
    {
        OfficeManager.Instance?.TriggerPatrolRandom(randomCount);
    }

    void OnClickAquired()
    {
        EmployeeManager.Instance?.AcquireEmployee("otaku_01");
        Debug.Log("직원 획득");
    }

    void OnClickDialogPatrol()
    {
        OfficeManager.Instance?.TriggerDialogPatrolRandom();
    }

    void OnClickTestEvent()
    {
        if (eventDropdown == null) return;
        var names = Enum.GetNames(typeof(RandomEventType));
        if (eventDropdown.value >= names.Length) return;

        if (Enum.TryParse(names[eventDropdown.value], out RandomEventType type))
            RandomEventManager.Instance?.TriggerEventTest(type);
    }
}
