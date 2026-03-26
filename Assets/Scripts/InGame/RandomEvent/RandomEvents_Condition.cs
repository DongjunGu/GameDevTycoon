using System.Collections.Generic;

public static class RandomEvents_Condition
{
    public static void Register(List<RandomEventData> pool, RandomEventManager mgr)
    {
        pool.Add(new RandomEventData
        {
            type          = RandomEventType.EmployeeRun,
            title         = "직원 도망!",
            description   = "만족도가 너무 낮아\n직원이 사라졌습니다!\n다른 직원들의 만족도도 하락합니다.",
            triggerChance = mgr.employeeRunChance,
            onApply       = () => mgr.TriggerEmployeeRunEvent()
        });

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