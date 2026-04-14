using System.Collections.Generic;

public static class RandomEvents_Condition
{
    public static readonly string[] ResignationMessages =
    {
        "안녕히 계세요 여러분\n전 이 세상의 모든 굴레와 속박을 벗어 던지고\n제 행복을 찾아 떠납니다.",
        "건강상의 사유로 그만두겠습니다.\n진단명은... 사장님 알레르기!"
    };
    public const string ResignationOvertimeMessage = "야근 도저히 못해먹겠네\n난 퇴사할껍니다!";

    public static readonly string[] RunAwayMessages =
    {
        "책상 위에 놓인 사원증이 반으로 쪼개져 있습니다.",
        "프로필 상태가 '구직 중'으로 바뀌었습니다.",
        "책상 위에 포스트잇 한 장이 붙어 있습니다.\n'회사 탈출은 지능순'"
    };

    public static void Register(List<RandomEventData> pool, RandomEventManager mgr)
    {
        pool.Add(new RandomEventData
        {
            type          = RandomEventType.EmployeeFight,
            title         = "직원 불화 발생!",
            description   = "직원 두 명이 크게 싸웠습니다!\n누구의 편을 들겠습니까?",
            triggerChance = mgr.employeeFightChance,
            onApply       = () => mgr.TriggerEmployeeFightEvent()
        });

        pool.Add(new RandomEventData
        {
            type          = RandomEventType.BadCompany,
            title         = "악명 높은 기업!",
            description   = "잦은 해고로 악명이 높아졌습니다!\n채용 조건이 불리해집니다.",
            triggerChance = mgr.badCompanyChance,
            onApply       = () => mgr.TriggerBadCompanyEvent()
        });
    }
}