using System.Collections;
using UnityEngine;

// ⚠ 에디터에서 GameScene 을 LoadingScene 없이 직접 실행할 때만 쓰는 임시 부트스트랩.
// 빌드 전에는 이름이 "EDITOR_" 로 시작하는 씬 루트 오브젝트를 전부 삭제할 것
// (이 오브젝트 포함 — EDITOR_ONLY_Bootstrap, EDITOR_BackendManager, EDITOR_GameSceneManager, EDITOR_SoundManager).
//
// 문제 1: GameScene 을 직접 실행하면 LoadingScene 이 만들던 매니저 싱글턴들이 없어서,
//         씬 UI 의 Awake 가 SomeManager.Instance 를 참조할 때 NRE 가 난다(DialogUI 등).
// 해결 1: 매니저 오브젝트들을 "비활성" 상태로 씬에 미리 배치해두고, 아주 이른 실행순서(-10000)의
//         이 스크립트가 Awake 에서 먼저 활성화한다 → 매니저들의 Awake(=Instance 세팅)가 다른 씬
//         UI 의 Awake(기본순서 0) 보다 먼저 완료된다.
//
// 문제 2: DontDestroyOnLoad 는 "씬 루트 오브젝트"에서만 동작한다. 매니저들을 이 오브젝트의
//         자식으로 묶어두면(정리하기는 편하지만) 각 매니저의 Awake 에서 호출하는
//         DontDestroyOnLoad(gameObject) 가 전부 조용히 실패한다("root GameObjects" 경고).
//         그 상태로는 씬이 한 번이라도 다시 로드되면 매니저가 통째로 파괴돼 .Instance 가 null이 됨.
// 해결 2: 매니저 오브젝트들은 이 오브젝트의 자식이 아니라 씬의 "루트" 형제로 둔다. 이 스크립트는
//         자기 자신을 제외하고 이름이 "EDITOR_" 로 시작하는 루트 오브젝트를 찾아 활성화한다
//         (하드코딩 참조 없이, 새 EDITOR_ 오브젝트를 추가해도 자동으로 커버됨).
//
// 문제 3: 매니저 Awake(Instance 세팅)는 여기서 즉시 끝나지만, 실제 뒤끝 로그인+데이터 로드
//         (BackendManager.Start → LoadAllAndEnterGame 콜백 체인)는 여러 프레임에 걸쳐 "비동기"로
//         끝난다. 반면 GameSceneInitializer.Start()(캐릭터 스폰/프로젝트 진행도 복원 등)는 기본
//         실행순서(0)라 데이터가 채워지기 전에 먼저 실행돼버려 빈 데이터로 끝나버린다
//         (정상 플로우는 LoadingScene 이 로드를 다 끝낸 뒤에야 GameScene 을 로드하므로 이 경쟁이 없음).
// 해결 3: GameSceneInitializer "컴포넌트만" enabled=false 로 꺼서 그 Start() 를 보류시키고,
//         BackendManager.HasInitializedThisSession(뒤끝 로드 완료 신호, 실제 로드 체인의 마지막
//         콜백에서 true 로 세팅됨)이 true 가 될 때까지 매 프레임 기다렸다가 다시 켠다. 재활성화
//         시점에 Start() 가 그제서야 실행되므로, 데이터가 다 채워진 뒤에 캐릭터 스폰/진행도 복원이
//         이뤄진다. GameObject 자체가 아니라 "컴포넌트만" 꺼야 한다 — 같은 오브젝트에 OfficeManager/
//         DeskManager/DevelopmentManager/CharacterManager 등이 같이 붙어있어서, gameObject 를 통째로
//         끄면 이들도 같이 죽어 뒤끝 로드 도중(예: CEO/비서 스폰) OfficeManager.Instance 를 참조하는
//         코드가 NRE 를 낸다. 정상 플로우에서도 안전(그 시점엔 이미 HasInitializedThisSession=true 라
//         한 프레임도 안 밀리고 바로 통과).
[DefaultExecutionOrder(-10000)]
public class EditorSceneBootstrap : MonoBehaviour
{
    const string Prefix = "EDITOR_";

    void Awake()
    {
        var gsi = FindAnyObjectByType<GameSceneInitializer>(FindObjectsInactive.Include);
        if (gsi != null && gsi.enabled)
        {
            gsi.enabled = false;
            StartCoroutine(ReactivateWhenDataLoaded(gsi));
        }

        var scene = gameObject.scene;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == gameObject) continue;
            if (!root.name.StartsWith(Prefix)) continue;
            root.SetActive(true);
        }
    }

    IEnumerator ReactivateWhenDataLoaded(GameSceneInitializer gsi)
    {
        while (!BackendManager.HasInitializedThisSession)
            yield return null;
        if (gsi != null) gsi.enabled = true;
    }
}
