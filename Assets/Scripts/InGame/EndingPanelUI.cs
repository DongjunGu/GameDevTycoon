using UnityEngine;
using UnityEngine.SceneManagement;

// 파산 / 자발적 메인메뉴 종료 공용 엔딩 화면 — LoanManager(파산), SettingsUI(메인메뉴 확인) 둘 다 여기서 Show() 호출.
public class EndingPanelUI : MonoBehaviour
{
    public static EndingPanelUI Instance { get; private set; }

    [Tooltip("엔딩 패널 루트. 비우면 이 GameObject 사용.")]
    public GameObject panelRoot;

    GameObject Panel => panelRoot != null ? panelRoot : gameObject;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        // 이 컴포넌트는 항상 활성인 호스트에 붙이고, panelRoot(EndingPanel)는 씬에 비활성으로 저장해둔다.
        // Awake에서 SetActive(false)를 직접 호출하지 않는 이유: panelRoot 자신에 이 스크립트를 붙이면
        // 비활성 상태에서 Awake가 아예 안 돌아 Instance가 끝까지 null로 남는 문제가 생긴다.
    }

    public void Show()
    {
        Panel.SetActive(true);
        GameTimeManager.Instance?.StopTime();
    }

    // EndingBtn OnClick — 아웃게임으로 이동.
    public void OnClickEnding()
    {
        SceneManager.LoadScene("OutGameScene");
    }
}
