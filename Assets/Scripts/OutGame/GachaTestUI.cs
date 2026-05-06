using UnityEngine;
using UnityEngine.UI;

// 임시 뽑기 테스트 — 정식 가챠 미구현 자리
// 클릭 시 풀에서 랜덤 직원 + Normal~Epic 랜덤 등급 1장 추가
public class GachaTestUI : MonoBehaviour
{
    [Header("References")]
    public Button gachaButton;
    [Tooltip("한 번 클릭에 뽑을 카드 수")]
    public int drawCount = 1;

    [Header("Grade 분포 (가중치, 합산 후 비례 추첨)")]
    [Range(0, 100)] public int weightNormal = 60;
    [Range(0, 100)] public int weightRare   = 30;
    [Range(0, 100)] public int weightEpic   = 10;

    void Awake()
    {
        if (gachaButton != null)
        {
            gachaButton.onClick.RemoveListener(OnClickGacha);
            gachaButton.onClick.AddListener(OnClickGacha);
        }
    }

    void OnClickGacha()
    {
        if (EmployeeManager.Instance?.poolEmployees == null) return;
        if (OwnedCardManager.Instance == null) return;
        var pool = EmployeeManager.Instance.poolEmployees;
        if (pool.Count == 0) return;

        for (int i = 0; i < drawCount; i++)
        {
            var emp = pool[Random.Range(0, pool.Count)];
            var grade = RollGrade();
            // 마스터 풀의 maxGrade 상한 적용 (있다면)
            if ((int)grade > (int)emp.maxGrade) grade = emp.maxGrade;
            OwnedCardManager.Instance.AddCard(emp.id, grade, save: i == drawCount - 1);
            Debug.Log($"[Gacha] +{emp.employeeName} ({grade})");
        }
    }

    EmployeeGrade RollGrade()
    {
        int total = Mathf.Max(1, weightNormal + weightRare + weightEpic);
        int r = Random.Range(0, total);
        if (r < weightNormal) return EmployeeGrade.Normal;
        if (r < weightNormal + weightRare) return EmployeeGrade.Rare;
        return EmployeeGrade.Epic;
    }
}
