using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

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

    // RandomEventType enum 개수 (경계 구분용)
    static readonly string[] _enumNames = Enum.GetNames(typeof(RandomEventType));

    // 조건 이벤트 목록 (CSV eventType 기준)
    static readonly string[] _conditionEventKeys =
    {
        "BadRumor",
        "AnxietyInducing",
        "CompanyBadReview",
        "OfficeRomance",
        "RomanceBrokeUp",
        "CoupleResignation",
        "LeaderBurnout",
        "LeaderJealousy",
        // "VoluntaryOvertime", // 야근모드 비활성화 — RandomEvents_Condition.TriggerVoluntaryOvertimeEvent 자체를 주석 처리함
    };

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
        var options = new List<string>(_enumNames);
        foreach (var key in _conditionEventKeys)
            options.Add($"[조건] {key}");
        eventDropdown.AddOptions(options);
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
        int idx = eventDropdown.value;

        // ── 일반 RandomEventType ──
        if (idx < _enumNames.Length)
        {
            if (Enum.TryParse(_enumNames[idx], out RandomEventType type))
                RandomEventManager.Instance?.TriggerEventTest(type);
            return;
        }

        // ── 조건 이벤트 ──
        string key = _conditionEventKeys[idx - _enumNames.Length];
        TriggerConditionEvent(key);
    }

    void TriggerConditionEvent(string key)
    {
        var mgr  = RandomEventManager.Instance;
        var year = GameTimeManager.Instance?.Year ?? 2000;

        // 직원 1명 (비CEO, 비파견)
        EmployeeData emp1 = null, emp2 = null;
        if (EmployeeManager.Instance != null)
        {
            foreach (var e in EmployeeManager.Instance.ownedEmployees)
            {
                if (e.isCEO) continue;
                if (DispatchManager.Instance != null && DispatchManager.Instance.IsDispatched(e.id)) continue;
                if (emp1 == null) { emp1 = e; continue; }
                if (emp2 == null) { emp2 = e; break; }
            }
        }

        switch (key)
        {
            case "BadRumor":
                if (mgr != null) RandomEvents_Condition.TriggerBadRumorEvent(mgr, year);
                break;
            case "AnxietyInducing":
                RandomEvents_Condition.TriggerAnxietyInducingEvent();
                break;
            case "CompanyBadReview":
                if (mgr != null) RandomEvents_Condition.TriggerCompanyBadReviewEvent(mgr, year);
                break;
            case "OfficeRomance":
                if (mgr != null && emp1 != null && emp2 != null)
                    RandomEvents_Condition.TriggerOfficeRomanceEvent(mgr, emp1.id, emp2.id);
                else Debug.LogWarning("[PatrolTestUI] OfficeRomance: 직원 2명 이상 필요");
                break;
            case "RomanceBrokeUp":
                if (mgr != null && emp1 != null && emp2 != null)
                    RandomEvents_Condition.TriggerRomanceBrokeUpEvent(mgr, emp1.id, emp2.id);
                else Debug.LogWarning("[PatrolTestUI] RomanceBrokeUp: 직원 2명 이상 필요");
                break;
            case "CoupleResignation":
                if (emp1 != null)
                    RandomEvents_Condition.TriggerCoupleResignationEvent(emp1.id);
                break;
            case "LeaderBurnout":
                if (emp1 != null)
                    RandomEvents_Condition.TriggerLeaderBurnoutEvent(emp1, 3, null);
                break;
            case "LeaderJealousy":
                if (emp1 != null)
                    RandomEvents_Condition.TriggerLeaderJealousyEvent(emp1, null);
                break;
            // 야근모드 비활성화 — TriggerVoluntaryOvertimeEvent 자체를 주석 처리했으므로 이 케이스도 비활성화.
            // case "VoluntaryOvertime":
            //     if (emp1 != null)
            //         RandomEvents_Condition.TriggerVoluntaryOvertimeEvent(emp1);
            //     break;
        }
    }
}
