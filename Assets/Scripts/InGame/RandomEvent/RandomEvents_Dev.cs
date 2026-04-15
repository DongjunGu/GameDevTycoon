using System.Collections.Generic;
using UnityEngine;

public static class RandomEvents_Dev
{
    public static void Register(List<RandomEventData> pool, RandomEventManager mgr)
    {
        pool.Add(new RandomEventData
        {
            type = RandomEventType.Blackout,
            title = "정전 발생!",
            description = "갑작스러운 정전으로\n작업 데이터가 손실되었습니다.\n기획, 개발, 아트 수치가 절반으로 감소합니다.",
            weight = mgr.blackoutWeight,
            onApply = () =>
            {
                DevelopmentPanelUI.Instance.MultiplyValues(0.5f);
                foreach (var e in EmployeeManager.Instance.ownedEmployees)
                {
                    e.ChangeSatisfaction(-10);
                    EmployeeManager.Instance.UpdateEmployee(e);
                }
            }
        });

        pool.Add(new RandomEventData
        {
            type = RandomEventType.TeamDinner,
            title = "회식 진행!",
            description = "팀원들의 사기가\n올랐습니다!\n기획, 개발, 아트 수치가 2배로 증가합니다.",
            weight = mgr.teamDinnerWeight,
            onApply = () =>
            {
                DevelopmentPanelUI.Instance.MultiplyValues(2.0f);
                foreach (var e in EmployeeManager.Instance.ownedEmployees)
                {
                    e.ChangeSatisfaction(+10);
                    EmployeeManager.Instance.UpdateEmployee(e);
                }
            }
        });

        pool.Add(new RandomEventData
        {
            type = RandomEventType.DevBoost,
            title = "개발 돌파구 발견!",
            description = "팀이 기술적 난관을\n극복했습니다!\n개발 수치가 50 증가합니다.",
            weight = mgr.devBoostWeight,
            onApply = () => DevelopmentPanelUI.Instance.AddValues(0f, 50f, 0f, 0f, 0f)
        });

        pool.Add(new RandomEventData
        {
            type = RandomEventType.PlanBoost,
            title = "기획 아이디어 폭발!",
            description = "기획팀에서 혁신적인\n아이디어가 나왔습니다!\n기획 수치가 50 증가합니다.",
            weight = mgr.planBoostWeight,
            onApply = () => DevelopmentPanelUI.Instance.AddValues(50f, 0f, 0f, 0f, 0f)
        });

        pool.Add(new RandomEventData
        {
            type = RandomEventType.ArtBoost,
            title = "아트 영감 폭발!",
            description = "아트팀이 놀라운 영감을\n받았습니다!\n아트 수치가 50 증가합니다.",
            weight = mgr.artBoostWeight,
            onApply = () => DevelopmentPanelUI.Instance.AddValues(0f, 0f, 50f, 0f, 0f)
        });

        pool.Add(new RandomEventData
        {
            type = RandomEventType.CreativityBoost,
            title = "창의적 영감!",
            description = "팀에 창의적인\n아이디어가 넘칩니다!\n창의성이 30 증가합니다.",
            weight = mgr.creativityBoostWeight,
            onApply = () => DevelopmentPanelUI.Instance.AddValues(0f, 0f, 0f, 0f, 30f)
        });

        pool.Add(new RandomEventData
        {
            type = RandomEventType.NetworkIssue,
            title = "네트워크 장애 발생!",
            description = "네트워크 장애로 인해\n2주간 개발속도가 50% 느려집니다.\n개발기간이 2주 늘어납니다.",
            weight = mgr.networkIssueWeight,
            onApply = () =>
            {
                float secondsPerWeek = ProjectSetupUI.SelectedScale switch
                {
                    ProjectScale.Small  => 5f,
                    ProjectScale.Medium => 4.2f,
                    ProjectScale.Large  => 3.9f,
                    _ => 5f
                };
                DevelopmentManager.Instance.ExtendDevelopmentDuration(2f * secondsPerWeek);
            }
        });

        pool.Add(new RandomEventData
        {
            type = RandomEventType.EmployeeScout,
            title = "스카우트 제안!",
            description = "경쟁사에서 직원에게\n스카우트 제안을 했습니다!",
            weight = mgr.employeeScoutWeight,
            onApply = () => mgr.TriggerScoutEvent()
        });

        pool.Add(new RandomEventData
        {
            type = RandomEventType.BetaTestIssue,
            title = "베타 테스트 유출!",
            description = "베타 테스트 영상이\n유출되었습니다!",
            weight = mgr.betaTestIssueWeight,
            onApply = () => mgr.TriggerBetaTestEvent()
        });
    }
}
