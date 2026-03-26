using System.Collections.Generic;

public static class RandomEvents_Release
{
    public static void Register(List<RandomEventData> pool, RandomEventManager mgr)
    {
        pool.Add(new RandomEventData
        {
            type          = RandomEventType.CompetitorRelease,
            title         = "경쟁사 출시!",
            description   = "경쟁사에서 비슷한 게임을 출시...\n점수에 불이익이 생겼습니다.",
            triggerChance = mgr.competitorChance,
            scoreBonus    = -3f,
        });

        pool.Add(new RandomEventData
        {
            type          = RandomEventType.PerfectTiming,
            title         = "완벽한 타이밍!",
            description   = "아주 좋은 타이밍에 출시했어요!\n점수에 보너스가 생겼습니다.",
            triggerChance = mgr.perfectTimingChance,
            scoreBonus    = 3f,
        });

        pool.Add(new RandomEventData
        {
            type          = RandomEventType.AlgorithmChoice,
            title         = "알고리즘의 선택!",
            description   = "출시작이 알고리즘을 타고\n역주행 중입니다!\n매출이 일시적으로 상승합니다.",
            triggerChance = mgr.algorithmChance,
            scoreBonus    = 0f,
            onApply       = () => mgr.TriggerAlgorithmEvent()
        });
    }
}