# -*- coding: utf-8 -*-
"""
이벤트 데이터 — 두 가지 버전
  "code"  : 지금 게임 코드에 들어 있는 그대로 (RandomEvent*_Chart.csv + RandomEvents_*.cs)
  "guide" : 노션/바탕화면 '이벤트_밸런싱_가이드' §5 설계안 (신규 이벤트 포함)

효과는 원시 단위 튜플로 적는다. 처리는 balance_sim.py의 apply_effects()가 한다.
  ("sat", v)              대상 직원 만족도 ±v
  ("sat2", v)             두 번째 대상(싸움 상대) 만족도
  ("sat_all", v)          전 직원 만족도
  ("money_r", v)          돈 −(R_ref × v%)            v는 숫자 또는 (1,2,3,4단계) 튜플
  ("gain_r", v)           돈 +(R_ref × v%)
  ("money_salary", v)     돈 −(총 연봉 × v%)
  ("money_emp_salary", v) 돈 −(대상 직원 연봉 × v%)
  ("part_pct", v)         파트 점수 ±v% (μ_part 기준 — 최종 파트 점수에 배율로 적용). 파트 = ctx["part"] 또는 대상 직군
  ("part2_pct", v)        두 번째 대상 직군 파트
  ("leader_pct", v)       대상 직군의 '지금까지 쌓인 팀장 점수' × v% (최소 1점) — 현재 코드의 싸움 이벤트
  ("leader2_pct", v)
  ("delay", n)            개발 기간 + n × (소형 1 / 중형 2 / 대형 3주)
  ("buff", pct, w)        대상 능력치 +pct%, 기간 w      w = ("L", 배수) | ("n",) 코드식 5~15주(1·2단계)/10~20주(3·4단계) | ("r", a, b)
  ("debuff", w)           대상 능력치 −20% 스택
  ("buff_all", pct, w)
  ("potential", 1)        대상 잠재력 +1단계 (S면 개인 버프 +1블록으로 대체)
  ("enh", -1)             대상 강화 −1
  ("resign",)             대상 퇴사
  ("item_gain",)          랜덤 아이템 1개
  ("thief", money_pct)    보유 아이템 1개 잃음, 없으면 돈 −money_pct% (가이드: R_ref 기준 / 코드: 보유금 기준)
  ("sales_bonus", v)      이번 게임 매출 보너스 ±v%
  ("aim_lock", aim, n)    다음 n번 팀장 점수 4회차 조준 고정
  ("deferred", weeks, [effects])  weeks주 뒤 적용
  ("chance", p, [effects], [else_effects])
  ("investment", spec)    투자 제안 수락
  ("req_sat_ge", v)       (선택지 맨 앞) 대상 만족도 v 이상일 때만 고를 수 있음

target: "emp" 랜덤 직원 / "emp_role:D" 해당 직군 직원 / "emp_role:DA" / "pair" 직군이 다른 두 명 /
        "random_part" / "none" / "villain" / "basics"(초심 회복 훈련 자동 대상) / "lowest_sat"
q: 등장 가능한 개발 진행도 구간 (1=0~25%, 2=25~50%, 3=50~75%, 4=75~100%)
"""

# 단계별 이벤트 버프 기간: 코드 = RandomStatBuffWeeksByStage (1·2단계 5~15주, 3·4단계 10~20주)
N = ("n",)
L1 = ("L", 1.0)
L_HALF = ("L", 0.5)

SCHEDULE = {
    # 진행도 4구간별 이벤트 1개 배치 확률, 작품당 최대 개수
    "code": dict(chances={s: (0.5, 0.7, 0.7, 0.5) for s in (1, 2, 3, 4)}, limit={s: 2 for s in (1, 2, 3, 4)},
                 no_repeat=True),   # [포팅: RandomEventManager.ScheduleEvents] 단계 무관, 최대 2개, 같은 이벤트 중복 없음
    "guide": dict(chances={1: (.5, .7, .7, .5), 2: (.5, .5, .5, .5), 3: (.7, .5, .5, .7), 4: (.7, .5, .5, .7)},
                  limit={1: 2, 2: 3, 3: 4, 4: 4}, no_repeat=True),  # [가이드 §1-1]
}

