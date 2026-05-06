using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 아웃게임 MainPanel 직원 초상화 갤러리
// EmployeeMasterData(poolEmployees) 전체를 표시:
//  - 해금됨(isDefault || IsAcquired) → 본인 portrait sprite (정상 색)
//  - 잠김                          → 본인 portrait sprite + lockedTint (실루엣 효과)
// useAutoGenerate=true면 프리팹 없이 단순 Image GameObject로 자동 생성
public class OutGameMainEmployeeGallery : MonoBehaviour
{
    [Header("Container")]
    [Tooltip("초상화가 배치될 부모 (GridLayoutGroup 권장)")]
    public Transform container;

    [Header("Mode")]
    [Tooltip("체크 시 프리팹 없이 단순 Image GameObject를 자동 생성. 끄면 itemPrefab 사용")]
    public bool useAutoGenerate = false;

    [Header("Item Prefab (수동 모드)")]
    [Tooltip("직원 슬롯에 사용할 프리팹. 자식의 Image에 Portraits/{portraitId} sprite를 세팅")]
    public GameObject itemPrefab;
    [Tooltip("itemPrefab 안에서 sprite를 세팅할 Image의 이름. 비우면 첫 번째 Image 사용")]
    public string portraitImageName = "";

    [Header("Auto Generate (자동 모드)")]
    [Tooltip("자동 생성 시 Image GameObject 사이즈")]
    public Vector2 itemSize = new Vector2(120, 120);
    [Tooltip("해금된 직원 Image 색")]
    public Color tint = Color.white;

    [Header("Locked")]
    [Tooltip("잠긴 직원 Image 색 (실루엣 효과). 두 모드 공통 적용")]
    public Color lockedTint = new Color(0f, 0f, 0f, 0.6f);

    [Header("Preview (클릭 시 애니메이션)")]
    [Tooltip("선택된 캐릭터가 인스턴스화될 부모. 일반적으로 2D worldspace 또는 World Space Canvas 아래의 Transform")]
    public Transform previewContainer;
    [Tooltip("프리뷰 표시 시 함께 토글할 Image (PreviewContainer의 배경 등). 기본 disabled → 클릭 시 enabled")]
    public Image previewImage;
    [Tooltip("프리뷰 인스턴스의 localPosition")]
    public Vector3 previewLocalPosition = Vector3.zero;
    [Tooltip("프리뷰 인스턴스의 localScale")]
    public Vector3 previewLocalScale = Vector3.one;
    [Tooltip("프리뷰 인스턴스에서 Rigidbody2D/Collider2D 및 OfficeCharacter류 MonoBehaviour 제거 (Animator만 남김)")]
    public bool stripPreviewBehaviours = true;
    [Tooltip("프리뷰 인스턴스에 적용할 Layer 이름 (PreviewCamera의 Culling Mask에 맞춤). 비우면 변경 안 함")]
    public string previewLayerName = "PreviewLayer";
    [Tooltip("잠긴 직원도 클릭 시 프리뷰 가능 (실루엣 색은 유지)")]
    public bool lockedClickable = true;
    [Tooltip("클릭 시 SpriteRenderer.color에 lockedTint 적용 (잠긴 직원 프리뷰)")]
    public bool applyLockedTintToPreview = true;

    [Header("Build 타이밍")]
    [Tooltip("OnEnable에서 자동으로 빌드")]
    public bool buildOnEnable = true;
    [Tooltip("EmployeeManager 데이터 로드 대기 (poolEmployees가 빌 때 재시도)")]
    public bool waitForData = true;
    [Tooltip("waitForData 활성 시 재시도 간격 (초)")]
    public float retryInterval = 0.25f;
    [Tooltip("waitForData 활성 시 최대 대기 시간 (초). 초과 시 빌드 시도하고 종료")]
    public float maxWaitSeconds = 10f;

    private Coroutine _buildRoutine;
    private GameObject _currentPreview;

    void OnEnable()
    {
        SetPreviewVisible(false);
        if (buildOnEnable) Rebuild();
    }

    void OnDisable()
    {
        // 패널 비활성 시 PreviewAnchor도 함께 끔 (씬 루트의 anchor는 자식이 아니라 자동 비활성 안 됨)
        SetPreviewVisible(false);
    }

    void SetPreviewVisible(bool on)
    {
        if (previewImage != null) previewImage.enabled = on;
        if (previewContainer != null) previewContainer.gameObject.SetActive(on);
    }

    public void Rebuild()
    {
        if (_buildRoutine != null) StopCoroutine(_buildRoutine);
        _buildRoutine = StartCoroutine(BuildRoutine());
    }

    IEnumerator BuildRoutine()
    {
        if (waitForData)
        {
            float elapsed = 0f;
            while (elapsed < maxWaitSeconds &&
                   (EmployeeManager.Instance == null ||
                    EmployeeManager.Instance.poolEmployees == null ||
                    EmployeeManager.Instance.poolEmployees.Count == 0))
            {
                yield return new WaitForSeconds(retryInterval);
                elapsed += retryInterval;
            }
        }
        Build();
        _buildRoutine = null;
    }

