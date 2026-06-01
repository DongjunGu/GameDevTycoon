using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 직원 등급을 Image 색으로 표시하는 공용 헬퍼.
// Normal/Rare/Epic = 단색, Unique = 황금 반짝임, Legendary = 무지개 반짝임(코루틴).
// 모달 패널에서 게임 시간이 멈춰도 보이도록 unscaledTime 사용.
public static class EmployeeGradeColor
{
    public static readonly Color Normal = new Color(0.92f, 0.92f, 0.92f);
    public static readonly Color Rare   = new Color(0.75f, 0.88f, 0.95f);
    public static readonly Color Epic   = new Color(0.55f, 0.30f, 0.85f);

    static readonly Color GoldA = new Color(1.00f, 0.85f, 0.30f);
    static readonly Color GoldB = new Color(0.85f, 0.65f, 0.10f);

    // grade 색을 img 에 적용. Unique/Legendary 는 host 에서 코루틴을 시작하고 핸들 반환(단색은 null).
    // 재적용 시 호출자가 이전 핸들을 StopCoroutine 으로 정지해야 함.
    public static Coroutine Apply(MonoBehaviour host, Image img, EmployeeGrade grade)
    {
        if (img == null) return null;

        bool canRun = host != null && host.isActiveAndEnabled;
        switch (grade)
        {
            case EmployeeGrade.Legendary:
                if (canRun) return host.StartCoroutine(LegendaryShimmer(img));
                img.color = Color.HSVToRGB(0f, 0.55f, 1f);
                return null;
            case EmployeeGrade.Unique:
                if (canRun) return host.StartCoroutine(UniqueShimmer(img));
                img.color = GoldA;
                return null;
            case EmployeeGrade.Rare: img.color = Rare; return null;
            case EmployeeGrade.Epic: img.color = Epic; return null;
            default:                 img.color = Normal; return null;
        }
    }

    public static IEnumerator UniqueShimmer(Image img)
    {
        const float speed = 2.0f;
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * speed) + 1f) / 2f;
            img.color = Color.Lerp(GoldB, GoldA, t);
            yield return null;
        }
    }

    public static IEnumerator LegendaryShimmer(Image img)
    {
        while (true)
        {
            float h = Mathf.Repeat(Time.unscaledTime * 0.25f, 1f);
            img.color = Color.HSVToRGB(h, 0.55f, 1f);
            yield return null;
        }
    }
}