# ── 현재 게임 코드 ────────────────────────────────────────────────────────────
EVENTS_CODE = [
    # 선택지 없음 [RandomEvent_Chart.csv + RandomEvents_Dev.cs]
    dict(id="CompetitorGame", w=0.5, q=(4, 4), target="none", options=[[("sat_all", -5)]]),
    dict(id="AvoidingEmployee", w=1, q=(1, 4), target="emp", options=[[("sat", -10)]]),
    dict(id="Cold", w=1, q=(1, 4), target="emp", options=[[("debuff", ("r", 4, 8))]]),
    dict(id="BadReview", w=1, q=(1, 4), target="emp", options=[[("sat", -10)]]),
    dict(id="NetworkIssue", w=1.3, q=(1, 3), target="none", options=[[("delay", 1)]]),
    dict(id="DrillEvent", w=1, q=(1, 4), target="emp", options=[[("debuff", ("r", 4, 8))]]),
    dict(id="ThiefEvent", w=1, q=(1, 4), target="none", options=[[("thief", "gold5")]]),
    # 선택지 [RandomEventChoice_Chart.csv + RandomEvents_Choice.cs]
    dict(id="Birthday", w=1, q=(1, 4), target="emp", options=[
        [("sat", -5)],
        [("sat", 10), ("money_emp_salary", 3)]]),     # 실제 차감 = 그 직원 연봉 3% (문구는 총 연봉 3%로 표시 — 불일치)
    dict(id="EarlyLeaveRequest", w=1, q=(1, 4), target="emp", options=[
        [("sat", -5)],
        [("sat", 5), ("delay", 1)]]),
    dict(id="HackyCode", w=1.3, q=(2, 3), target="emp_role:D", options=[
        [("deferred", 2, [("leader_pct", -10)])],    # 100% 확률, 2주 뒤 개발 팀장 점수 −10%
        [("delay", 1)]]),
    dict(id="EquipmentUpgrade", w=1, q=(1, 4), target="emp_role:DA", options=[
        [("money_salary", 3), ("buff", 10, N)],
        []]),
    dict(id="CompanyDinner", w=1, q=(1, 4), target="none", options=[
        [("sat_all", -5)],
        [("money_salary", 5), ("chance", 0.5, [("sat_all", 5)], [])],
        [("money_salary", 10), ("sat_all", 10)]]),
    dict(id="BossGossip", w=1, q=(1, 4), target="emp", options=[
        [("sat_all", 5)],
        [("sat", -5), ("buff", 10, N)]]),
    dict(id="YoutuberRequest", w=1, q=(3, 4), target="none", options=[
        [("chance", 1 / 3, [("sales_bonus", 5)], [("chance", 0.5, [], [("sales_bonus", -5)])])],  # 장르 인기도 3+/2/1 [근사: 인기도 미구현 → 각 1/3]
        []]),
    dict(id="EmployeeFight", w=4, q=(3, 4), target="pair", options=[   # 탕수육/민초/에어컨 3종 중 하나
        [("leader_pct", 10), ("sat", 10), ("leader2_pct", -10), ("sat2", -10)],
        [("leader2_pct", 10), ("sat2", 10), ("leader_pct", -10), ("sat", -10)]]),
]
# 투자 제안: 풀이 아니라 개발 시작 시 50% 확률로 따로 발생 [RandomEventManager.investmentTriggerChance]
INVESTMENT_CODE = dict(trigger=0.5)
CODE_COND = dict(  # 조건 이벤트 [RandomEvents_Condition*.cs]
    coffee=dict(spill=0.3, buff=(10, N), sat=15, refuse=-10),        # 커피 요청: 개발 중 30% 쏟음(지연), 아니면 버프+만족도
    energy=dict(spill=0.0, buff=(10, N), sat=25, refuse=-10),
    anxiety=[("sat", -5), ("buff", -10, N)],
    romance=dict(sat=10, buff=(10, N)), breakup=-20, couple_resign=1.0,
    burnout=dict(p=0.5, min_stage=1, effect=[("debuff", N)]),
    jealousy=dict(p=0.7, min_stage=1, effect=[("sat", -15)]),
)

