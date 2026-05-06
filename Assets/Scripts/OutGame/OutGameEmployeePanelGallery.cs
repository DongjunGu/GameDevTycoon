using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum EmployeeGalleryMode
{
    All,            // 전체 (해금 우선 + 마스터 순서)
    GradeDesc,      // 등급 높은 순
    GradeAsc,       // 등급 낮은 순
    RolePlanner,    // 기획 필터
    RoleProgrammer, // 개발 필터
    RoleArtist      // 아트 필터
}

// 아웃게임 EmployeePanel 갤러리
// - EmployeeMasterData 풀에서 모드(All/GradeDesc/GradeAsc/Role*)에 따라 정렬·필터 후 ItemPrefab 생성
// - 해금 직원 우선, 잠긴 직원은 뒤로
// - 클릭 시 preview 표시 (메인 갤러리와 동일 RT 파이프라인)
public class OutGameEmployeePanelGallery : MonoBehaviour
{
    [Header("Container")]
    [Tooltip("ItemPrefab이 배치될 부모 (GridLayoutGroup)")]
    public Transform container;

    [Header("Item")]
    [Tooltip("EmployeePanelItemUI가 부착된 프리팹")]
    public GameObject itemPrefab;

    [Header("Preview")]
    [Tooltip("선택된 캐릭터가 인스턴스화될 부모 Transform")]
    public Transform previewContainer;
    [Tooltip("프리뷰 표시 시 함께 토글할 Image (배경/RT)")]
    public Image previewImage;
    public Vector3 previewLocalPosition = Vector3.zero;
    public Vector3 previewLocalScale    = Vector3.one;
    [Tooltip("프리뷰 인스턴스에서 Rigidbody2D/Collider2D/MonoBehaviour 제거")]
    public bool stripPreviewBehaviours = true;
    [Tooltip("프리뷰 인스턴스에 적용할 Layer (PreviewCamera Culling Mask와 매칭)")]
    public string previewLayerName = "PreviewLayer";

    [Header("Locked")]
    public Color lockedTint = new Color(0f, 0f, 0f, 0.6f);
    public bool  lockedClickable = true;
    public bool  applyLockedTintToPreview = true;

    [Header("Description")]
    [Tooltip("등급 표시 패널 (DescriptionPanel) — 클릭한 직원의 maxGrade 위 슬롯을 잠금 처리")]
    public EmployeePanelDescriptionUI descriptionUI;

    [Header("Sort / Filter")]
    [Tooltip("정렬·필터 모드 (UI에서 SetMode로 변경)")]
    public EmployeeGalleryMode mode = EmployeeGalleryMode.All;

    [Header("Build 타이밍")]
    public bool buildOnEnable = true;
    public bool waitForData   = true;
    public float retryInterval  = 0.25f;
    public float maxWaitSeconds = 10f;

    private Coroutine _buildRoutine;
    private GameObject _currentPreview;
    private readonly List<EmployeePanelItemUI> _spawned = new();

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
        if (previewImage != null)
        {
            previewImage.enabled = on;
            // PreviewContainer의 모든 자식(NameText, DescriptionPanel 등) 토글
            // PreviewContainer GameObject 자체는 layout 슬롯이라 끄지 않음 (내부만 토글)
            var t = previewImage.transform;
            for (int i = 0; i < t.childCount; i++)
                t.GetChild(i).gameObject.SetActive(on);
        }
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
        if (container == null || itemPrefab == null) return;
        if (EmployeeManager.Instance == null) return;

        ClearChildren();

        var pool = EmployeeManager.Instance.poolEmployees;
        if (pool == null || pool.Count == 0) return;

        // 1) 모드별 필터
        var filtered = new List<EmployeeData>();
        foreach (var emp in pool)
        {
            if (emp == null) continue;
            if (!PassFilter(emp)) continue;
            filtered.Add(emp);
        }

        // 2) 정렬: 항상 해금 우선, 그 다음 모드별 2차 키
        filtered.Sort((a, b) =>
        {
            bool ua = a.isDefault || EmployeeManager.Instance.IsAcquired(a.id);
            bool ub = b.isDefault || EmployeeManager.Instance.IsAcquired(b.id);
            if (ua != ub) return ua ? -1 : 1;
            return CompareByMode(a, b);
        });

        foreach (var emp in filtered)
        {
            bool unlocked = emp.isDefault || EmployeeManager.Instance.IsAcquired(emp.id);
            CreateItem(emp, unlocked);
        }
    }

    bool PassFilter(EmployeeData emp)
    {
        switch (mode)
        {
            case EmployeeGalleryMode.RolePlanner:    return emp.role == EmployeeRole.Planner;
            case EmployeeGalleryMode.RoleProgrammer: return emp.role == EmployeeRole.Programmer;
            case EmployeeGalleryMode.RoleArtist:     return emp.role == EmployeeRole.Artist;
            default: return true;
        }
    }

    int CompareByMode(EmployeeData a, EmployeeData b)
    {
        switch (mode)
        {
            case EmployeeGalleryMode.GradeDesc: return ((int)b.maxGrade).CompareTo((int)a.maxGrade);
            case EmployeeGalleryMode.GradeAsc:  return ((int)a.maxGrade).CompareTo((int)b.maxGrade);
            default: return 0; // 풀 원본 순서 유지
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
        _spawned.Clear();
    }

    void CreateItem(EmployeeData emp, bool unlocked)
    {
        var go = Instantiate(itemPrefab, container);
        go.name = unlocked ? emp.id : $"locked_{emp.id}";

        var item = go.GetComponent<EmployeePanelItemUI>();
        if (item == null) item = go.GetComponentInChildren<EmployeePanelItemUI>(true);
        if (item != null)
        {
            item.SetData(emp, unlocked);
            item.OnClicked += HandleItemClicked;
            _spawned.Add(item);
        }
    }

    void HandleItemClicked(EmployeePanelItemUI item)
    {
        if (item == null || item.Data == null) return;
        if (!item.IsUnlocked && !lockedClickable) return;
        ShowPreview(item.Data.portraitId, item.IsUnlocked);
        if (descriptionUI != null) descriptionUI.ApplyEmployee(item.Data, item.IsUnlocked);
    }

    public void ShowPreview(string portraitId, bool unlocked = true)
    {
        if (previewContainer == null) return;
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

        var animator = go.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.SetBool("isFront", true);
            animator.SetBool("isWorking", false);
        }

        _currentPreview = go;
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

    void StripPreviewComponents(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true))   Destroy(rb);
        foreach (var col in go.GetComponentsInChildren<Collider2D>(true))    Destroy(col);
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    // ── 외부 API ────────────────────────────────
    public void SetMode(EmployeeGalleryMode m)
    {
        if (mode == m) return;
        mode = m;
        Rebuild();
    }
}
