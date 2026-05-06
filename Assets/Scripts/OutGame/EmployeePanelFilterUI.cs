using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// EmployeePanel 필터 바 — 6개 토글 버튼
// 클릭 시 OutGameEmployeePanelGallery.SetMode(...) 호출 + 활성 버튼 시각 강조
public class EmployeePanelFilterUI : MonoBehaviour
{
    [Serializable]
    public class ModeButton
    {
        public EmployeeGalleryMode mode;
        public Button button;
        [Tooltip("선택 시 색이 바뀔 라벨 (TMP)")]
        public TMP_Text labelText;
        [Tooltip("선택 시 켜질 인디케이터 (밑줄/하이라이트)")]
        public GameObject activeIndicator;
    }

    [Header("References")]
    public OutGameEmployeePanelGallery gallery;

    [Header("Buttons")]
    public ModeButton[] buttons;

    [Header("Visual")]
    public Color activeLabelColor   = Color.white;
    public Color inactiveLabelColor = new Color(1f, 1f, 1f, 0.45f);

    [Header("Default")]
    public EmployeeGalleryMode defaultMode = EmployeeGalleryMode.All;

    [Header("Toggle (FilterBar show/hide)")]
    [Tooltip("토글 대상 — 보통 FilterBar GameObject")]
    public GameObject filterBarRoot;
    [Tooltip("클릭 시 filterBarRoot 활성/비활성 토글하는 버튼")]
    public Button toggleButton;
    [Tooltip("시작 시 FilterBar 노출 여부")]
    public bool startVisible = true;

    void Start()
    {
        WireButtons();
        ApplyMode(defaultMode, propagate: true);

        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveAllListeners();
            toggleButton.onClick.AddListener(ToggleFilterBar);
        }
        if (filterBarRoot != null) filterBarRoot.SetActive(startVisible);
    }

    public void ToggleFilterBar()
    {
        if (filterBarRoot == null) return;
        filterBarRoot.SetActive(!filterBarRoot.activeSelf);
    }

    void WireButtons()
    {
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
        {
            var entry = buttons[i];
            if (entry == null || entry.button == null) continue;
            var captured = entry.mode;
            entry.button.onClick.RemoveAllListeners();
            entry.button.onClick.AddListener(() => OnFilterClicked(captured));
        }
    }

    void OnFilterClicked(EmployeeGalleryMode m)
    {
        ApplyMode(m, propagate: true);
        if (filterBarRoot != null) filterBarRoot.SetActive(false);
    }

    void ApplyMode(EmployeeGalleryMode m, bool propagate)
    {
        if (buttons != null)
        {
            foreach (var entry in buttons)
            {
                if (entry == null) continue;
                bool active = entry.mode == m;
                if (entry.activeIndicator != null) entry.activeIndicator.SetActive(active);
                if (entry.labelText != null) entry.labelText.color = active ? activeLabelColor : inactiveLabelColor;
            }
        }
        if (propagate && gallery != null) gallery.SetMode(m);
    }
}