# ── 가이드 설계안 (§5) ────────────────────────────────────────────────────────
S = lambda *v: tuple(v)  # 단계별 값 (1,2,3,4단계)
EVENTS_GUIDE = [
    # 5-1 선택지 — 기존
    dict(id="Birthday", w=1, q=(1, 4), target="emp", options=[
        [("sat", -10)], [("sat", 15), ("money_r", S(5, 4.5, 4, 5))]]),
    dict(id="LeaveRequest", w=1, q=(1, 4), target="emp", options=[
        [("sat", -10), ("part_pct", S(-1, -4, -6, -5))], [("sat", 15), ("delay", 1)]]),
    dict(id="HackyCode", w=1.3, q=(2, 3), target="emp_role:D", options=[
        [("deferred", 2, [("chance", 0.5, [("part_pct", S(-15, -25, -25, -25))], [])])], [("delay", 1)]]),
    dict(id="Fight", w=2.5, q=(4, 4), target="pair", options=[
        [("part_pct", 9), ("sat", 10), ("part2_pct", -9), ("sat2", -10)],
        [("part2_pct", 9), ("sat2", 10), ("part_pct", -9), ("sat", -10)]]),
    dict(id="Equipment", w=1, q=(1, 4), target="emp_role:DA", options=[
        [("buff", 20, L1), ("money_r", 4.5)], []]),
    dict(id="TeamDinner", w=1, q=(1, 4), target="none", options=[
        [("sat_all", -5)],
        [("chance", 0.5, [("sat_all", 5)], []), ("money_r", S(6, 5.5, 7, 10))],
        [("sat_all", 10), ("money_r", S(10, 11, 14.5, 20.5))]]),
    dict(id="BossGossip", w=1, q=(1, 4), target="emp", options=[
        [("sat_all", 5)], [("sat", -5), ("buff", 20, L1)]]),
    dict(id="Youtuber", w=2, q=(3, 4), target="none", options=[
        [("chance", 1 / 3, [("sales_bonus", 5)], [("chance", 0.5, [], [("sales_bonus", -5)])])], []]),
    dict(id="Investment", w=1, q=(1, 4), target="none", options=[[("investment", "guide")], []]),
    # 5-2 선택지 — 신규
    dict(id="Package", w=1, q=(1, 4), target="none", options=[
        [("chance", 0.5, [("item_gain",), ("gain_r", S(3, 3, 4, 6))], [("sat_all", -5)])], []]),
    dict(id="UsedChair", w=1, q=(1, 4), target="emp", options=[
        [("money_r", 2.5), ("chance", 0.7, [("buff", 20, L1)], [("buff", -20, L_HALF)])], []]),
    dict(id="Cat", w=1, q=(1, 4), target="emp", options=[
        [("chance", 0.5, [("sat_all", 5)], [("part_pct", S(-7, -6, -8, -12))])], [("sat", -5)]]),
    dict(id="USB", w=1, q=(1, 4), target="emp", options=[
        [("chance", 0.5, [("potential", 1)], [("buff", 20, L1)])], []]),
    dict(id="Visitor", w=1, q=(1, 1), target="none", options=[
        [], [("chance", 0.5, [("thief", "r5")], [("gain_r", 7)])]]),
    dict(id="Lotto", w=0.5, q=(1, 4), target="emp", options=[
        [("sat", 10), ("chance", 0.05, [("resign",)], [])], [("sat", -10)]]),
    dict(id="Scout", w=1, q=(1, 4), target="emp", options=[
        [("req_sat_ge", 60), ("sat", -10)],
        [("money_r", S(21, 8, 5, 6)), ("sat", 10)],
        [("resign",)]]),
    dict(id="Outsource", w=1, q=(1, 4), target="random_part", options=[
        [("part_pct", 9), ("money_r", 4.5)], []]),
    dict(id="Contest", w=1, q=(1, 4), target="best", options=[
        [("potential", 1)], [("sat_all", S(5, 5, 10, 10))]]),
    dict(id="Pressure", w=1, q=(1, 4), target="emp", options=[
        [("buff", 20, L1), ("sat", -15)], [("sat", 10)]]),
    dict(id="Perfectionist", w=1, q=(1, 4), target="random_part", options=[
        [("sat_all", -5), ("part_pct", S(8, 7, 9, 13))], []]),
    dict(id="Villain", w=1, q=(1, 4), target="villain", stage_min=2, options=[
        [("resign",)], [("sat_all", -5)]]),
    dict(id="GutRush", w=1, q=(1, 4), target="emp", options=[
        [("aim_lock", "high", 3), ("buff", 20, L_HALF)], []]),
    dict(id="SafetyFirst", w=1, q=(1, 4), target="emp", options=[
        [], [("aim_lock", "low", 3), ("sat", 10)]]),
    dict(id="BackToBasics", w=1, q=(1, 4), target="basics", options=[
        [("potential", 1), ("enh", -1)], []]),
    # 5-4 선택지 없음
    dict(id="NetworkDown", w=1.0, q=(1, 3), target="none", options=[[("delay", 1)]]),
    dict(id="AvoidingEmployee", w=1, q=(1, 4), target="emp", options=[[("sat", -25)]]),
    dict(id="BadReview", w=1, q=(1, 4), target="emp", options=[[("sat", -25)]]),
    dict(id="Cold", w=1, q=(1, 4), target="emp", options=[[("debuff", L1)]]),
    dict(id="Drill", w=1, q=(1, 4), target="emp", options=[[("debuff", L1)]]),
    dict(id="Competitor", w=0.5, q=(4, 4), target="none", options=[[("sat_all", -5)]]),
    dict(id="Thief", w=1, q=(1, 4), target="none", options=[[("thief", "r3")]]),
    dict(id="NewYearFortune", w=1, q=(1, 4), target="none", month=1, options=[
        [("chance", 0.5, [("sat_all", 5)], [("sat_all", -5)])]]),
    dict(id="LuckyThings", w=3, q=(1, 4), target="emp", options=[[("sat", 25)]]),   # 명당 / 의자 득템 / 최애 게임
    dict(id="BestCondition", w=1, q=(1, 4), target="emp", options=[[("buff", 20, L1)]]),
    dict(id="Landlord", w=1, q=(1, 4), target="none", options=[[("money_r", 5)]]),
    dict(id="PartFlaw", w=3, q=(1, 4), target="random_part", options=[[("part_pct", -9)]]),     # 기획 결함 / 키보드 샷건 / 태블릿 고장
    dict(id="PartBurst", w=3, q=(1, 4), target="random_part_led", options=[[("part_pct", 9)]]),  # 아이디어 폭발 / 기적의 오타 / 금손 모드 (그 파트 팀장 이후)
]
GUIDE_COND = dict(
    coffee=dict(spill=0.3, buff=(20, L_HALF), sat=0, refuse=-10),   # ㉕ 70% 개인 버프 +½블록 / 30% 지연
    energy=dict(spill=0.0, buff=(20, L_HALF), sat=0, refuse=-10),   # ㉖
    anxiety=[("sat", -10), ("debuff", L_HALF)],
    romance=dict(sat=10, buff=(20, L_HALF)), breakup=-20, couple_resign=0.5,
    burnout=dict(p=0.5, min_stage=3, effect=[("debuff", L1)]),
    jealousy=dict(p=0.7, min_stage=2, effect=[("sat", -25)]),
)

EVENT_SPECS = {"code": (EVENTS_CODE, CODE_COND), "guide": (EVENTS_GUIDE, GUIDE_COND)}
