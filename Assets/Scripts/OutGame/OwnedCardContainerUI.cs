using System;
using System.Collections.Generic;
using UnityEngine;

// ContainerPanel 안에 보유 카드 GridLayout 빌드
// OwnedCardManager.OnChanged 구독 → 자동 재빌드
// 정렬·필터 모드는 EmployeeGalleryMode 재사용 (EmployeePanel 갤러리와 동일 UX)
// 카드 클릭 → OnCardClicked 이벤트 (MergeUI 등 외부 리스너에 전달)
public class OwnedCardContainerUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("카드가 배치될 부모 (GridLayoutGroup)")]
    public Transform container;
    [Tooltip("OwnedCardItemUI가 부착된 프리팹")]
    public GameObject itemPrefab;

    [Header("Build")]
    public bool buildOnEnable = true;

    [Header("Sort / Filter")]
    [Tooltip("정렬·필터 모드 (UI에서 SetMode로 변경)")]
    public EmployeeGalleryMode mode = EmployeeGalleryMode.All;

    public event Action<OwnedCardItemUI> OnCardClicked;

    void OnEnable()
    {
        if (OwnedCardManager.Instance != null)
            OwnedCardManager.Instance.OnChanged += Rebuild;
        if (buildOnEnable) Rebuild();
    }

    void OnDisable()
    {
        if (OwnedCardManager.Instance != null)
            OwnedCardManager.Instance.OnChanged -= Rebuild;
    }

    public void Rebuild()
    {
        if (container == null || itemPrefab == null) return;
        if (OwnedCardManager.Instance == null) return;

        ClearChildren();

        var all = OwnedCardManager.Instance.AllCards;
        var keys = new List<string>();
        foreach (var kv in all)
        {
            if (kv.Value <= 0) continue;
            if (!PassFilter(kv.Key)) continue;
            keys.Add(kv.Key);
        }

        keys.Sort(CompareByMode);

        foreach (var key in keys)
        {
            int n = all[key];
            OwnedCardManager.ParseKey(key, out var empId, out var grade, out var stage);
            var masterEmp = FindMasterEmployee(empId);

            for (int i = 0; i < n; i++)
            {
                var go = Instantiate(itemPrefab, container);
                go.name = $"card_{empId}_{(int)grade}_{stage}_{i}";
                var item = go.GetComponent<OwnedCardItemUI>();
                if (item == null) item = go.GetComponentInChildren<OwnedCardItemUI>(true);
                if (item != null)
                {
                    item.SetData(empId, grade, stage, masterEmp);
                    item.OnClicked += HandleItemClicked;
                }
            }
        }
    }

    void HandleItemClicked(OwnedCardItemUI item) => OnCardClicked?.Invoke(item);

    bool PassFilter(string key)
    {
        if (mode != EmployeeGalleryMode.RolePlanner &&
            mode != EmployeeGalleryMode.RoleProgrammer &&
            mode != EmployeeGalleryMode.RoleArtist) return true;

        OwnedCardManager.ParseKey(key, out var empId, out _, out _);
        var emp = FindMasterEmployee(empId);
        if (emp == null) return false;
        return mode switch
        {
            EmployeeGalleryMode.RolePlanner    => emp.role == EmployeeRole.Planner,
            EmployeeGalleryMode.RoleProgrammer => emp.role == EmployeeRole.Programmer,
            EmployeeGalleryMode.RoleArtist     => emp.role == EmployeeRole.Artist,
            _ => true,
        };
    }

    int CompareByMode(string a, string b)
    {
        OwnedCardManager.ParseKey(a, out var ea, out var ga, out var sa);
        OwnedCardManager.ParseKey(b, out var eb, out var gb, out var sb);
        switch (mode)
        {
            case EmployeeGalleryMode.GradeDesc:
            {
                int c = ((int)gb).CompareTo((int)ga);
                if (c != 0) return c;
                int s = sb.CompareTo(sa);
                if (s != 0) return s;
                return string.Compare(ea, eb, StringComparison.Ordinal);
            }
            case EmployeeGalleryMode.GradeAsc:
            {
                int c = ((int)ga).CompareTo((int)gb);
                if (c != 0) return c;
                int s = sa.CompareTo(sb);
                if (s != 0) return s;
                return string.Compare(ea, eb, StringComparison.Ordinal);
            }
            default:
            {
                // empId 알파벳 → grade 내림차순 → stage 내림차순
                int c = string.Compare(ea, eb, StringComparison.Ordinal);
                if (c != 0) return c;
                int g = ((int)gb).CompareTo((int)ga);
                if (g != 0) return g;
                return sb.CompareTo(sa);
            }
        }
    }

    EmployeeData FindMasterEmployee(string empId)
    {
        if (EmployeeManager.Instance?.poolEmployees == null) return null;
        foreach (var e in EmployeeManager.Instance.poolEmployees)
            if (e != null && e.id == empId) return e;
        return null;
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

    public void SetMode(EmployeeGalleryMode m)
    {
        if (mode == m) return;
        mode = m;
        Rebuild();
    }

    // 합성 매칭 카드를 앞으로 sibling 재정렬 + 비매칭은 dim
    // sameEmpId가 비어있으면 grade/stage만 일치 (Epic+1/Unique+1 합성)
    public void ApplyMergeHighlight(EmployeeGrade matGrade, int matStage, string sameEmpId)
    {
        if (container == null) return;
        var matched = new List<OwnedCardItemUI>();
        var unmatched = new List<OwnedCardItemUI>();
        foreach (var it in container.GetComponentsInChildren<OwnedCardItemUI>(true))
        {
            if (it == null) continue;
            bool matches = it.Grade == matGrade && it.Stage == matStage
                && (string.IsNullOrEmpty(sameEmpId) || it.EmployeeId == sameEmpId);
            if (matches) matched.Add(it);
            else unmatched.Add(it);
        }
        int idx = 0;
        foreach (var it in matched)   { it.transform.SetSiblingIndex(idx++); it.SetDimmed(false); }
        foreach (var it in unmatched) { it.transform.SetSiblingIndex(idx++); it.SetDimmed(true);  }
    }

    // dim 해제 + 원래 모드 정렬로 복원
    public void ClearMergeHighlight()
    {
        if (container == null) return;
        foreach (var it in container.GetComponentsInChildren<OwnedCardItemUI>(true))
            if (it != null) it.SetDimmed(false);
        Rebuild();
    }
}