    public void Build()
    {
        if (container == null)
        {
            Debug.LogWarning("[MainEmployeeGallery] container 미설정");
            return;
        }
        if (EmployeeManager.Instance == null)
        {
            Debug.LogWarning("[MainEmployeeGallery] EmployeeManager.Instance 없음 (로그인/데이터 로드 전)");
            return;
        }
        if (!useAutoGenerate && itemPrefab == null)
        {
            Debug.LogWarning("[MainEmployeeGallery] itemPrefab 미설정 (useAutoGenerate를 켜거나 프리팹을 지정하세요)");
            return;
        }

        ClearChildren();

        var pool = EmployeeManager.Instance.poolEmployees;
        if (pool == null || pool.Count == 0) return;

        // 해금 직원 먼저, 잠긴 직원은 맨 뒤로
        foreach (var emp in pool)
        {
            if (emp == null) continue;
            if (!(emp.isDefault || EmployeeManager.Instance.IsAcquired(emp.id))) continue;
            if (useAutoGenerate) CreateAutoItem(emp, true);
            else                 CreatePrefabItem(emp, true);
        }
        foreach (var emp in pool)
        {
            if (emp == null) continue;
            if (emp.isDefault || EmployeeManager.Instance.IsAcquired(emp.id)) continue;
            if (useAutoGenerate) CreateAutoItem(emp, false);
            else                 CreatePrefabItem(emp, false);
        }
    }

    void ClearChildren()
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var child = container.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    // ── 수동 모드 (프리팹 사용) ─────────────────
    void CreatePrefabItem(EmployeeData emp, bool unlocked)
    {
        var go = Instantiate(itemPrefab, container);
        go.name = unlocked ? emp.portraitId : $"locked_{emp.id}";

        var img = ResolvePortraitImage(go);
        if (img != null)
        {
            var sprite = LoadPortraitSpriteAsset(emp.portraitId);
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
                img.enabled = true;
                if (!unlocked) img.color = lockedTint;
            }
        }

        AttachClick(go, emp, unlocked);
    }

    // ── 자동 생성 모드 ──────────────────────────
    void CreateAutoItem(EmployeeData emp, bool unlocked)
    {
        var go = new GameObject(unlocked ? emp.portraitId : $"locked_{emp.id}",
            typeof(RectTransform), typeof(Image));
        go.transform.SetParent(container, false);
        ((RectTransform)go.transform).sizeDelta = itemSize;

        var img = go.GetComponent<Image>();
        img.preserveAspect = true;
        img.sprite = LoadCharacterSprite(emp.portraitId);
        img.color  = unlocked ? tint : lockedTint;

        AttachClick(go, emp, unlocked);
    }

    // ── 클릭 → 프리뷰 ───────────────────────────
    void AttachClick(GameObject go, EmployeeData emp, bool unlocked)
    {
        if (!unlocked && !lockedClickable) return;

        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();

        string portraitId = emp.portraitId;
        bool   isUnlocked = unlocked;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => ShowPreview(portraitId, isUnlocked));
    }

    public void ShowPreview(string portraitId, bool unlocked = true)
    {
        if (previewContainer == null)
        {
            Debug.LogWarning("[MainEmployeeGallery] previewContainer 미설정");
            return;
        }

        SetPreviewVisible(true);

        if (_currentPreview != null) Destroy(_currentPreview);

        var prefab = Resources.Load<GameObject>($"Characters/{portraitId}");
        if (prefab == null) return;

        var go = Instantiate(prefab, previewContainer);
        go.name = $"preview_{portraitId}";
        go.transform.localPosition = previewLocalPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = previewLocalScale;

        if (stripPreviewBehaviours) StripPreviewComponents(go);

        if (!string.IsNullOrEmpty(previewLayerName))
        {
            int layer = LayerMask.NameToLayer(previewLayerName);
            if (layer >= 0) SetLayerRecursively(go, layer);
        }

        if (!unlocked && applyLockedTintToPreview)
        {
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                sr.color = lockedTint;
        }

        // 프리뷰 애니메이션 상태 강제: 정면 / 작업중 아님
        var animator = go.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.SetBool("isFront", true);
            animator.SetBool("isWorking", false);
        }

        _currentPreview = go;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    public void ClearPreview()
    {
        if (_currentPreview != null)
        {
            Destroy(_currentPreview);
            _currentPreview = null;
        }
        SetPreviewVisible(false);
    }

    // 프리뷰에선 Animator만 살리고 이동/충돌/캐릭터 로직 컴포넌트 제거
    void StripPreviewComponents(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            Destroy(mb);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true))
            Destroy(rb);
        foreach (var col in go.GetComponentsInChildren<Collider2D>(true))
            Destroy(col);
    }

    // 수동 모드용: Resources/Portraits/{portraitId} 에서 Sprite 직접 로드
    Sprite LoadPortraitSpriteAsset(string portraitId)
    {
        if (string.IsNullOrEmpty(portraitId)) return null;
        return Resources.Load<Sprite>($"Portraits/{portraitId}");
    }

    // 자동 모드용: Resources/Characters/{portraitId} 프리팹의 SpriteRenderer.sprite 추출
    Sprite LoadCharacterSprite(string portraitId)
    {
        if (string.IsNullOrEmpty(portraitId)) return null;
        var prefab = Resources.Load<GameObject>($"Characters/{portraitId}");
        if (prefab == null) return null;
        var sr = prefab.GetComponent<SpriteRenderer>();
        return sr != null ? sr.sprite : null;
    }

    Image ResolvePortraitImage(GameObject root)
    {
        if (!string.IsNullOrEmpty(portraitImageName))
        {
            var t = FindChildByName(root.transform, portraitImageName);
            if (t != null)
            {
                var img = t.GetComponent<Image>();
                if (img != null) return img;
            }
        }
        return root.GetComponentInChildren<Image>(true);
    }

    Transform FindChildByName(Transform parent, string targetName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == targetName) return c;
            var sub = FindChildByName(c, targetName);
            if (sub != null) return sub;
        }
        return null;
    }
}
