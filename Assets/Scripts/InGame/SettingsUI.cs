using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// 인게임 설정창. 메인메뉴 이동 + 배경음/효과음 볼륨 조절.
public class SettingsUI : MonoBehaviour
{
    [Header("설정 패널")]
    [Tooltip("열고 닫을 설정창 루트. 비우면 이 GameObject 사용.")]
    public GameObject settingsPanel;

    [Header("볼륨 슬라이더 (0~1)")]
    public Slider bgmSlider;   // 배경음
    public Slider sfxSlider;   // 효과음

    [Header("버튼")]
    public Button closeButton; // 닫기 (OnClick 자동 연결)

    GameObject Panel => settingsPanel != null ? settingsPanel : gameObject;

    void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(SettingClose);
            closeButton.onClick.AddListener(SettingClose);
        }
        // 슬라이더 → 볼륨 연결을 Awake 에서 1회 (패널을 어떤 방식으로 열든 작동)
        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
            bgmSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }
    }

    // ── 설정창 열기/닫기 ──────────────────────
    // SettingOpen 버튼(설정 아이콘 등) OnClick 에 연결. 현재 볼륨으로 슬라이더 동기화 + 리스너 연결.
    public void SettingOpen()
    {
        Panel.SetActive(true);
        GameTimeManager.Instance?.StopTime(); // 설정창 동안 시간 정지 (SettingClose 의 StartTime 과 1:1)

        // 현재 저장된 볼륨으로 슬라이더 표시값 동기화 (리스너는 Awake 에서 이미 연결됨)
        var sm = SoundManager.Instance;
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(sm != null ? sm.BgmVolume : 1f);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sm != null ? sm.SfxVolume : 1f);
    }

    public void SettingClose()
    {
        Panel.SetActive(false);
        GameTimeManager.Instance?.StartTime(); // SettingOpen 의 StopTime 해소
    }

    void OnBgmVolumeChanged(float v) => SoundManager.Instance?.SetBgmVolume(v);
    void OnSfxVolumeChanged(float v) => SoundManager.Instance?.SetSfxVolume(v);

    // ── 메인메뉴 ──────────────────────────────
    // "메인메뉴" 버튼 OnClick 에 연결.
    // ConfirmUI 로 경고 후 '네' 선택 시 런 종료 → OutGameScene.
    public void OnClickMainMenu()
    {
        if (ConfirmUI.Instance == null) { GoToMainMenu(); return; }

        ConfirmUI.Instance.Show(
            "메인메뉴로 가면 진행상황을 모두 잃습니다.\n그래도 메인메뉴로 가시겠습니까?",
            onConfirm: GoToMainMenu,
            onCancel: () => { },     // 아니오 — 아무것도 안 함
            confirmText: "네",
            cancelText: "아니오"
        );
    }

    // 런 종료(playing=false) 저장 성공 시 OutGameScene 진입.
    // 저장 실패 시 씬 진입 금지(다음 부팅에서 playing=true 로 빈 게임씬 진입 방지) — 재시도 유도. (파산 흐름과 동일 패턴)
    void GoToMainMenu()
    {
        if (RunStateManager.Instance == null)
        {
            SceneManager.LoadScene("OutGameScene");
            return;
        }

        RunStateManager.Instance.EndRun(success =>
        {
            if (success)
            {
                SceneManager.LoadScene("OutGameScene");
                return;
            }
            AlertUI.Instance?.Show(
                "진행 상태 저장에 실패했습니다.\n인터넷 상태 확인 후 다시 시도해주세요.",
                GoToMainMenu
            );
        });
    }
}
