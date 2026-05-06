using System.Collections.Generic;
using UnityEngine;

// ContainerPanel 안에 보유 카드 GridLayout 빌드
// OwnedCardManager.OnChanged 구독 → 자동 재빌드
public class OwnedCardContainerUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("카드가 배치될 부모 (GridLayoutGroup)")]
    public Transform container;
    [Tooltip("OwnedCardItemUI가 부착된 프리팹")]
    public GameObject itemPrefab;

    [Header("Build")]
    public bool buildOnEnable = true;

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

        // 정렬: empId 알파벳 → grade 내림차순 (높은 등급이 먼저)
        var keys = new List<string>(OwnedCardManager.Instance.AllCards.Keys);
        keys.Sort((a, b) =>
        {
            int barA = a.IndexOf('|');
            int barB = b.IndexOf('|');
            string ea = barA > 0 ? a.Substring(0, barA) : a;
            string eb = barB > 0 ? b.Substring(0, barB) : b;
            int empCmp = string.Compare(ea, eb, System.StringComparison.Ordinal);
            if (empCmp != 0) return empCmp;
            int ga = barA > 0 ? int.Parse(a.Substring(barA + 1)) : 0;
            int gb = barB > 0 ? int.Parse(b.Substring(barB + 1)) : 0;
            return gb.CompareTo(ga); // 높은 등급 먼저
        });

        foreach (var key in keys)
        {
            int n = OwnedCardManager.Instance.AllCards[key];
            if (n <= 0) continue;

            int barIdx = key.IndexOf('|');
            if (barIdx <= 0) continue;
            string empId = key.Substring(0, barIdx);
            int gInt = int.Parse(key.Substring(barIdx + 1));
            var grade = (EmployeeGrade)gInt;

            var masterEmp = FindMasterEmployee(empId);

            // 중복은 N장 모두 별도 카드로 표시
            for (int i = 0; i < n; i++)
            {
                var go = Instantiate(itemPrefab, container);
                go.name = $"card_{empId}_{gInt}_{i}";
                var item = go.GetComponent<OwnedCardItemUI>();
                if (item == null) item = go.GetComponentInChildren<OwnedCardItemUI>(true);
                if (item != null) item.SetData(empId, grade, masterEmp);
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
}
