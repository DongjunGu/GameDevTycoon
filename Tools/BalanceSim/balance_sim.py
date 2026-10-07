# -*- coding: utf-8 -*-
"""
GameDevTycoon 런(회차) 밸런싱 시뮬레이터 — v2

설계 문서: 같은 폴더의 시뮬레이터_설계.md / 이벤트 데이터: sim_events.py

원칙
  - 게임 프로젝트(Assets/)는 절대 수정하지 않는다. 공식은 여기로 "옮겨" 쓰고, 출처를 주석으로 남긴다.
  - [포팅] = 코드에서 그대로 / [문서] = 바탕화면 기획 문서 / [근사] = 단순화 / [임시] = 실험으로 정할 값 / TODO = 미구현
  - 표준 라이브러리만 사용 (이 PC의 anaconda numpy가 MKL 오류로 죽는 문제 회피)

사용법 (이 폴더에서)
  python balance_sim.py trace     --stage 3 [--events]  # 주 단위 만족도 흐름
  python balance_sim.py validate  --runs 100 [--spec code]  # 가정 vs 측정 (단계별)
  python balance_sim.py enhroi                          # 강화 손익: 연봉 상승 vs 매출 증가 (단계·성수별)
  python balance_sim.py satvalue  --runs 150            # 단계별 만족도 가치 (측정 분포 + A/B)
  python balance_sim.py outgame   --runs 60             # 아웃게임 요소별 효과 (단계별)
  python balance_sim.py items     --runs 60             # 아이템 실측 효용 + 구매 전략 비교
  python balance_sim.py reach     --runs 100            # 스펙 × 정책별 단계 도달률·파산·플레이 시간
  python balance_sim.py luck / inject / calibrate
"""
import argparse
import hashlib
import math
import statistics
import struct
from dataclasses import dataclass, field
from typing import Callable, Dict, List, Optional, Tuple

import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))  # 같은 폴더의 sim_events.py
from sim_events import EVENT_SPECS, SCHEDULE, INVESTMENT_CODE  # noqa: E402

# ════════════════════════════════════════════════════════════════════════════
# 0. 시간
# ════════════════════════════════════════════════════════════════════════════
WEEKS_PER_YEAR = 48
RENT_WEEK = 24            # 7월 1주차 [포팅: GametimeManager.cs:524]
MERCHANT_MONTHS = (5, 7)  # [포팅: MerchantManager]
DEV_SECONDS = 80.0        # 개발은 규모 무관 80초 [포팅: GametimeManager.SetProjectSpeed]
IDLE_SEC_PER_WEEK = 6.0
UI_SEC_PER_PROJECT = 30.0  # [임시] 팀장 점수·이벤트·결과창 등 시간 정지 화면


def month_of(woy: int) -> int:
    return woy // 4 + 1


def week_of_month(woy: int) -> int:
    return woy % 4 + 1


# ════════════════════════════════════════════════════════════════════════════
# 1. 설계 파라미터
# ════════════════════════════════════════════════════════════════════════════
@dataclass
class RentConfig:
    """사무실비 = 단계 기준가 × (1 + 단계 상승률) ^ (지금 단계에서 지난 해 수)  [임시 — calibrate로 보정]"""
    base: Tuple[float, float, float, float] = (1_000, 20_000, 150_000, 600_000)
    growth: Tuple[float, float, float, float] = (0.15, 0.15, 0.15, 0.15)

    def rent(self, stage: int, years_in_stage: int) -> float:
        return self.base[stage - 1] * (1 + self.growth[stage - 1]) ** years_in_stage


@dataclass
class SimConfig:
    rent: RentConfig = field(default_factory=RentConfig)
    max_years: int = 40
    event_spec: str = "guide"      # "guide" = 이벤트 가이드 설계안 / "code" = 지금 게임 코드
    events_on: bool = True
    event_scale: float = 1.0       # 만족도·파트·버프 효과 배율 (영향 크기 실험용)
    # 사무실 이전 — 게임 코드에 아직 없음. 기획: 추후 미션 연동, 초반은 "점수 넘으면 이동".
    # 시점: 출시 직후 '다음 게임을 시작하기 직전'에 이전 → 인원 충원 → 큰 규모로 개발 시작
    move_rule: str = "score"       # "score" / "money" / "locked"
    move_score: Tuple[float, float, float] = (60, 75, 85)  # [임시] 1→2, 2→3, 3→4 평론가 점수
    move_cost_mult: float = 0.0
    wait_hires_after_move: bool = True  # 이전 직후 면접(3주)이 끝나 인원을 채운 뒤 개발 시작
    item_spec: str = "v2"
    salary_negotiation: bool = True
    # 테크트리 미구현 → 단계별 진행 가정 [임시]
    cell_score: Tuple[int, int, int, int] = (5, 5, 10, 15)
    grid_cells: Tuple[int, int, int, int] = (16, 16, 28, 36)
    tech_awaken: Tuple[bool, bool, bool, bool] = (False, False, True, True)


# ════════════════════════════════════════════════════════════════════════════
# 2. 게임 데이터
# ════════════════════════════════════════════════════════════════════════════
STAGE = {  # [포팅: 경제구조 §1, §6-1, §6-6 / 정원은 GameScene.unity maxEmployeePerStage 2,4,6,7 (CEO 제외)]
    1: dict(scale="small", max_emp=2, dev_weeks=16, sale=[.50, .30, .20], mult=1.0, dev_cost=1_000),
    2: dict(scale="mid", max_emp=4, dev_weeks=24, sale=[.31, .26, .15, .12, .09, .07], mult=2.9, dev_cost=15_000),
    3: dict(scale="large", max_emp=6, dev_weeks=32, sale=[.24, .18, .15, .13, .11, .08, .07, .04], mult=8.3, dev_cost=100_000),
    4: dict(scale="large", max_emp=7, dev_weeks=32, sale=[.24, .18, .15, .13, .11, .08, .07, .04], mult=8.3, dev_cost=100_000),
}
LARGE_7P_MULT = 13.8
DELAY_UNIT = {"small": 1, "mid": 2, "large": 3}


def scale_mult(stage: int, n_emp: int) -> float:
    return LARGE_7P_MULT if STAGE[stage]["scale"] == "large" and n_emp >= 7 else STAGE[stage]["mult"]


# 강화표 [포팅: 경제구조 §6-3]  n: (성공%, 유지%, 하락%, 강화비용, 주스탯 증가(n→n+1), 연봉 증가(n+1성 도달))
ENHANCE = [
    (80, 20, 0, 200, 20, 100), (75, 25, 0, 200, 20, 200), (70, 30, 0, 400, 25, 300),
    (65, 35, 0, 400, 25, 400), (60, 40, 0, 600, 30, 500), (60, 40, 0, 600, 30, 600),
    (55, 45, 0, 800, 35, 700), (55, 45, 0, 1_000, 35, 800), (50, 50, 0, 1_300, 40, 900),
    (50, 50, 0, 1_600, 40, 1_000), (50, 50, 0, 2_000, 40, 1_500), (45, 53, 2, 3_400, 50, 3_000),
    (45, 52.4, 2.6, 3_800, 50, 4_500), (40, 56.8, 3.2, 4_300, 55, 6_300), (40, 56.2, 3.8, 5_000, 55, 10_000),
    (40, 55.6, 4.4, 5_800, 55, 13_000), (35, 60, 5, 11_000, 65, 25_000), (35, 59.4, 5.6, 12_000, 65, 30_000),
    (30, 63.6, 6.4, 15_000, 70, 35_000), (30, 63, 7, 18_000, 70, 100_000), (30, 62.4, 7.6, 21_000, 70, 130_000),
    (20, 71.8, 8.2, 32_000, 90, 160_000), (20, 71.2, 8.8, 39_000, 90, 200_000), (10, 80.6, 9.4, 72_000, 120, 250_000),
    (5, 85, 10, 94_000, 160, 300_000),
]
MAX_ENH = 25
POT_ORDER = ["C", "B", "A", "S"]
POT_ENH_BONUS = {"C": 0, "B": 2, "A": 4, "S": 7}

# 팀장 점수 [포팅: DevelopmentManager.cs:1160~1900, 2363]
LEADER_STAGE_M = (1.35, 1.5, 1.65)
LEADER_DS_MAX_ROLL = (4, 6, 6, 14)
LEADER_DS_BASE = (19, 19, 19, 13)
LEADER_ROUND_MULT = (0.6, 0.9, 1.0, 1.5)
LEADER_C0 = 0.176
LEADER_BURST_CUT = 0.05
LEADER_POT_P = {"C": 0.962, "B": 1.0, "A": 1.038, "S": 1.068}
AIM_U_RANGE = {"low": (1, 6), "mid": (5, 10), "high": (9, 14)}
LEADER_BONUS_THRESH = (90, 95, 99)
LEADER_BONUS_F = (
    ((1.99, 2.985), (5.469, 8.204), (22.143, 33.215)),
    ((1.634, 2.452), (4.43, 6.645), (16.954, 25.431)),
    ((1.173, 1.759), (3.111, 4.666), (10.86, 16.291)),
)

# 개발 틱 [포팅: DevelopmentManager.BuildTickOrder:573, AccumulateByType:2527]
TICKS_PER_EMP = 12
TICK_JACKPOT_P, TICK_SUCCESS_P, TICK_BUG_P = 0.11, 0.387, 0.25
# 창의성 [포팅: CreativityGameData.DrawRandomBlock, CreativityGameUI]
BLOCK_CELL_DIST = ((2, 0.3), (3, 0.4), (4, 0.3))
PERFECT_BONUS = 0.10

PLATFORM = {  # [포팅: SCRIPTS.md §3]
    "Mobile":   lambda p, d, a, c: 1.5 * p + d + a + 1.5 * c,
    "PC":       lambda p, d, a, c: p + 1.5 * d + a + 1.5 * c,
    "Nintendo": lambda p, d, a, c: p + d + 1.5 * a + 1.5 * c,
}
PLATFORM_W = {"Mobile": (1.5, 1, 1, 1.5), "PC": (1, 1.5, 1, 1.5), "Nintendo": (1, 1, 1.5, 1.5)}

# 채용 [포팅: 경제구조 §6-4 / 문서: 아웃 게임 로직.xlsx]
HIRE_POT = {
    "Normal":    {1: (80, 18, 2, 0), 2: (60, 35, 5, 0), 3: (40, 49, 10, 1)},
    "Rare":      {1: (70, 25, 5, 0), 2: (50, 40, 9, 1), 3: (30, 52, 15, 3)},
    "Epic":      {1: (60, 30, 10, 0), 2: (40, 40, 17, 3), 3: (20, 55, 20, 5)},
    "Unique":    {1: (50, 35, 15, 0), 2: (30, 40, 25, 5), 3: (10, 50, 30, 10)},
    "Legendary": {1: (40, 40, 20, 0), 2: (20, 40, 30, 10), 3: (0, 40, 40, 20)},
}
HIRE_GRADE_MULT = {"Normal": 0.6, "Rare": 0.7, "Epic": 0.8, "Unique": 0.9, "Legendary": 1.0}
HIRE_DISCOUNT = {1: (10, 20, 20, 20, 30), 2: (10, 15, 20, 20, 35), 3: (10, 15, 25, 0, 50)}
HIRE_TIER = {1: 1, 2: 2, 3: 3, 4: 3}         # [근사] 사무실 단계 → 채용 단계 (실제는 테크트리 '채용 2/3단계')
HIRE_AD_COST = {1: 0, 2: 10_000, 3: 100_000}  # [이벤트 가이드 §3-6] 1단계는 [임시] 0
HIRE_CANDIDATES = {1: 3, 2: 4, 3: 5}          # [이벤트 가이드 §3-6] 1단계는 [임시] 3명
HIRE_ENH = {1: 0, 2: 5, 3: 10}                # TODO 채용 강화 레벨 [임시]
HIRE_WAIT_WEEKS = 3
HIRE_MAIN = (20, 50)
HIRE_CREA = (10, 20)

SAT_START = 80
SAT_MONTHLY_DROP = 5
SAT_COMPLETE = 40

# 아웃게임 특성 [문서: 바탕화면 '아웃 게임 로직.xlsx' > 특성 뽑기. 문서에 수치가 없으면 Trait_Chart.csv 값]
# id: (이름, 등급, 효과, 값)
TRAITS = {
    # S
    "enh_s": ("강화비 할인(S)", "S", "enhanceCostDiscount", 15),   # ⚠ 문서는 5%(B와 동일) — CSV s2 15%로 둠, 확인 필요
    "high_enh_s": ("15성+ 성공률", "S", "highEnhanceSuccess", 5),
    "yearly_all_s": ("연 1회 5% 전원 95", "S", "yearlyAllRecover", 5),
    "sat_drop_s": ("월 하락 −1", "S", "monthlySatDropReduce", 1),
    "no_drop_1_10": ("1~10성 하락X", "S", "none", 0),             # 1~10성은 원래 하락 확률 0% → 효과 없음 (문서 확인 필요)
    "large_sale_s": ("대작 매출 +5%", "S", "largeScaleSaleBonus", 5),
    "perfect_sale_s": ("평론가 100점 +5%", "S", "perfectScoreSaleBonus", 5),
    # A
    "marketing_god": ("마케팅의 신", "A", "marketingFree", 1),
    "enh_a": ("강화비 −10%", "A", "enhanceCostDiscount", 10),
    "recruiter": ("호객행위", "A", "recruitApplicants", 1),
    "yearly_one_a": ("연 1회 10% 1명 풀회복", "A", "yearlySatRecover", 10),
    "research_a": ("연구 −10%", "A", "none", 0),                  # 테크트리 미구현
    "highsat_a": ("만족도 80+ 스탯 +10%", "A", "highSatStatBonus", 10),
    "mid_sale_a": ("중형 매출 +5%", "A", "mediumScaleSaleBonus", 5),
    "bug_a": ("버그 2개 자동 수정", "A", "none", 0),               # 버그 미구현
    # B
    "gold_spoon": ("금수저", "B", "startGold", 6000),
    "enh_b": ("강화비 −5%", "B", "enhanceCostDiscount", 5),
    "research_b": ("연구 −5%", "B", "none", 0),
    "highsat_b": ("만족도 80+ 스탯 +5%", "B", "highSatStatBonus", 5),
    "negotiation_b": ("연봉협상 동결 무패널티", "B", "negotiationSafe", 1),  # 문서에만 있음 (CSV 없음)
    "block_b": ("창의성 블록 +1", "B", "extraBlock", 1),          # 문서: 게임마다 +1 / CSV b5: 시작 시 블록 아이템 1개
    "haggler_plus": ("중고 거래+", "B", "itemDiscount", 10),
    "small_sale_b": ("소형 매출 +5%", "B", "smallScaleSaleBonus", 5),
    # C
    "silver_spoon": ("은수저", "C", "startGold", 3000),
    "devcost_c": ("개발금 −10%", "C", "devCostDiscount", 10),
    "resign_c": ("퇴사 확률 −10%", "C", "resignChanceReduce", 10),
    "caffeine": ("카페인 중독", "C", "startItem_coffee", 1),
    "haggler": ("중고 거래", "C", "itemDiscount", 5),
    "fatigue_c": ("피로도 −1", "C", "none", 0),                    # 피로도 미구현
    "beginner_luck": ("초심자의 행운", "C", "firstSaleBonus", 10),
    "poor_rescue": ("가난한 회사", "C", "brokeRescue", 1),
}

# 아이템 v2 [노션 '아이템 밸런싱 가이드']
ITEM_RATE = {"하": 0.025, "중": 0.04, "상": 0.05, "최상": 0.075}
ITEM_TARGET_V = {"하": 5, "중": 7.5, "상": 10, "최상": 15}
ITEMS = {
    "enhanceProtect": ("하", (2,)), "enhanceMid": ("중", (2,)), "enhanceMidPlus": ("상", (3,)),
    "enhanceHigh": ("최상", (4,)), "resetSpirit": ("중", (2, 3, 4)), "upgradeRandom": ("중", (2, 3, 4)),
    "upgradeDevelop": ("상", (3, 4)), "upgradeArt": ("상", (3, 4)), "upgradePlan": ("상", (3, 4)),
    "coffee": ("하", (1, 2, 3, 4)), "mysteryPotion": ("중", (1, 2, 3, 4)), "energyDrink": ("최상", (1, 2, 3, 4)),
    "relax": ("하", (1, 2, 3, 4)), "awaken": ("중", (1, 2, 3, 4)), "techNote": ("최상", (3, 4)),
    "blockRandom": ("하", (3, 4)), "blockLegendary": ("중", (3, 4)), "hypnotizer": ("최상", (2, 3, 4)),
}
ENH_ITEM_CAP = {"enhanceMid": 14, "enhanceMidPlus": 21, "enhanceHigh": 23}
UPGRADE_PART = {"upgradeDevelop": "D", "upgradeArt": "A", "upgradePlan": "P"}
SAT_ITEMS = ("coffee", "mysteryPotion", "energyDrink")

# 가이드 값 (검증 비교용)
GUIDE_RREF = {1: 9_225, 2: 156_478, 3: 1_379_000, 4: 5_025_000}
GUIDE_REP = {1: (2, 7), 2: (4, 13), 3: (6, 18), 4: (7, 22)}
GUIDE_BANDS = {1: (90, 10, 0), 2: (75, 20, 5), 3: (65, 25, 10), 4: (60, 30, 10)}
GUIDE_PRE_COMPLETE_SAT = {1: 80, 2: 70, 3: 60, 4: 60}
# 코드: 구간 확률 50/70/70/50%, 최대 2개 → 기대값 E[min(X,2)] = 1.8
GUIDE_EVENTS_PER_PROJECT = {"guide": {1: 1.8, 2: 1.9, 3: 2.4, 4: 2.4}, "code": {s: 1.8 for s in (1, 2, 3, 4)}}
GUIDE_LEADER_SHARE = {1: 52, 2: 45, 3: 35, 4: 32}
GUIDE_SAT5_BAND = {"safe": (0.58, 0.29, 0.15, 0.12), "caution": (4.98, 1.98, 1.21, 1.40),
                   "danger": (13.2, 6.3, 3.9, 4.7)}   # 이벤트 가이드 §3-4 ②
GUIDE_SAT5_AVG = (1.02, 0.93, 0.79, 0.96)
GUIDE_Q = (26, 10, 6.3, 7.6)
ROLES = ("P", "D", "A")

# ════════════════════════════════════════════════════════════════════════════
# 3. 난수 — 키 기반 스트림
# ════════════════════════════════════════════════════════════════════════════
STREAMS = ("event", "leader", "tick", "enhance", "sales", "hire", "merchant", "satisfaction", "salary", "misc")


class Rng:
    def __init__(self, seed: int, overrides: Optional[Dict[str, int]] = None):
        self.seed = seed
        self.overrides = overrides or {}

    def u(self, stream: str, *key) -> float:
        s = self.overrides.get(stream, self.seed)
        h = hashlib.blake2b(repr((s, stream) + key).encode(), digest_size=8).digest()
        return (struct.unpack("<Q", h)[0] >> 11) / float(1 << 53)

    def randint(self, stream: str, lo: int, hi: int, *key) -> int:
        return lo + int(self.u(stream, *key) * (hi - lo + 1))

    def uniform(self, stream: str, lo: float, hi: float, *key) -> float:
        return lo + (hi - lo) * self.u(stream, *key)

    def weighted(self, stream: str, items, weights, *key):
        r = self.u(stream, *key) * sum(weights)
        for it, w in zip(items, weights):
            r -= w
            if r <= 0:
                return it
        return items[-1]

    def pick(self, stream: str, items, *key):
        return items[int(self.u(stream, *key) * len(items))]


# ════════════════════════════════════════════════════════════════════════════
# 4. 아웃게임 스펙 [문서: 아웃 게임 로직.xlsx]
#    특성(슬롯 2→5) + 카드 등급 + 기억의 조각(CEO 파트별 레벨 1~40)
# ════════════════════════════════════════════════════════════════════════════
@dataclass
class OutgameProfile:
    name: str
    traits: Tuple[str, ...] = ()
    card_grade: str = "Normal"
    ceo_levels: Tuple[int, int, int] = (1, 1, 1)   # 기획/개발/아트 레벨
    regress: bool = False                          # 회귀 후 시작 자금 15,000 (신규 10,000)

    def t(self, effect: str) -> float:
        return sum(TRAITS[i][3] for i in self.traits if i in TRAITS and TRAITS[i][2] == effect)

    @property
    def start_money(self) -> float:
        return (15_000 if self.regress else 10_000) + self.t("startGold")

    def lv(self, part: str) -> int:
        return self.ceo_levels[ROLES.index(part)]

    def ceo_stat(self, part: str) -> int:
        """[문서] 기본 50 + 레벨당 10, 10레벨 달성 시 +100 (10레벨 = 240)"""
        L = self.lv(part)
        return 50 + 10 * (L - 1) + (100 if L >= 10 else 0)


PROFILES = {
    # [임시] "몇 회차 후 이 스펙인가"(진행 속도) 정보가 아직 없어 조합만 단계적으로 잡음
    "P0_신규": OutgameProfile("P0_신규"),
    "P1_초급": OutgameProfile("P1_초급", ("silver_spoon", "haggler"), "Normal", (5, 5, 5), regress=True),
    "P2_중급": OutgameProfile("P2_중급", ("gold_spoon", "enh_b", "highsat_b"), "Rare", (10, 10, 10), regress=True),
    "P3_고스펙": OutgameProfile("P3_고스펙", ("gold_spoon", "enh_a", "highsat_a", "resign_c"), "Rare", (20, 20, 20),
                              regress=True),
    "P4_최고": OutgameProfile("P4_최고", ("enh_s", "sat_drop_s", "large_sale_s", "highsat_a", "marketing_god"),
                             "Legendary", (30, 30, 30), regress=True),
}

# ════════════════════════════════════════════════════════════════════════════
# 5. 상태
# ════════════════════════════════════════════════════════════════════════════


@dataclass
class Employee:
    eid: int
    role: str
    main: float
    crea: float
    pot: str = "C"
    grade: str = "Normal"
    enh: int = 0
    sat: float = SAT_START
    salary: float = 300
    buffs: List[List[float]] = field(default_factory=list)  # [남은 주, %]
    debuffs: List[int] = field(default_factory=list)        # 스택별 남은 주 (스택당 −20%)
    high_sat_bonus: float = 0.0
    is_ceo: bool = False
    consec_leader: int = 0
    consec_nonleader: int = 0

    def sat_mult(self) -> float:
        return 1.1 if self.sat > 80 else 1.0 if self.sat > 60 else 0.9 if self.sat > 40 else 0.8

    def base_main(self) -> float:
        return self.main * (1.10 if self.grade == "Legendary" and self.enh >= 20 else 1.0)

    def eff_main(self) -> float:
        pct = (self.sat_mult() - 1) * 100 + sum(b[1] for b in self.buffs) - 20 * len(self.debuffs)
        if self.sat >= 80:  # [문서] 만족도 80 이상 (코드 문구는 81 이상)
            pct += self.high_sat_bonus
        return max(0.0, self.base_main() * (1 + pct / 100))

    def eff_crea(self) -> float:
        return self.crea * self.sat_mult()

    def band(self) -> str:
        return "safe" if self.sat >= 56 else "caution" if self.sat >= 41 else "danger"


@dataclass
class Project:
    platform: str
    stage: int
    dev_weeks: int
    base_weeks: int
    week: int = 0
    parts: Dict[str, float] = field(default_factory=lambda: {"P": 0.0, "D": 0.0, "A": 0.0})
    leader_pts: Dict[str, float] = field(default_factory=lambda: {"P": 0.0, "D": 0.0, "A": 0.0})
    part_mult: Dict[str, float] = field(default_factory=lambda: {"P": 1.0, "D": 1.0, "A": 1.0, "C": 1.0})
    blocks: List[int] = field(default_factory=list)
    tick_plan: Dict[int, List[Tuple[int, str]]] = field(default_factory=dict)
    events: List[Tuple[int, dict]] = field(default_factory=list)    # (주차, 이벤트)
    n_events: int = 0
    leaders_done: set = field(default_factory=set)
    upgrades_used: set = field(default_factory=set)
    deferred: List[tuple] = field(default_factory=list)
    sales_bonus: float = 0.0
    investment: Optional[dict] = None


@dataclass
class RunState:
    cfg: SimConfig
    prof: OutgameProfile
    rng: Rng
    money: float
    year: int = 0
    woy: int = 0
    stage: int = 1
    years_in_stage: int = 0
    emps: List[Employee] = field(default_factory=list)
    project: Optional[Project] = None
    sales_queue: List[float] = field(default_factory=list)
    last_rev: float = 0.0
    last_rev_mult: float = 1.0
    last_parts: Dict[str, float] = field(default_factory=dict)
    releases: int = 0
    inventory: Dict[str, int] = field(default_factory=dict)
    merchant_week: int = -1
    sat_dropped_month: int = -1
    negotiation_week: int = -1
    hire_pending: int = 0              # 남은 면접 대기 주 (0 = 없음)
    waiting_after_move: bool = False
    move_ready: bool = False
    next_eid: int = 1
    bankrupt: Optional[str] = None
    freeze_until: int = -1
    play_sec: float = 0.0
    aim_lock: Optional[List] = None
    exits_by_year: Dict[int, int] = field(default_factory=dict)
    rumor_year: int = -1
    couple: Optional[Tuple[int, int]] = None
    pending: List[tuple] = field(default_factory=list)   # (발동 주, 종류, 데이터) — 커피 요청·연애 결말 등
    poor_rescue_used: bool = False
    log: Dict[str, list] = field(default_factory=lambda: {
        "games": [], "stage_year": {1: 0}, "bands": [], "rref_year": [], "events": [], "items": [],
        "item_use": [], "resign": [], "income": [], "sat_drop": [], "negotiation": [], "aims": [], "cond": []})

    @property
    def abs_week(self) -> int:
        return self.year * WEEKS_PER_YEAR + self.woy

    def total_salary(self) -> float:
        return sum(e.salary for e in self.emps)

    def r_ref(self) -> float:
        """R_ref = 최근작 매출 × (현재 규모배율 ÷ 그 작품 규모배율), 출시작 없으면 총 연봉 × 1.5 [이벤트 가이드 §2]"""
        if self.last_rev <= 0:
            return max(1.0, self.total_salary() * 1.5)
        return self.last_rev * scale_mult(self.stage, len(self.emps)) / self.last_rev_mult

    def rent_now(self) -> float:
        return self.cfg.rent.rent(self.stage, self.years_in_stage)

    def reserve(self) -> float:
        return self.total_salary() + self.rent_now() + dev_cost(self)

    def ceo(self, part: str) -> Employee:
        """CEO — 개발 틱은 없고, 모든 파트의 팀장 후보 [포팅: DispatchPanelUI '팀장 선택 모드는 CEO도 후보']"""
        s = self.prof.ceo_stat(part)
        return Employee(0, part, main=s, crea=0, pot="B", sat=100, salary=0, is_ceo=True)


def dev_cost(st: RunState) -> float:
    return STAGE[st.stage]["dev_cost"] * (1 - st.prof.t("devCostDiscount") / 100)


# ════════════════════════════════════════════════════════════════════════════
# 6. 시스템
# ════════════════════════════════════════════════════════════════════════════
def tick_S(skill: float) -> float:
    """[포팅: DevelopmentManager.cs:2533] (CalcConstantDev:2506은 호출되지 않는 옛 공식)"""
    return 1.7266 + 0.054105 * max(0.0, skill) ** 0.9076


def tick_counts(crea: float, awaken: bool, jackpot_p: float = TICK_JACKPOT_P) -> Dict[str, int]:
    """[포팅: BuildTickOrder:573] 각성 미해금이면 잭팟 확률이 성공으로 합쳐짐.
    [문서] CEO 개발 Lv31: 잭팟 11% → 12%, 그만큼 꽝 감소"""
    if awaken:
        jp, sp = jackpot_p, TICK_SUCCESS_P
    else:
        jp, sp = 0.0, TICK_SUCCESS_P + TICK_JACKPOT_P
    cp = 0.10 + 0.15 * min(1.0, max(0.0, (crea - 17.5) / 707.5))
    n = TICKS_PER_EMP
    jackpot = max(1, round(n * jp)) if jp > 0 else 0
    success, creativity, bug = round(n * sp), round(n * cp), max(1, round(n * TICK_BUG_P))
    blank = n - jackpot - success - creativity - bug
    if blank < 0:
        creativity, blank = max(0, creativity + blank), 0
    return dict(jackpot=jackpot, success=success, creativity=creativity, bug=bug, blank=blank)


def leader_K(skill: float) -> float:
    return 0.8738 + 0.026409 * max(0.0, skill) ** 0.9081


def leader_ds(r: int, roll: int, M: float) -> float:
    return float(round(LEADER_DS_BASE[r] + roll * M))


def leader_round_score(r: int, K: float, P: float, ds: float) -> float:
    return LEADER_ROUND_MULT[r] * LEADER_C0 * K * P * ds ** 0.95


def leader_M(enh: int) -> float:
    lv = max(0, min(25, enh))
    return LEADER_STAGE_M[0 if lv <= 9 else 1 if lv <= 18 else 2]


def bonus_bucket(enh: int) -> int:
    return 0 if enh <= 10 else 1 if enh <= 20 else 2


@dataclass
class LeaderCtx:
    K: float
    P: float
    M: float
    enh: int
    cum_ds3: float
    round_scores: List[float]
    bonus_granted: List[bool]
    bonus_total: float
    bonus_mult: float = 1.0


def aim_expected_score(ctx: LeaderCtx, aim: str) -> float:
    lo, hi = AIM_U_RANGE[aim]
    burst_total = sum(s * (1 - LEADER_BURST_CUT) for s in ctx.round_scores) + ctx.bonus_total
    tot = 0.0
    for U in range(lo, hi + 1):
        ds4 = leader_ds(3, U, ctx.M)
        cum = ctx.cum_ds3 + ds4
        if cum > 100:
            tot += burst_total
            continue
        new_bonus = sum(ctx.K * sum(LEADER_BONUS_F[bonus_bucket(ctx.enh)][i]) / 2 * ctx.bonus_mult
                        for i, th in enumerate(LEADER_BONUS_THRESH) if not ctx.bonus_granted[i] and cum >= th)
        tot += sum(ctx.round_scores) + leader_round_score(3, ctx.K, ctx.P, ds4) + ctx.bonus_total + new_bonus
    return tot / (hi - lo + 1)


def run_leader_session(st: RunState, leader: Employee, part: str, policy) -> float:
    """팀장 점수 1세션 [포팅: BuildAndShowLeaderScore + SelectRound4Aim]
    + [문서] CEO 개발 Lv20: 90/95/99 보너스 ×1.5"""
    rng, key = st.rng, (st.releases, part)
    K, P, M = leader_K(leader.eff_main()), LEADER_POT_P[leader.pot], leader_M(leader.enh)
    bmult = 1.5 if st.prof.lv("D") >= 20 else 1.0
    granted, bonus_total, scores, cum = [False] * 3, 0.0, [], 0.0

    def grant(cum_ds):
        nonlocal bonus_total
        for i, th in enumerate(LEADER_BONUS_THRESH):
            if not granted[i] and cum_ds >= th:
                fmin, fmax = LEADER_BONUS_F[bonus_bucket(leader.enh)][i]
                bonus_total += K * round(rng.uniform("leader", fmin, fmax, *key, "bonus", i), 2) * bmult
                granted[i] = True

    for r in range(3):
        ds = leader_ds(r, rng.randint("leader", 1, LEADER_DS_MAX_ROLL[r], *key, "roll", r), M)
        cum += ds
        if cum > 100:
            return sum(round(s * (1 - LEADER_BURST_CUT)) for s in scores) + bonus_total
        scores.append(round(leader_round_score(r, K, P, ds) * rng.uniform("leader", .97, 1.03, *key, "amp", r)))
        grant(cum)

    ctx = LeaderCtx(K, P, M, leader.enh, cum, list(scores), list(granted), bonus_total, bmult)
    if st.aim_lock and st.aim_lock[1] > 0:   # 이벤트 '직감적 돌진'/'안전제일'
        aim = st.aim_lock[0]
        st.aim_lock[1] -= 1
    else:
        aim = policy.choose_aim(st, ctx)
    st.log["aims"].append(aim)
    lo, hi = AIM_U_RANGE[aim]
    ds4 = leader_ds(3, rng.randint("leader", lo, hi, *key, "aim"), M)
    if cum + ds4 > 100:
        return sum(round(s * (1 - LEADER_BURST_CUT)) for s in scores) + bonus_total
    s4 = round(leader_round_score(3, K, P, ds4) * rng.uniform("leader", .97, 1.03, *key, "amp", 3))
    grant(cum + ds4)
    return sum(scores) + s4 + bonus_total


def pick_leader(st: RunState, part: str) -> Employee:
    """그 직군 직원 + CEO 중 능력치가 가장 높은 사람 (유저가 고르는 것 — 최고 능력치를 고른다고 가정)"""
    cands = [e for e in st.emps if e.role == part] + [st.ceo(part)]
    return max(cands, key=lambda e: e.eff_main())


def personal_buff_weeks(st: RunState) -> int:
    """개인 버프 1블록 기간 L = max(0.5, 0.18N) × T [이벤트 가이드 §4-1]"""
    return round(max(0.5, 0.18 * len(st.emps)) * STAGE[st.stage]["dev_weeks"])


def team_buff_weeks(st: RunState) -> int:
    return round((0.55 if len(st.emps) <= 2 else 0.35) * STAGE[st.stage]["dev_weeks"])


def critic_score(quality: float) -> float:
    """[포팅: 경제구조 §6-2] (랜덤 ±5 제외)"""
    return 53.46 * math.log(max(0.0, quality) + 248) - 302.9


# ── 개발 ────────────────────────────────────────────────────────────────────
def schedule_events(st: RunState, pr: Project):
    events, _ = EVENT_SPECS[st.cfg.event_spec]
    sch = SCHEDULE[st.cfg.event_spec]
    chances, limit = sch["chances"][st.stage], sch["limit"][st.stage]
    used = set()
    for q in range(1, 5):
        if len(pr.events) >= limit:
            break
        if st.rng.u("event", st.releases, "slot", q) >= chances[q - 1]:
            continue
        pool = [e for e in events if e["q"][0] <= q <= e["q"][1] and e["id"] not in used
                and st.stage >= e.get("stage_min", 1)]
        if not pool:
            continue
        ev = st.rng.weighted("event", pool, [e["w"] for e in pool], st.releases, "pick", q)
        used.add(ev["id"])
        qlen = pr.base_weeks / 4
        week = int((q - 1) * qlen + st.rng.u("event", st.releases, "when", q) * qlen)
        pr.events.append((week, ev))


def start_project(st: RunState, policy):
    s = STAGE[st.stage]
    st.money -= dev_cost(st)
    pr = Project(platform=policy.choose_platform(st), stage=st.stage, dev_weeks=s["dev_weeks"], base_weeks=s["dev_weeks"])
    awaken = st.cfg.tech_awaken[st.stage - 1]
    jp = 0.12 if st.prof.lv("D") >= 31 else TICK_JACKPOT_P
    for e in st.emps:
        counts = tick_counts(e.eff_crea(), awaken, jp)
        kinds = [k for k, n in counts.items() for _ in range(n)]
        order = sorted(range(len(kinds)), key=lambda i: st.rng.u("tick", st.releases, e.eid, "order", i))
        seg = pr.base_weeks / TICKS_PER_EMP
        for slot, idx in enumerate(order):
            w = int(slot * seg + st.rng.u("tick", st.releases, e.eid, "time", slot) * seg)
            pr.tick_plan.setdefault(w, []).append((e.eid, kinds[idx]))
    st.project = pr
    if st.cfg.events_on:
        schedule_events(st, pr)
        if st.cfg.event_spec == "code" and st.rng.u("event", st.releases, "invest") < INVESTMENT_CODE["trigger"]:
            if policy.accept_investment(st, "code"):
                part = st.rng.pick("event", ROLES, st.releases, "invpart")
                pr.investment = dict(spec="code", part=part, reward=max(100, round(st.total_salary() * 0.1)),
                                     threshold=st.last_parts.get(part, 0) + 5)


def fire_tick(st: RunState, e: Employee, kind: str, key):
    """[포팅: AccumulateByType] 잭팟 1.8×S×U(0.8~1.2) / 성공 S×U / 창의성 블록 / 버그·꽝"""
    pr = st.project
    S = tick_S(e.eff_main())
    if kind == "jackpot":
        pr.parts[e.role] += max(1, round(1.8 * S * st.rng.uniform("tick", 0.8, 1.2, *key)))
    elif kind == "success":
        pr.parts[e.role] += max(0, round(S * st.rng.uniform("tick", 0.8, 1.2, *key)))
    elif kind == "creativity":
        pr.blocks.append(st.rng.weighted("tick", [c for c, _ in BLOCK_CELL_DIST], [w for _, w in BLOCK_CELL_DIST], *key))
    # TODO 버그 틱 → 버그 수정 단계(4초/주) · 출시 감점


def progress_project(st: RunState, policy):
    pr = st.project
    if pr.week < pr.base_weeks:
        for eid, kind in pr.tick_plan.get(pr.week, []):
            e = next((x for x in st.emps if x.eid == eid), None)
            if e is not None:
                fire_tick(st, e, kind, (st.releases, eid, "tick", pr.week, kind))
    for part, at in (("P", 0.0), ("D", 0.25), ("A", 0.75)):
        if part not in pr.leaders_done and pr.week >= at * pr.dev_weeks:
            pr.leaders_done.add(part)
            leader = pick_leader(st, part)
            pts = run_leader_session(st, leader, part, policy)
            pr.parts[part] += pts
            pr.leader_pts[part] += pts
            leader_condition_events(st, leader, part)
    for week, ev in list(pr.events):
        if week == pr.week:
            trigger_event(st, ev, policy)
            pr.n_events += 1
    for item in [d for d in pr.deferred if d[0] == pr.week]:
        pr.deferred.remove(item)
        apply_effects(st, item[2], item[1], ("def", st.releases, pr.week))
    policy.use_items(st, "project_week")
    st.play_sec += DEV_SECONDS / pr.base_weeks
    pr.week += 1
    if pr.week >= pr.dev_weeks:
        finish_project(st, policy)


def creativity_score(st: RunState, pr: Project) -> float:
    """[포팅: CreativityGameUI] 놓인 칸 × 칸당 점수 (+판을 다 채우면 10%)
    [문서] CEO 아트 Lv20 퍼펙트 보너스 2배, Lv30 칸당 점수 +5% / 특성 '창의성 랜덤 블록 +1'
    [근사] 얻은 블록을 판 크기까지 다 놓는다고 가정."""
    for n in range(int(st.prof.t("extraBlock"))):
        pr.blocks.append(st.rng.weighted("tick", [2, 3, 4], [0.3, 0.4, 0.3], st.releases, "xblock", n))
    cap = st.cfg.grid_cells[st.stage - 1]
    cells = min(cap, sum(pr.blocks))
    per_cell = st.cfg.cell_score[st.stage - 1] * (1.05 if st.prof.lv("A") >= 30 else 1.0)
    bonus = PERFECT_BONUS * (2 if st.prof.lv("A") >= 20 else 1)
    score = cells * per_cell
    return score * (1 + bonus) if cells >= cap else score


def finish_project(st: RunState, policy):
    pr = st.project
    C = creativity_score(st, pr)
    lv40 = {r: (1.10 if st.prof.lv(r) >= 40 else 1.0) for r in ROLES}  # [문서] CEO Lv40: 그 파트 점수 +10%
    p, d, a = (pr.parts[k] * pr.part_mult[k] * lv40[k] for k in ROLES)
    c = C * pr.part_mult["C"]
    raw = PLATFORM[pr.platform](p, d, a, c)
    # TODO 버그 감점·쏠림 감점·인기도·피로도 (경제구조 §6-2) — 1.0
    mastery = 1.0 if st.releases < 5 else 1.03 if st.releases < 10 else 1.06 if st.releases < 20 else 1.10  # [근사]
    mkt_cost, M = policy.choose_marketing(st)
    st.money -= mkt_cost
    quality = raw * mastery * M
    critic = critic_score(quality)
    mult = scale_mult(st.stage, len(st.emps))
    scale = STAGE[st.stage]["scale"]
    bonus = (st.prof.t({"small": "smallScaleSaleBonus", "mid": "mediumScaleSaleBonus",
                        "large": "largeScaleSaleBonus"}[scale]) / 100
             + (st.prof.t("firstSaleBonus") / 100 if st.releases == 0 else 0)
             + (st.prof.t("perfectScoreSaleBonus") / 100 if critic >= 100 else 0)
             + pr.sales_bonus / 100)
    noise = st.rng.uniform("sales", 0.9, 1.1, st.releases)
    revenue = mult * 248 * (quality / 100) ** 2 * (1 + bonus) * noise  # [포팅: SalesUI.cs:315]
    for i, share in enumerate(STAGE[st.stage]["sale"]):
        while len(st.sales_queue) <= i:
            st.sales_queue.append(0.0)
        st.sales_queue[i] += revenue * share
    if pr.investment:   # 투자 제안 정산
        inv = pr.investment
        if inv["spec"] == "code":   # [포팅: CheckInvestmentResult] 지난 작품 같은 파트 +5 이상이면 성공
            ok = pr.parts[inv["part"]] >= inv["threshold"]
            st.money += inv["reward"] if ok else -round(inv["reward"] * 1.5)
        elif st.rng.u("event", st.releases, "invresult") >= 0.575:   # [가이드 ⑨] 성공률 55~60% [근사 57.5%]
            st.money -= 0.10 * st.r_ref()
    w = PLATFORM_W[pr.platform]
    st.log["games"].append(dict(
        year=st.year, stage=st.stage, revenue=revenue, quality=quality, critic=critic, raw=raw,
        n_emp=len(st.emps), max_enh=max([e.enh for e in st.emps], default=0),
        share=dict(P=w[0] * p / raw, D=w[1] * d / raw, A=w[2] * a / raw, C=w[3] * c / raw) if raw > 0 else {},
        leader_share={k: pr.leader_pts[k] / pr.parts[k] for k in ROLES if pr.parts[k] > 0},
        pre_sat=[e.sat for e in st.emps], n_events=pr.n_events, salary_ratio=st.total_salary() / max(1, revenue),
        cells=sum(pr.blocks), grid=st.cfg.grid_cells[st.stage - 1]))
    st.last_parts = dict(pr.parts)
    st.last_rev, st.last_rev_mult = revenue, mult
    st.releases += 1
    for e in st.emps:   # [포팅: OnProjectCompleted ← SalesUI.cs:173]
        e.sat = min(100, e.sat + SAT_COMPLETE)
    st.play_sec += UI_SEC_PER_PROJECT
    if st.cfg.move_rule == "score" and st.stage < 4 and critic >= st.cfg.move_score[st.stage - 1]:
        st.move_ready = True
    st.project = None
    policy.after_release(st)


# ── 강화 ────────────────────────────────────────────────────────────────────
def enhance_once(st: RunState, e: Employee, key) -> str:
    if e.enh >= MAX_ENH:
        return "max"
    succ, stay, drop, cost, _, _ = ENHANCE[e.enh]
    cost *= (1 - st.prof.t("enhanceCostDiscount") / 100)
    if e.enh >= 15:
        b = st.prof.t("highEnhanceSuccess")
        succ, stay = succ + b, stay - b
    if st.money < cost:
        return "nomoney"
    st.money -= cost
    r = st.rng.u("enhance", *key) * 100
    if r < succ:
        apply_enh_up(e)
        return "success"
    if r < succ + stay:
        return "stay"
    if st.inventory.get("enhanceProtect", 0) > 0 and 11 <= e.enh <= 24:
        st.inventory["enhanceProtect"] -= 1
        st.log["item_use"].append((st.abs_week, "enhanceProtect"))
        return "protected"
    apply_enh_down(e)
    return "drop"


def apply_enh_up(e: Employee):
    gain = ENHANCE[e.enh][4] + POT_ENH_BONUS[e.pot]
    e.main += gain
    e.crea += gain * 0.5
    e.salary += ENHANCE[e.enh][5]
    e.enh += 1


def apply_enh_down(e: Employee):
    if e.enh <= 0:
        return
    e.enh -= 1
    gain = ENHANCE[e.enh][4] + POT_ENH_BONUS[e.pot]
    e.main -= gain
    e.crea -= gain * 0.5
    e.salary -= ENHANCE[e.enh][5]


def expected_enh_cost(s: int) -> float:
    """E(s) = (강화비용 + 하락% × E(s−1)) ÷ 성공% [이벤트 가이드 §3-7]"""
    e_prev = 0.0
    for n in range(s + 1):
        succ, _, drop, cost, _, _ = ENHANCE[n]
        e_prev = (cost + drop / 100 * e_prev) / (succ / 100)
    return e_prev


CUM_ENH_COST = [0.0]
for _s in range(MAX_ENH):
    CUM_ENH_COST.append(CUM_ENH_COST[-1] + expected_enh_cost(_s))


# ── 채용 ────────────────────────────────────────────────────────────────────
def make_employee(st: RunState, role: str, tier: int, key) -> Tuple[Employee, float]:
    grade = st.prof.card_grade
    pot = st.rng.weighted("hire", POT_ORDER, HIRE_POT[grade][tier], *key, "pot")
    rare = 50 if grade != "Normal" else 0   # [문서] 레어 이상: 모든 능력치 +50 (코드 EmployeeData는 주스탯만)
    e = Employee(st.next_eid, role, main=st.rng.randint("hire", *HIRE_MAIN, *key, "main") + rare,
                 crea=st.rng.randint("hire", *HIRE_CREA, *key, "crea") + rare, pot=pot, grade=grade,
                 salary=st.rng.randint("hire", 2, 10, *key, "sal") * 50 if tier == 1 else 300,
                 high_sat_bonus=st.prof.t("highSatStatBonus"))
    for _ in range(HIRE_ENH[tier]):
        apply_enh_up(e)
    acc = sum(ENHANCE[n][5] for n in range(HIRE_ENH[tier]))
    disc = st.rng.weighted("hire", (0.4, 0.3, 0.2, 0.1, 0.0), HIRE_DISCOUNT[tier], *key, "disc")
    st.next_eid += 1
    return e, acc * HIRE_GRADE_MULT[grade] * (1 - disc)


def post_hiring(st: RunState):
    st.money -= HIRE_AD_COST[HIRE_TIER[st.stage]]
    st.hire_pending = HIRE_WAIT_WEEKS


def complete_hiring(st: RunState, policy):
    """면접 끝 → 후보 N명 중 빈자리만큼 채용 (돈이 되는 만큼)"""
    st.hire_pending = 0
    tier = HIRE_TIER[st.stage]
    n_cand = HIRE_CANDIDATES[tier] + int(st.prof.t("recruitApplicants"))
    for i in range(n_cand):
        if len(st.emps) >= STAGE[st.stage]["max_emp"]:
            break
        counts = {r: sum(e.role == r for e in st.emps) for r in ROLES}
        role = min(counts, key=counts.get)
        e, contract = make_employee(st, role, tier, (st.abs_week, "hire", i))
        if st.money - contract < st.reserve() * policy.hire_reserve:
            break
        st.money -= contract
        st.emps.append(e)
        on_hire(st, e)


# ── 이벤트 ──────────────────────────────────────────────────────────────────
def stage_val(v, stage):
    return v[stage - 1] if isinstance(v, tuple) and len(v) == 4 else v


def weeks_of(st: RunState, w, key) -> int:
    if w[0] == "L":
        return max(1, round(personal_buff_weeks(st) * w[1]))
    if w[0] == "n":   # [포팅: RandomStatBuffWeeksByStage] 1·2단계 5~15주, 3·4단계 10~20주
        return st.rng.randint("event", 5, 15, *key, "w") if st.stage <= 2 else st.rng.randint("event", 10, 20, *key, "w")
    return st.rng.randint("event", w[1], w[2], *key, "w")


def pick_target(st: RunState, ev, key) -> Optional[dict]:
    t = ev["target"]
    ctx = dict(event=ev, key=key)
    pool = list(st.emps)
    if t == "none":
        return ctx
    if t == "random_part":
        ctx["part"] = st.rng.pick("event", ROLES, *key, "part")
        return ctx
    if t == "random_part_led":
        led = [r for r in ROLES if st.project and r in st.project.leaders_done]
        if not led:
            return None
        ctx["part"] = st.rng.pick("event", led, *key, "part")
        return ctx
    if not pool:
        return None
    if t.startswith("emp_role:"):
        pool = [e for e in pool if e.role in t.split(":")[1]]
    elif t == "pair":
        a = st.rng.pick("event", pool, *key, "a")
        others = [e for e in pool if e.role != a.role]
        if not others:
            return None
        ctx["emp2"] = st.rng.pick("event", others, *key, "b")
        pool = [a]
    elif t == "villain":   # 만족도 최하위 중 성수 낮은 직원 [가이드 ㉑]
        pool = sorted(pool, key=lambda e: (e.sat, e.enh))[:1]
    elif t == "best":
        pool = [max(pool, key=lambda e: e.eff_main())]
    elif t == "basics":   # E(성수−1)이 R_ref의 5~10%인 직원 (잠재력 S 제외) [가이드 ㉔]
        r = st.r_ref()
        pool = [e for e in pool if e.pot != "S" and e.enh > 0 and 5 <= expected_enh_cost(e.enh - 1) / r * 100 <= 10]
    if not pool:
        return None
    ctx["emp"] = st.rng.pick("event", pool, *key, "tgt")
    ctx["part"] = ctx["emp"].role
    return ctx


def option_cost(st: RunState, ctx, opt) -> float:
    c = 0.0
    for eff in opt:
        k = eff[0]
        if k == "money_r":
            c += stage_val(eff[1], st.stage) / 100 * st.r_ref()
        elif k == "money_salary":
            c += eff[1] / 100 * st.total_salary()
        elif k == "money_emp_salary" and "emp" in ctx:
            c += eff[1] / 100 * ctx["emp"].salary
    return c


def option_ok(st: RunState, ctx, opt) -> bool:
    for eff in opt:
        if eff[0] == "req_sat_ge" and ("emp" not in ctx or ctx["emp"].sat < eff[1]):
            return False
    return st.money >= option_cost(st, ctx, opt)   # 돈이 부족한 선택지는 비활성 [가이드 §4-2]


def trigger_event(st: RunState, ev, policy):
    key = (st.releases, "ev", ev["id"])
    if ev.get("month") and month_of(st.woy) != ev["month"]:
        return
    ctx = pick_target(st, ev, key)
    if ctx is None:
        return
    options = [o for o in ev["options"] if option_ok(st, ctx, o)]
    if not options:
        return
    idx = 0 if len(options) == 1 else policy.choose_event_option(st, ctx, options)
    apply_effects(st, ctx, options[idx], key)
    st.log["events"].append((st.year, st.stage, ev["id"], ev["options"].index(options[idx])))


def change_sat(e: Employee, delta: float):
    e.sat = max(1, min(100, e.sat + delta))


def remove_employee(st: RunState, e: Employee, reason: str):
    if e not in st.emps:
        return
    st.emps.remove(e)
    st.log["resign"].append((st.year, st.stage, e.enh, reason))
    st.exits_by_year[st.year] = st.exits_by_year.get(st.year, 0) + 1
    for o in st.emps:   # 남은 직원 −5 [포팅: ReduceAllSatisfactionExcept(5)] (도주·해고도 같다고 [근사])
        o.sat = max(0, o.sat - 5)
    check_couple_exit(st, e)
    check_rumor(st)


def apply_effects(st: RunState, ctx, effects, key):
    sc = st.cfg.event_scale
    pr = st.project
    for i, eff in enumerate(effects):
        k = eff[0]
        emp, emp2 = ctx.get("emp"), ctx.get("emp2")
        if k == "chance":
            branch = eff[2] if st.rng.u("event", *key, "ch", i) < eff[1] else (eff[3] if len(eff) > 3 else [])
            apply_effects(st, ctx, branch, key + ("c", i))
        elif k == "req_sat_ge":
            pass
        elif k == "sat" and emp:
            change_sat(emp, stage_val(eff[1], st.stage) * sc)
        elif k == "sat2" and emp2:
            change_sat(emp2, eff[1] * sc)
        elif k == "sat_all":
            for e in st.emps:
                change_sat(e, stage_val(eff[1], st.stage) * sc)
        elif k in ("money_r", "money_salary", "money_emp_salary"):
            st.money -= option_cost(st, ctx, [eff])
        elif k == "gain_r":
            st.money += stage_val(eff[1], st.stage) / 100 * st.r_ref()
        elif k == "part_pct" and pr and ctx.get("part"):
            pr.part_mult[ctx["part"]] += stage_val(eff[1], st.stage) * sc / 100
        elif k == "part2_pct" and pr and emp2:
            pr.part_mult[emp2.role] += stage_val(eff[1], st.stage) * sc / 100
        elif k in ("leader_pct", "leader2_pct") and pr:   # [포팅] 지금까지 쌓인 그 파트 팀장 점수 × 10% (최소 1)
            who = emp2 if k == "leader2_pct" else emp
            if who:
                d = max(1.0, pr.leader_pts[who.role] * abs(eff[1]) / 100)
                pr.parts[who.role] += d * sc * (1 if eff[1] > 0 else -1)
        elif k == "delay" and pr:
            pr.dev_weeks += DELAY_UNIT[STAGE[st.stage]["scale"]] * eff[1]
        elif k == "buff" and emp:
            emp.buffs.append([weeks_of(st, eff[2], key + (i,)), eff[1] * sc])
        elif k == "debuff" and emp:
            emp.debuffs.append(weeks_of(st, eff[1], key + (i,)))
        elif k == "buff_all":
            for e in st.emps:
                e.buffs.append([weeks_of(st, eff[2], key + (i,)), eff[1] * sc])
        elif k == "potential" and emp:
            if emp.pot == "S":
                emp.buffs.append([personal_buff_weeks(st), 20])
            else:
                emp.pot = POT_ORDER[POT_ORDER.index(emp.pot) + 1]
        elif k == "enh" and emp:
            apply_enh_down(emp)
        elif k == "resign" and emp:
            remove_employee(st, emp, "event")
        elif k == "item_gain":
            pool = [it for it, (_, stg) in ITEMS.items() if st.stage in stg]
            give_item(st, st.rng.pick("event", pool, *key, "item", i))
        elif k == "thief":
            owned = [it for it, n in st.inventory.items() if n > 0]
            if owned:
                st.inventory[st.rng.pick("event", owned, *key, "steal")] -= 1
                if eff[1] == "r3":
                    st.money -= 0.03 * st.r_ref()
            elif eff[1] == "gold5":   # [포팅] 아이템 없으면 보유금 5%
                st.money -= max(1, 0.05 * max(0.0, st.money))
            else:                     # [가이드] R_ref 5%, 보유금 10% 상한
                st.money -= min(0.05 * st.r_ref(), 0.10 * max(0.0, st.money))
        elif k == "sales_bonus" and pr:
            pr.sales_bonus += eff[1]
        elif k == "aim_lock":
            st.aim_lock = [eff[1], eff[2]]
        elif k == "deferred" and pr:
            pr.deferred.append((pr.week + eff[1], eff[2], ctx))
        elif k == "investment" and pr:
            st.money += 0.05 * st.r_ref()   # [가이드 ⑨] 즉시 R × 5%
            pr.investment = dict(spec="guide")


# ── 조건 이벤트 ──────────────────────────────────────────────────────────────
def cond_spec(st: RunState):
    return EVENT_SPECS[st.cfg.event_spec][1]


def leader_condition_events(st: RunState, leader: Employee, part: str):
    """팀장 멈춰!(연속 3회 50%) / 나도 팀장...(연속 4회 미선정 70%) [포팅: DevelopmentManager.cs:2008, 2017]"""
    if not st.cfg.events_on:
        return
    cs = cond_spec(st)
    jealous = None
    for e in [e for e in st.emps if e.role == part]:
        if e is leader:
            e.consec_leader += 1
            e.consec_nonleader = 0
        else:
            e.consec_leader = 0
            e.consec_nonleader += 1
            if e.consec_nonleader >= 4:
                jealous = e
    key = (st.releases, "lead", part)
    j = cs["jealousy"]
    if jealous and st.stage >= j["min_stage"] and st.rng.u("event", *key, "j") < j["p"]:
        apply_effects(st, dict(emp=jealous), j["effect"], key + ("j",))
        st.log["cond"].append((st.year, "jealousy"))
    b = cs["burnout"]
    if not leader.is_ceo and leader.consec_leader >= 3 and st.stage >= b["min_stage"] \
            and st.rng.u("event", *key, "b") < b["p"]:
        apply_effects(st, dict(emp=leader), b["effect"], key + ("b",))
        st.log["cond"].append((st.year, "burnout"))


def check_rumor(st: RunState):
    """1년에 2명 이상 나가면 50% → 50% 안좋은 소문(채용 −1, 미구현) / 50% 불안감 조성 [포팅: RandomEvents_Condition:52]"""
    if not st.cfg.events_on or st.rumor_year == st.year or st.exits_by_year.get(st.year, 0) < 2 or not st.emps:
        return
    st.rumor_year = st.year
    key = (st.year, "rumor")
    if st.rng.u("event", *key) < 0.5:
        if st.rng.u("event", *key, "kind") < 0.5:
            st.log["cond"].append((st.year, "rumor"))   # TODO 채용 지원 인원 −1 (1년)
        else:
            apply_effects(st, dict(emp=st.rng.pick("event", st.emps, *key, "t")), cond_spec(st)["anxiety"], key)
            st.log["cond"].append((st.year, "anxiety"))


def on_hire(st: RunState, e: Employee):
    """사내 연애: 신규 입사 시 이성 직원과 20% [가이드 5-5 / 근사: 이성일 확률 50%]"""
    if not st.cfg.events_on or st.couple or len(st.emps) < 2:
        return
    key = (st.abs_week, "romance", e.eid)
    if st.rng.u("event", *key) < 0.2 * 0.5:
        other = st.rng.pick("event", [x for x in st.emps if x is not e], *key, "o")
        st.pending.append((st.abs_week + 2, "romance", (e.eid, other.eid)))


def give_item(st: RunState, item: str):
    st.inventory[item] = st.inventory.get(item, 0) + 1
    if item in ("coffee", "energyDrink") and st.cfg.events_on:   # 획득 시 요청 이벤트 예약 [포팅: ItemManager.AddItem]
        st.pending.append((st.abs_week + st.rng.randint("event", 2, 8, st.abs_week, item, "req"), "request", item))


def process_pending(st: RunState, policy):
    cs = cond_spec(st)
    for p in [p for p in st.pending if p[0] <= st.abs_week]:
        st.pending.remove(p)
        _, kind, data = p
        key = (st.abs_week, kind)
        if kind == "romance":
            a = next((x for x in st.emps if x.eid == data[0]), None)
            b = next((x for x in st.emps if x.eid == data[1]), None)
            if a and b:
                r = cs["romance"]
                for x in (a, b):
                    change_sat(x, r["sat"])
                    x.buffs.append([weeks_of(st, r["buff"][1], key + (x.eid,)), r["buff"][0]])
                st.couple = (a.eid, b.eid)
                st.log["cond"].append((st.year, "romance"))
                if st.rng.u("event", *key, "end") < 0.6:   # 60% 확률로 8~16주 뒤 결말
                    st.pending.append((st.abs_week + st.rng.randint("event", 8, 16, *key), "breakup", data))
        elif kind == "breakup" and st.couple == data:
            for x in st.emps:
                if x.eid in data:
                    change_sat(x, cs["breakup"])
            st.couple = None
            st.log["cond"].append((st.year, "breakup"))
        elif kind == "request":
            if st.inventory.get(data, 0) <= 0 or not st.emps:
                continue
            req = cs["coffee" if data == "coffee" else "energy"]
            e = st.rng.pick("event", st.emps, *key, "who")
            if policy.give_requested_item(st, e, data, req):
                st.inventory[data] -= 1
                if st.project and st.rng.u("event", *key, "spill") < req["spill"]:
                    st.project.dev_weeks += DELAY_UNIT[STAGE[st.stage]["scale"]]
                else:
                    e.buffs.append([weeks_of(st, req["buff"][1], key), req["buff"][0]])
                    change_sat(e, req["sat"])
            else:
                change_sat(e, req["refuse"])
            st.log["cond"].append((st.year, "request_" + data))


def check_couple_exit(st: RunState, e: Employee):
    if not st.couple or e.eid not in st.couple:
        return
    partner_id = st.couple[0] if st.couple[1] == e.eid else st.couple[1]
    st.couple = None
    partner = next((x for x in st.emps if x.eid == partner_id), None)
    if partner and st.rng.u("event", st.abs_week, "couple") < cond_spec(st)["couple_resign"]:
        remove_employee(st, partner, "couple")


# ── 상인 / 아이템 ────────────────────────────────────────────────────────────
def item_price(st: RunState, item: str) -> float:
    return round(st.r_ref() * ITEM_RATE[ITEMS[item][0]] * (1 - st.prof.t("itemDiscount") / 100), -1)


def merchant_visit(st: RunState, policy):
    pool = [i for i, (_, stages) in ITEMS.items() if st.stage in stages]
    offers = []
    for n in range(min(3, len(pool))):
        pick = st.rng.pick("merchant", pool, st.year, "pick", n)
        pool.remove(pick)
        offers.append((pick, item_price(st, pick)))
    for item, price in policy.choose_purchases(st, offers):
        if st.money >= price:
            st.money -= price
            give_item(st, item)
            st.log["items"].append((st.year, st.stage, item, price, price / st.r_ref() * 100))


def use_item(st: RunState, item: str, target: Optional[Employee] = None) -> bool:
    """아이템 v2 효과 [노션 '아이템 밸런싱 가이드']"""
    if st.inventory.get(item, 0) <= 0:
        return False
    key = (st.abs_week, "item", item)
    pr = st.project
    if item in ENH_ITEM_CAP:
        if target is None or target.enh > ENH_ITEM_CAP[item] or target.enh >= MAX_ENH:
            return False
        apply_enh_up(target)
    elif item == "resetSpirit":
        if target is None or target.pot == "S":
            return False
        target.pot = POT_ORDER[POT_ORDER.index(target.pot) + 1]
        apply_enh_down(target)
    elif item in UPGRADE_PART or item == "upgradeRandom":
        if pr is None or item in pr.upgrades_used:
            return False
        part = UPGRADE_PART.get(item) or st.rng.pick("misc", ROLES, *key)
        pr.part_mult[part] += 0.17 if item in UPGRADE_PART else 0.13
        pr.upgrades_used.add(item)
    elif item == "coffee":
        change_sat(target, 25)
    elif item == "mysteryPotion":
        change_sat(target, 5 * st.rng.randint("misc", 3, 13, *key))
    elif item == "energyDrink":
        target.sat = 100
        target.buffs.append([personal_buff_weeks(st), 20])
    elif item == "relax":
        target.debuffs = []
        change_sat(target, 20)
    elif item == "awaken":
        for e in st.emps:
            e.buffs.append([team_buff_weeks(st), 15])
    elif item in ("blockRandom", "blockLegendary"):
        if pr is None:
            return False
        pr.blocks += ([st.rng.weighted("misc", [2, 3], [0.3, 0.7], *key, n) for n in range(2)]
                      if item == "blockRandom" else [1, 1, 1])
    elif item == "techNote":
        pass  # TODO 테크트리 미구현
    else:
        return False
    st.inventory[item] -= 1
    st.log["item_use"].append((st.abs_week, item))
    return True


# ── 만족도 / 사직 / 연봉협상 ─────────────────────────────────────────────────
def weekly_satisfaction(st: RunState):
    """[포팅: EmployeeManager.OnWeekPassed] 2주차 50% / 안 떨어졌으면 3주차. 전 직원 동시, 클램프 [0,100]"""
    m, w = st.year * 12 + month_of(st.woy), week_of_month(st.woy)
    drop = False
    if st.sat_dropped_month != m:
        if w == 2:
            drop = st.rng.u("satisfaction", m, "drop") < 0.5
        elif w == 3:
            drop = True
    if drop:
        st.sat_dropped_month = m
        amount = max(0, SAT_MONTHLY_DROP - int(st.prof.t("monthlySatDropReduce")))
        for e in st.emps:
            e.sat = max(0, min(100, e.sat - amount))
        st.log["sat_drop"].append((st.abs_week, w))


def check_resignations(st: RunState, policy):
    """[포팅: RandomEventManager.cs:1078] 41 미만 매주 (50−만족도)% − 특성 c2. 80% 사직서 / 20% 도주. 한 주 1명."""
    for e in list(st.emps):
        if e.sat >= 41:
            continue
        p = (50 - e.sat) / 100 - st.prof.t("resignChanceReduce") / 100
        if p <= 0 or st.rng.u("satisfaction", st.abs_week, "resign", e.eid) >= p:
            continue
        run_away = st.rng.u("satisfaction", st.abs_week, "run", e.eid) >= 0.8
        if not run_away and st.inventory.get("hypnotizer", 0) > 0 and policy.use_hypnotizer(st, e):
            st.inventory["hypnotizer"] -= 1
            change_sat(e, 40)
            st.log["item_use"].append((st.abs_week, "hypnotizer"))
            return
        remove_employee(st, e, "run" if run_away else "resign")
        return


def salary_negotiation(st: RunState, policy):
    """[포팅: SalaryNegotiationManager] + [문서] 특성 '연봉협상 동결해도 패널티 없음'"""
    if not st.emps:
        return
    k = (st.year, "neg")
    crit = st.rng.randint("salary", 0, 2, *k, "crit")
    target = (max(st.emps, key=lambda e: e.main) if crit == 0 else
              min(st.emps, key=lambda e: e.salary) if crit == 1 else min(st.emps, key=lambda e: e.sat))
    raise_pct = st.rng.randint("salary", 8, 12, *k, "r") if crit == 0 else st.rng.randint("salary", 5, 10, *k, "r")
    angry_p = 0.0 if st.prof.t("negotiationSafe") else max(0, 90 - target.sat) / 100
    accept = policy.choose_salary(st, target, raise_pct, angry_p)
    if accept:
        target.salary = round(target.salary * (1 + raise_pct / 100))
        change_sat(target, 20)
    elif st.rng.u("salary", *k, "angry") < angry_p:
        change_sat(target, -20)
    st.log["negotiation"].append((st.year, accept, raise_pct, round(target.sat)))


def yearly_trait_recover(st: RunState):
    if st.emps and st.rng.u("misc", st.year, "a2") < st.prof.t("yearlySatRecover") / 100:
        st.rng.pick("misc", st.emps, st.year, "a2t").sat = 100
    if st.rng.u("misc", st.year, "s4") < st.prof.t("yearlyAllRecover") / 100:
        for e in st.emps:
            e.sat = max(e.sat, 95)


# ════════════════════════════════════════════════════════════════════════════
# 7. 정책 — 유저의 선택 (강화 / 이벤트 / 팀장 조준 / 아이템 / 연봉협상)
# ════════════════════════════════════════════════════════════════════════════
ENH_GAIN_CACHE: Dict[Tuple[int, int], float] = {}   # (단계, 성수) → +1강 시 연간 매출 증가 (exp_enh_roi가 채움)


class Policy:
    name = "base"
    reserve_mult = 1.0
    hire_reserve = 1.0

    def plan_enhancements(self, st) -> List[Employee]: return []
    def choose_event_option(self, st, ctx, options) -> int: return 0
    def choose_aim(self, st, ctx: LeaderCtx) -> str: return "mid"
    def choose_purchases(self, st, offers): return []
    def use_items(self, st, moment: str): pass
    def use_hypnotizer(self, st, e) -> bool: return True
    def choose_salary(self, st, e, raise_pct, angry_p) -> bool: return True
    def accept_investment(self, st, spec) -> bool: return True
    def give_requested_item(self, st, e, item, req) -> bool: return True

    def choose_platform(self, st) -> str:
        parts = {r: sum(e.eff_main() for e in st.emps if e.role == r) for r in ROLES}
        return {"P": "Mobile", "D": "PC", "A": "Nintendo"}[max(parts, key=parts.get)]

    def choose_marketing(self, st) -> Tuple[float, float]:
        if st.prof.t("marketingFree"):
            return 0.0, 1.15
        cost = 0.20 * st.total_salary()
        return (cost, 1.05) if st.money - cost > st.reserve() * 0.5 else (0.0, 0.85)

    def want_hiring(self, st) -> bool:
        return len(st.emps) < STAGE[st.stage]["max_emp"] and \
            st.money >= st.reserve() * 1.2 + HIRE_AD_COST[HIRE_TIER[st.stage]]

    def after_release(self, st): pass

    def _use_items_rules(self, st, sat_threshold=50):
        low = min(st.emps, key=lambda e: e.sat, default=None)
        pr = st.project
        for item in list(st.inventory):
            if st.inventory.get(item, 0) <= 0:
                continue
            if item in SAT_ITEMS and low and low.sat < sat_threshold:
                use_item(st, item, low)
            elif item in ENH_ITEM_CAP and st.emps:
                ok = [e for e in st.emps if e.enh <= ENH_ITEM_CAP[item]]
                if ok:
                    use_item(st, item, max(ok, key=lambda e: e.enh))
            elif item in UPGRADE_PART and pr and pr.week == pr.base_weeks // 2:
                use_item(st, item)
            elif item in ("upgradeRandom", "blockRandom", "blockLegendary") and pr and pr.week == pr.base_weeks // 2:
                use_item(st, item)
            elif item == "awaken" and pr and pr.week == 0:
                use_item(st, item)
            elif item == "relax" and low and (low.debuffs or low.sat < 60):
                use_item(st, item, low)
            elif item == "resetSpirit" and st.emps:
                cand = [e for e in st.emps if e.pot != "S"]
                if cand:
                    use_item(st, item, min(cand, key=lambda e: e.enh))


class RandomPolicy(Policy):
    name = "random"

    def choose_aim(self, st, ctx):
        return st.rng.pick("misc", ["low", "mid", "high"], st.releases, "aim", ctx.cum_ds3)

    def choose_event_option(self, st, ctx, options):
        return int(st.rng.u("misc", *ctx["key"], "opt") * len(options))

    def plan_enhancements(self, st):
        if not st.emps or st.money < st.reserve():
            return []
        return [st.rng.pick("misc", st.emps, st.abs_week, "enh", i) for i in range(3)]

    def choose_purchases(self, st, offers):
        return [o for i, o in enumerate(offers) if st.rng.u("misc", st.year, "buy", i) < 0.5]

    def use_items(self, st, moment):
        for item in list(st.inventory):
            if st.inventory[item] > 0 and st.emps:
                use_item(st, item, st.emps[0])

    def choose_salary(self, st, e, raise_pct, angry_p):
        return st.rng.u("misc", st.year, "sal") < 0.5

    def accept_investment(self, st, spec):
        return st.rng.u("misc", st.releases, "inv") < 0.5

    def give_requested_item(self, st, e, item, req):
        return st.rng.u("misc", st.abs_week, "give") < 0.5


class HeuristicPolicy(Policy):
    """평균 유저"""
    name = "heuristic"
    hire_reserve = 1.2

    def __init__(self, aim="mid", reserve_mult=1.2, enh_budget=0.5):
        self.aim, self.reserve_mult, self.enh_budget = aim, reserve_mult, enh_budget

    def choose_aim(self, st, ctx):
        return self.aim

    def choose_event_option(self, st, ctx, options):
        def score(opt):
            s = 0.0
            for eff in opt:
                if eff[0] in ("sat", "sat_all"):
                    s += stage_val(eff[1], st.stage)
                elif eff[0] == "resign":
                    s -= 50
            spend = option_cost(st, ctx, opt) > 0
            return s - (100 if spend and st.money < st.reserve() * 2 else 0)
        return max(range(len(options)), key=lambda i: score(options[i]))

    def plan_enhancements(self, st):
        budget = (st.money - st.reserve() * self.reserve_mult) * self.enh_budget
        if budget <= 0 or not st.emps:
            return []
        e = min(st.emps, key=lambda x: x.enh)
        cost = ENHANCE[min(e.enh, MAX_ENH - 1)][3]
        return [e] * int(min(10, budget // max(1, cost)))

    def choose_purchases(self, st, offers):
        spare = st.money - st.reserve() * self.reserve_mult
        out = []
        for item, price in sorted(offers, key=lambda o: o[1]):
            if price <= spare * 0.3:
                out.append((item, price))
                spare -= price
        return out

    def use_items(self, st, moment):
        self._use_items_rules(st, sat_threshold=50)

    def choose_salary(self, st, e, raise_pct, angry_p):
        return st.money > st.reserve() * 1.5

    def give_requested_item(self, st, e, item, req):
        return e.sat < 60


class ExpertPolicy(HeuristicPolicy):
    """숙련 유저 — 기대값으로 판단 (이벤트 가이드의 V 환산 사용)"""
    name = "expert"
    DELAY_V = {"small": 5.0, "mid": 6.7, "large": 7.5}
    BUFF_B = (0.290, 0.145, 0.073, 0.062)
    hire_reserve = 1.0

    def __init__(self, purchase="target", save_for_merchant=False, roi_enhance=True):
        super().__init__(aim="mid", reserve_mult=1.1, enh_budget=0.8)
        self.purchase, self.save_for_merchant, self.roi_enhance = purchase, save_for_merchant, roi_enhance
        self.measured_v: Dict[Tuple[int, str], float] = {}

    def choose_aim(self, st, ctx):
        return max(("low", "mid", "high"), key=lambda a: aim_expected_score(ctx, a))

    def sat_value(self, st, e: Employee, delta: float) -> float:
        return delta / 5 * GUIDE_SAT5_BAND[e.band()][st.stage - 1]

    def option_value(self, st, ctx, opt) -> float:
        s, v = st.stage - 1, 0.0
        emp, emp2 = ctx.get("emp"), ctx.get("emp2")
        r = st.r_ref()
        for eff in opt:
            k = eff[0]
            if k == "chance":
                v += eff[1] * self.option_value(st, ctx, eff[2]) + \
                    (1 - eff[1]) * (self.option_value(st, ctx, eff[3]) if len(eff) > 3 else 0)
            elif k == "sat" and emp:
                v += self.sat_value(st, emp, stage_val(eff[1], st.stage))
            elif k == "sat2" and emp2:
                v += self.sat_value(st, emp2, eff[1])
            elif k == "sat_all":
                v += sum(self.sat_value(st, e, stage_val(eff[1], st.stage)) for e in st.emps)
            elif k in ("money_r", "money_salary", "money_emp_salary"):
                v -= option_cost(st, ctx, [eff]) / r * 100
            elif k == "gain_r":
                v += stage_val(eff[1], st.stage)
            elif k in ("part_pct", "part2_pct"):
                v += 0.58 * stage_val(eff[1], st.stage)
            elif k in ("leader_pct", "leader2_pct"):
                v += 0.58 * eff[1] * 0.45
            elif k == "delay":
                v -= self.DELAY_V[STAGE[st.stage]["scale"]] * eff[1]
            elif k in ("buff", "debuff"):
                pct, w = (eff[1], eff[2]) if k == "buff" else (-20, eff[1])
                weeks = personal_buff_weeks(st) * w[1] if w[0] == "L" else 10
                lead = emp is not None and emp is pick_leader(st, emp.role)
                v += self.BUFF_B[s] * (2 if lead else 1) * pct / 10 * weeks
            elif k == "potential":
                v += 7.5
            elif k == "resign":
                v -= GUIDE_Q[s]
            elif k == "enh" and emp:
                v -= expected_enh_cost(max(0, emp.enh - 1)) / r * 100
            elif k == "item_gain":
                v += 0.8
            elif k == "sales_bonus":
                v += eff[1]
            elif k == "deferred":
                v += self.option_value(st, ctx, eff[2])
            elif k == "investment":
                v += 5 - 0.425 * 10
        return v

    def choose_event_option(self, st, ctx, options):
        return max(range(len(options)), key=lambda i: self.option_value(st, ctx, options[i]))

    def enhance_worth(self, st, e: Employee) -> bool:
        """+1강의 연간 매출 증가가 연봉 증가보다 클 때만 (exp_enh_roi 표). 표가 없으면 R_ref 15% 규칙."""
        inc = ENHANCE[e.enh][5]
        gain = ENH_GAIN_CACHE.get((st.stage, e.enh))
        if self.roi_enhance and gain is not None:
            return gain > inc
        return inc <= 0.15 * st.r_ref()

    def plan_enhancements(self, st):
        if self.save_for_merchant and 0 < st.merchant_week - st.woy <= 6:
            return []
        budget = st.money - st.reserve() * self.reserve_mult
        if budget <= 0 or not st.emps:
            return []
        out = []
        for e in sorted(st.emps, key=lambda x: expected_enh_cost(min(x.enh, MAX_ENH - 1))):
            if e.enh >= MAX_ENH or not self.enhance_worth(st, e):
                continue
            cost = ENHANCE[e.enh][3]
            n = min(6, int(budget * 0.5 // max(1, cost)))
            out += [e] * n
            budget -= cost * n
        return out

    def item_value(self, st, item: str) -> float:
        if self.purchase == "measured":
            return self.measured_v.get((st.stage, item), 0.0)
        return ITEM_TARGET_V[ITEMS[item][0]]

    def choose_purchases(self, st, offers):
        spare = st.money - st.reserve()
        if self.purchase == "none":
            return []
        if self.purchase == "all":
            ranked = list(offers)
        elif self.purchase == "cheap":
            ranked = sorted(offers, key=lambda o: o[1])
        else:
            pv = lambda o: o[1] / st.r_ref() * 100
            ranked = [o for o in sorted(offers, key=lambda o: -self.item_value(st, o[0]) / max(0.01, pv(o)))
                      if self.item_value(st, o[0]) > pv(o)]
        out = []
        for item, price in ranked:
            if price <= spare:
                out.append((item, price))
                spare -= price
        return out

    def use_items(self, st, moment):
        self._use_items_rules(st, sat_threshold=56)

    def choose_salary(self, st, e, raise_pct, angry_p):
        cost_v = e.salary * raise_pct / 100 / st.r_ref() * 100
        gain_v = self.sat_value(st, e, 20) + angry_p * self.sat_value(st, e, 20)
        return gain_v > cost_v and st.money > st.reserve()

    def accept_investment(self, st, spec):
        return True if spec == "code" else (5 - 0.425 * 10) > 0

    def give_requested_item(self, st, e, item, req):
        keep_v = ITEM_TARGET_V[ITEMS[item][0]] * 0.5
        give_v = (1 - req["spill"]) * 2.6 - req["spill"] * self.DELAY_V[STAGE[st.stage]["scale"]]
        return give_v - keep_v > self.sat_value(st, e, req["refuse"])


POLICIES = {"random": RandomPolicy, "heuristic": HeuristicPolicy, "expert": ExpertPolicy}

# ════════════════════════════════════════════════════════════════════════════
# 8. 한 런 실행
# ════════════════════════════════════════════════════════════════════════════


@dataclass
class RunResult:
    seed: int
    profile: str
    policy: str
    years: int
    max_stage: int
    bankrupt: Optional[str]
    stage_year: Dict[int, int]
    total_revenue: float
    play_min: float
    net_worth: float
    inv_value: float   # 안 쓰고 남은 아이템 (구매가 기준)
    log: dict


def net_worth(st: RunState) -> float:
    """보유 자금 + 직원 성수에 들어간 기대 강화비 Σ E(s)"""
    return st.money + sum(CUM_ENH_COST[e.enh] for e in st.emps)


def new_run_state(cfg: SimConfig, prof: OutgameProfile, seed: int, rng_overrides=None) -> RunState:
    st = RunState(cfg=cfg, prof=prof, rng=Rng(seed, rng_overrides), money=prof.start_money)
    e, _ = make_employee(st, "P", 1, (0, "start"))  # 시작 직원 1명 [근사]
    st.emps.append(e)
    for i in prof.traits:
        if i in TRAITS and TRAITS[i][2].startswith("startItem_"):
            for _ in range(int(TRAITS[i][3])):
                give_item(st, TRAITS[i][2].split("_", 1)[1])
    return st


def stage_scenario(cfg: SimConfig, prof: OutgameProfile, stage: int, seed: int, rng_overrides=None) -> RunState:
    """'그 단계에 막 들어온' 대표 상태 — 가이드 §3-1 대표값(직원 수·대표 성수). 이전은 출시 직후라 기존 직원 만족도 100."""
    st = RunState(cfg=cfg, prof=prof, rng=Rng(seed, rng_overrides), money=0, stage=stage)
    n, star = GUIDE_REP[stage]
    for i in range(n):
        e, _ = make_employee(st, ROLES[i % 3], 1, (0, "scn", i))
        target = max(0, star + st.rng.randint("hire", -2, 2, 0, "scn", i, "star"))
        while e.enh < target:
            apply_enh_up(e)
        e.sat = 100
        st.emps.append(e)
    st.releases = {1: 3, 2: 8, 3: 15, 4: 25}[stage]
    st.last_rev, st.last_rev_mult = GUIDE_RREF[stage], scale_mult(stage, n)
    st.money = st.reserve() * 2
    st.log["stage_year"] = {stage: 0}
    return st


def stage_up_allowed(st: RunState) -> bool:
    if st.stage >= 4:
        return False
    cost = st.cfg.rent.base[st.stage] * st.cfg.move_cost_mult
    nxt = st.stage + 1
    if st.cfg.move_rule == "score":
        need = cost + st.total_salary() + st.cfg.rent.base[nxt - 1] + STAGE[nxt]["dev_cost"]
        return st.move_ready and st.money >= need
    if st.cfg.move_rule == "money":
        return len(st.emps) >= STAGE[st.stage]["max_emp"] and st.money >= cost + st.reserve() * 2
    return False


def simulate_run(cfg: SimConfig, prof: OutgameProfile, policy: Policy, seed: int, rng_overrides=None,
                 inject: Optional[Callable[[RunState], None]] = None, start: Optional[RunState] = None,
                 max_weeks: Optional[int] = None) -> RunResult:
    st = start or new_run_state(cfg, prof, seed, rng_overrides)
    weeks = 0
    while st.year < cfg.max_years and st.bankrupt is None and (max_weeks is None or weeks < max_weeks):
        weeks += 1
        if st.woy == 0:
            if st.year > 0 or start is not None:
                pay = st.total_salary()
                if st.money < pay and st.prof.t("brokeRescue") and not st.poor_rescue_used and st.emps:
                    st.poor_rescue_used = True   # 특성 '가난한 회사' [근사: 지급 불가 시점에 1회]
                    st.money += st.rng.pick("misc", st.emps, st.year, "rescue").salary
                if st.money < pay:
                    st.bankrupt = "salary"
                    break
                st.money -= pay
                if weeks > 1:
                    st.years_in_stage += 1
            st.merchant_week = (st.rng.randint("merchant", *MERCHANT_MONTHS, st.year, "m") - 1) * 4 \
                + st.rng.randint("merchant", 0, 3, st.year, "w")
            r = st.rng.randint("salary", 0, 7, st.year, "when")
            st.negotiation_week = ((11 if r < 4 else 12) - 1) * 4 + r % 4
            yearly_trait_recover(st)
            st.log["rref_year"].append((st.year, st.stage, st.r_ref()))
        if st.woy == RENT_WEEK:
            if st.money < st.rent_now():
                st.bankrupt = "rent"
                break
            st.money -= st.rent_now()
        weekly_satisfaction(st)
        if cfg.item_spec != "off" and st.woy == st.merchant_week:
            merchant_visit(st, policy)
        if cfg.salary_negotiation and st.woy == st.negotiation_week:
            salary_negotiation(st, policy)
        process_pending(st, policy)
        if inject is not None:
            inject(st)
        if st.sales_queue:
            inc = st.sales_queue.pop(0)
            st.money += inc
            st.log["income"].append((st.abs_week, inc))
        frozen = st.abs_week < st.freeze_until
        # ── 다음 게임 시작 직전: 사무실 이전 → 인원 충원 → 개발 시작 ──
        if st.project is None:
            if not frozen and stage_up_allowed(st):
                st.money -= cfg.rent.base[st.stage] * cfg.move_cost_mult
                st.stage += 1
                st.years_in_stage = 0
                st.move_ready = False
                st.log["stage_year"][st.stage] = st.year
                st.waiting_after_move = cfg.wait_hires_after_move
            if not frozen and not st.hire_pending and policy.want_hiring(st):
                post_hiring(st)
            if st.waiting_after_move and not st.hire_pending:
                st.waiting_after_move = False
            if not st.waiting_after_move and st.money >= dev_cost(st):
                start_project(st, policy)
        elif not frozen and not st.hire_pending and policy.want_hiring(st):
            post_hiring(st)
        if st.project is not None:
            progress_project(st, policy)
        else:
            st.play_sec += IDLE_SEC_PER_WEEK
        for i, e in enumerate([] if frozen else policy.plan_enhancements(st)):
            if e not in st.emps or e.enh >= MAX_ENH:
                continue
            if st.money - ENHANCE[e.enh][3] < st.reserve() * policy.reserve_mult:
                break
            enhance_once(st, e, (st.abs_week, "enh", e.eid, i))
        if st.hire_pending:
            st.hire_pending -= 1
            if st.hire_pending <= 0:
                complete_hiring(st, policy)
        for e in st.emps:
            for b in e.buffs:
                b[0] -= 1
            e.buffs = [b for b in e.buffs if b[0] > 0]
            e.debuffs = [d - 1 for d in e.debuffs if d - 1 > 0]
        check_resignations(st, policy)
        if st.woy % 4 == 0:
            st.log["bands"].append((st.stage, [e.band() for e in st.emps]))
        st.woy += 1
        if st.woy >= WEEKS_PER_YEAR:
            st.woy = 0
            st.year += 1
    return RunResult(seed, prof.name, policy.name, st.year, st.stage, st.bankrupt, dict(st.log["stage_year"]),
                     sum(g["revenue"] for g in st.log["games"]), st.play_sec / 60, net_worth(st),
                     sum(n * item_price(st, it) for it, n in st.inventory.items() if n > 0 and it in ITEMS), st.log)


# ════════════════════════════════════════════════════════════════════════════
# 9. 실험
# ════════════════════════════════════════════════════════════════════════════
def mean(xs):
    xs = list(xs)
    return statistics.mean(xs) if xs else float("nan")


def se(xs):
    xs = list(xs)
    return statistics.pstdev(xs) / max(1, len(xs)) ** 0.5 if len(xs) > 1 else 0.0


def cfg_with(cfg: SimConfig, **kw) -> SimConfig:
    return SimConfig(**{**cfg.__dict__, **kw})


def batch(cfg, prof, policy_name, seeds, **kw) -> List[RunResult]:
    return [simulate_run(cfg, prof, POLICIES[policy_name](), s, **kw) for s in seeds]


def reach_rate(results: List[RunResult], stage: int) -> float:
    return sum(r.max_stage >= stage for r in results) / len(results)


def scenario_runs(cfg, prof, stage, seeds, policy_factory=HeuristicPolicy, weeks=96, **kw) -> List[RunResult]:
    c = cfg_with(cfg, move_rule="locked")
    return [simulate_run(c, prof, policy_factory(), s, start=stage_scenario(c, prof, stage, s), max_weeks=weeks, **kw)
            for s in seeds]


# ── 9-1. 강화 손익 ──
def expected_parts(st: RunState, policy, n_mc=120) -> Dict[str, float]:
    """팀의 작품당 기대 파트 점수 (틱 기대값 + 팀장 점수 몬테카를로)"""
    awaken = st.cfg.tech_awaken[st.stage - 1]
    parts = {"P": 0.0, "D": 0.0, "A": 0.0}
    blocks = 0.0
    for e in st.emps:
        c = tick_counts(e.eff_crea(), awaken)
        S = tick_S(e.eff_main())
        parts[e.role] += c["success"] * S + c["jackpot"] * 1.8 * S
        blocks += c["creativity"] * 3.0
    saved_rng, saved_rel, saved_lock = st.rng, st.releases, st.aim_lock
    st.aim_lock = None
    for part in ROLES:
        leader = pick_leader(st, part)
        tot = 0.0
        for i in range(n_mc):
            st.rng, st.releases = Rng(10_000 + i), 0
            tot += run_leader_session(st, leader, part, policy)
        parts[part] += tot / n_mc
    st.rng, st.releases, st.aim_lock = saved_rng, saved_rel, saved_lock
    cap = st.cfg.grid_cells[st.stage - 1]
    cells = min(cap, blocks)
    parts["C"] = cells * st.cfg.cell_score[st.stage - 1] * ((1 + PERFECT_BONUS) if cells >= cap else 1)
    return parts


def expected_revenue(st: RunState, policy, n_mc=120) -> float:
    pt = expected_parts(st, policy, n_mc)
    raw = PLATFORM[policy.choose_platform(st)](pt["P"], pt["D"], pt["A"], pt["C"])
    q = raw * 1.06 * 1.05
    return scale_mult(st.stage, len(st.emps)) * 248 * (q / 100) ** 2


def exp_enh_roi(cfg, profile="P0_신규", seeds=5, quiet=False, n_mc=120):
    """+1강의 연간 매출 증가 vs 연봉 증가 — 단계별 대표 팀에서 한 직원의 성수만 바꿔가며 계산.
    연간 매출 = 작품당 매출 × (48 ÷ 개발 기간). 팀장(그 파트 최고) / 일반 직원 두 경우. 만족도 80 고정."""
    prof = PROFILES[profile]
    pol = ExpertPolicy()
    rows = {}
    for stage in (1, 2, 3, 4):
        per_year = WEEKS_PER_YEAR / STAGE[stage]["dev_weeks"]
        for n in range(0, MAX_ENH):
            g_lead, g_mem = [], []
            for s in range(seeds):
                for who in ("lead", "member"):
                    st = stage_scenario(cfg, prof, stage, s)
                    if who == "member" and len(st.emps) <= 3:
                        continue
                    for e in st.emps:
                        e.sat = 80
                    tgt = st.emps[0] if who == "lead" else st.emps[3]
                    while tgt.enh > n:
                        apply_enh_down(tgt)
                    while tgt.enh < n:
                        apply_enh_up(tgt)
                    others = [o for o in st.emps if o.role == tgt.role and o is not tgt]
                    for o in others:   # 팀장 = 대상(lead) / 다른 직원(member)이 되도록 능력치 조정
                        if who == "lead":
                            o.main = min(o.main, tgt.main * 0.9)
                        else:
                            o.main = max(o.main, tgt.main * 1.1 + 200)
                    r0 = expected_revenue(st, pol, n_mc)
                    apply_enh_up(tgt)
                    r1 = expected_revenue(st, pol, n_mc)
                    (g_lead if who == "lead" else g_mem).append((r1 - r0) * per_year)
            rows[(stage, n)] = (mean(g_lead), mean(g_mem) if g_mem else float("nan"))
            ENH_GAIN_CACHE[(stage, n)] = rows[(stage, n)][1] if g_mem else rows[(stage, n)][0]
    if quiet:
        return rows
    typical = {1: range(0, 12), 2: range(8, 18), 3: range(13, 23), 4: range(17, 25)}
    print(f"[강화 손익] {profile} 대표 팀에서 대상 직원 성수만 변경, 만족도 80. 단위 G/년")
    print("  '팀장' = 그 파트 팀장인 직원 / '일반' = 같은 직군의 팀장 아닌 직원 (2단계부터)")
    for stage in (1, 2, 3, 4):
        print(f"\n  [{stage}단계] 연간 작품 {WEEKS_PER_YEAR / STAGE[stage]['dev_weeks']:.1f}개, 대표 성수 {GUIDE_REP[stage][1]}")
        print(f"  {'강화':>7} {'강화비 E(s)':>11} {'연봉 증가/년':>11} {'매출 증가(팀장)':>15} {'매출 증가(일반)':>15} "
              f"{'순이득(팀장)':>12} {'순이득(일반)':>12} {'강화비 회수':>9}")
        for n in typical[stage]:
            gl, gm = rows[(stage, n)]
            inc = ENHANCE[n][5]
            g = gm if gm == gm else gl
            net = g - inc
            pay = f"{expected_enh_cost(n) / net:.1f}년" if net > 0 else "회수 불가"
            print(f"  {n:>3}→{n + 1:<3} {expected_enh_cost(n):>11,.0f} {inc:>11,} {gl:>15,.0f} "
                  f"{(gm if gm == gm else float('nan')):>15,.0f} {gl - inc:>12,.0f} "
                  f"{(gm - inc if gm == gm else float('nan')):>12,.0f} {pay:>9}")
    return rows


def recommend_salary(rows, ratio=0.5):
    """연봉 증가 상한 = 그 성수가 주로 쓰이는 단계의 '일반 직원' 연간 매출 증가 × ratio"""
    home = lambda n: 1 if n <= 10 else 2 if n <= 15 else 3 if n <= 20 else 4
    print(f"\n[권장] 도달 성수별 연봉 증가 상한 = 주 활동 단계에서 일반 직원 연간 매출 증가의 {ratio:.0%}")
    print(f"  {'도달 성수':>8} {'주 단계':>6} {'현재 연봉 증가':>13} {'연간 매출 증가':>14} {'권장 상한':>11} {'판정':>4}")
    for n in range(MAX_ENH):
        stg = home(n + 1)
        gl, gm = rows[(stg, n)]
        g = gm if gm == gm else gl
        inc = ENHANCE[n][5]
        mark = "✅" if inc <= g * ratio else "⚠" if inc <= g else "❌"
        print(f"  {n + 1:>7}성 {stg:>6} {inc:>13,} {g:>14,.0f} {g * ratio:>11,.0f} {mark:>4}")


# ── 9-2. 만족도 추적 ──
def exp_trace(cfg, stage, profile, seed=1, weeks=64, events=False):
    c = cfg_with(cfg, move_rule="locked", events_on=events, item_spec="off", salary_negotiation=events)
    st = stage_scenario(c, PROFILES[profile], stage, seed)
    for e in st.emps:
        e.sat = 80
    pol = HeuristicPolicy()
    pol.plan_enhancements = lambda s: []
    pol.want_hiring = lambda s: False
    print(f"[{stage}단계 / {profile} / 이벤트 {'켬(' + c.event_spec + ')' if events else '끔'}] 주차별 만족도")
    print("  주차      " + "  ".join(f"직원{e.eid}({e.role})" for e in st.emps) + "   사건")
    prev = dict(games=0, sat_drop=0, events=0, cond=0)
    for _ in range(weeks):
        simulate_run(c, st.prof, pol, seed, start=st, max_weeks=1)
        note = []
        if len(st.log["sat_drop"]) > prev["sat_drop"]:
            note.append(f"월간 하락(−5, {st.log['sat_drop'][-1][1]}주차)")
        if len(st.log["games"]) > prev["games"]:
            note.append("완성 +40")
        note += [f"이벤트 {e[2]}(선택 {e[3] + 1})" for e in st.log["events"][prev["events"]:]]
        note += [f"조건 {x[1]}" for x in st.log["cond"][prev["cond"]:]]
        prev = {k: len(st.log[k]) for k in prev}
        wk = (st.woy - 1) % WEEKS_PER_YEAR
        print(f"  {month_of(wk):2d}월 {week_of_month(wk)}주  " + "  ".join(f"{round(e.sat):>10}" for e in st.emps)
              + "   " + ", ".join(note))


# ── 9-3. 가정 검증 ──
def band_share(runs: List[RunResult]):
    bands = [b for r in runs for _, bs in r.log["bands"] for b in bs]
    return {k: 100 * sum(b == k for b in bands) / max(1, len(bands)) for k in ("safe", "caution", "danger")}


def exp_validate(cfg, runs, profile="P2_중급"):
    prof, seeds = PROFILES[profile], range(runs)

    def verdict(m, lo, hi):
        return "✅" if lo <= m <= hi else "⚠"
    print(f"[가정 검증] 이벤트 {cfg.event_spec}, 스펙 {profile}, 단계별 {runs}판 × 96주 (평균 유저, 단계 고정)\n")
    rs = scenario_runs(cfg, prof, 2, seeds)
    drops = [d for r in rs for d in r.log["sat_drop"]]
    w2 = sum(1 for _, w in drops if w == 2) / max(1, len(drops))
    print(f"(1) 월간 만족도 하락: 달마다 {len(drops) / (runs * 24):.2f}회 (규칙 1.00) / 2주차 {w2:.0%} (규칙 50%)")
    print("\n(2) 단계별 — 가이드 가정 vs 시뮬레이터")
    print(f"  {'지표':34} {'단계':>4} {'가이드':>14} {'측정':>16}  판정")
    for stage in (1, 2, 3, 4):
        rs = scenario_runs(cfg, prof, stage, seeds)
        quiet = scenario_runs(cfg_with(cfg, events_on=False, salary_negotiation=False), prof, stage, seeds)
        games = [g for r in rs for g in r.log["games"]]
        qgames = [g for r in quiet for g in r.log["games"]]
        sh = band_share(rs)
        g = GUIDE_BANDS[stage]
        print(f"  {'만족도 안전/주의/위험(%)':32} {stage:>4} {'%d/%d/%d' % g:>14} "
              f"{'%d/%d/%d' % (round(sh['safe']), round(sh['caution']), round(sh['danger'])):>16}  "
              f"{verdict(sh['safe'], g[0] - 10, g[0] + 10)}")
        pre = mean(min(gm["pre_sat"]) for gm in qgames if gm["pre_sat"])
        print(f"  {'완성 직전 최저 만족도(이벤트 없음)':28} {stage:>4} {GUIDE_PRE_COMPLETE_SAT[stage]:>14} {pre:>16.0f}  "
              f"{verdict(pre, GUIDE_PRE_COMPLETE_SAT[stage] - 7, GUIDE_PRE_COMPLETE_SAT[stage] + 7)}")
        c_share = 100 * mean(gm["share"].get("C", 0) for gm in games)
        print(f"  {'창의성 비중(%)':35} {stage:>4} {13:>14} {c_share:>16.1f}  {verdict(c_share, 9, 17)}")
        part = 100 * mean(mean(gm["share"].get(k, 0) for k in ROLES) for gm in games)
        print(f"  {'파트 1개 비중(%)':35} {stage:>4} {29:>14} {part:>16.1f}  {verdict(part, 25, 33)}")
        lead = 100 * mean(mean(gm["leader_share"].values()) for gm in games if gm["leader_share"])
        print(f"  {'팀장 점수 비중(%)':34} {stage:>4} {GUIDE_LEADER_SHARE[stage]:>14} {lead:>16.0f}  "
              f"{verdict(lead, GUIDE_LEADER_SHARE[stage] - 10, GUIDE_LEADER_SHARE[stage] + 10)}")
        ev = mean(gm["n_events"] for gm in games)
        ge = GUIDE_EVENTS_PER_PROJECT[cfg.event_spec][stage]
        print(f"  {'프로젝트당 이벤트 수':32} {stage:>4} {ge:>14} {ev:>16.2f}  {verdict(ev, ge - 0.3, ge + 0.3)}")
        r1 = statistics.median(r.log["games"][0]["salary_ratio"] for r in rs if r.log["games"])
        print(f"  {'총 연봉 ÷ 첫 작품 매출':31} {stage:>4} {'0.38~0.73':>14} {r1:>16.2f}  {verdict(r1, 0.38, 0.73)}")
        first = mean(r.log["games"][0]["revenue"] for r in rs if r.log["games"])
        print(f"  {'대표 팀 첫 작품 매출 ÷ 가이드 R_ref':27} {stage:>4} {1.0:>14} {first / GUIDE_RREF[stage]:>16.2f}  "
              f"{verdict(first / GUIDE_RREF[stage], 0.7, 1.3)}")
        resign = sum(len(r.log["resign"]) for r in rs) / runs
        print(f"  {'96주 동안 퇴사 수(1판)':32} {stage:>4} {'—':>14} {resign:>16.2f}")
        print()


# ── 9-4. 단계별 만족도 가치 ──
def exp_satvalue(cfg, runs, profile="P2_중급"):
    """단계별로 (a) 측정 분포 × 가이드 구간 가치 (b) A/B 직접 측정.
    A/B: 개발 2주차에 '랜덤 직원' 또는 '최저 만족도 직원' +25 → 96주 뒤 순자산 차이 ÷ 5 (= +5당 V)."""
    prof = PROFILES[profile]
    c = cfg_with(cfg, move_rule="locked")
    print(f"[만족도 가치] 이벤트 {cfg.event_spec}, {profile}, 단계별 {runs}판. 단위: 1인 만족도 +5당 V")
    print(f"  {'단계':>4} {'측정 분포 안전/주의/위험':>22} {'가이드공식×측정분포':>18} {'가이드 값':>9} "
          f"{'A/B 랜덤 직원':>14} {'A/B 최저 직원':>14}")
    for stage in (1, 2, 3, 4):
        rs = scenario_runs(c, prof, stage, range(runs))
        sh = band_share(rs)
        formula = sum(sh[b] / 100 * GUIDE_SAT5_BAND[b][stage - 1] for b in ("safe", "caution", "danger"))
        res = {}
        for mode in ("random", "lowest"):
            d = []
            for s in range(runs):
                a0, b0 = stage_scenario(c, prof, stage, s), stage_scenario(c, prof, stage, s)
                rref = a0.r_ref()
                done = {}

                def once(st, done=done, mode=mode, s=s):
                    if not done and st.project is not None and st.project.week == 2 and st.emps:
                        done[1] = True
                        t = (min(st.emps, key=lambda e: e.sat) if mode == "lowest"
                             else st.rng.pick("misc", st.emps, s, "satv"))
                        change_sat(t, 25)
                a = simulate_run(c, prof, HeuristicPolicy(), s, start=a0, inject=once, max_weeks=96)
                b = simulate_run(c, prof, HeuristicPolicy(), s, start=b0, max_weeks=96)
                d.append((a.net_worth - b.net_worth) / rref * 100 / 5)
            res[mode] = (mean(d), se(d))
        print(f"  {stage:>4} {'%d/%d/%d' % (round(sh['safe']), round(sh['caution']), round(sh['danger'])):>22} "
              f"{formula:>18.2f} {GUIDE_SAT5_AVG[stage - 1]:>9.2f} "
              f"{res['random'][0]:>8.2f}±{res['random'][1]:<5.2f} {res['lowest'][0]:>8.2f}±{res['lowest'][1]:<5.2f}")


# ── 9-5. 아웃게임 요소별 효과 ──
def exp_outgame(cfg, runs):
    """기준(P0) 대비 요소 하나만 켰을 때 — 단계별 첫 작품 매출 변화(%)와 96주 순자산 변화(V)"""
    base = PROFILES["P0_신규"]
    c = cfg_with(cfg, move_rule="locked")
    variants = {"카드 레어(+50)": dict(card_grade="Rare"), "카드 레전더리": dict(card_grade="Legendary")}
    for lv in (10, 20, 30, 40):
        variants[f"CEO 전 파트 Lv{lv}"] = dict(ceo_levels=(lv, lv, lv))
    for tid in ("enh_s", "enh_a", "highsat_a", "highsat_b", "sat_drop_s", "resign_c", "marketing_god",
                "large_sale_s", "mid_sale_a", "small_sale_b", "negotiation_b", "block_b", "high_enh_s"):
        variants[f"특성 {TRAITS[tid][0]}"] = dict(traits=(tid,))
    print(f"[아웃게임 요소별 효과] P0 기준, 요소 하나만 켬. 단계별 {runs}판. 첫 작품 매출 % / 96주 순자산 V")
    print(f"  {'요소':24} " + " ".join(f"{f'{s}단계':>18}" for s in (1, 2, 3, 4)))
    base_runs = {s: scenario_runs(c, base, s, range(runs)) for s in (1, 2, 3, 4)}
    rrefs = {s: [stage_scenario(c, base, s, i).r_ref() for i in range(runs)] for s in (1, 2, 3, 4)}
    for name, kw in variants.items():
        prof = OutgameProfile(name, **kw)
        cells = []
        for stage in (1, 2, 3, 4):
            b, v = base_runs[stage], scenario_runs(c, prof, stage, range(runs))
            dr = mean((x.log["games"][0]["revenue"] / y.log["games"][0]["revenue"] - 1) * 100
                      for x, y in zip(v, b) if x.log["games"] and y.log["games"])
            dn = mean((x.net_worth - y.net_worth) / rr * 100 for x, y, rr in zip(v, b, rrefs[stage]))
            cells.append(f"{dr:+6.1f}% {dn:+8.1f}V")
        print(f"  {name:24} " + " ".join(f"{x:>18}" for x in cells))
    print("  [문서 예상 '누적 매출 영향'] CEO Lv20 +2~4%, Lv30 +5~7%, Lv40 +9~11%")


# ── 9-6. 아이템 ──
def measure_items(cfg, runs, profile="P2_중급", stages=(1, 2, 3, 4)) -> Dict[Tuple[int, str], float]:
    prof = PROFILES[profile]
    c = cfg_with(cfg, move_rule="locked", item_spec="off")
    out = {}
    print(f"[아이템 실측] 이벤트 {cfg.event_spec}, {profile}, 단계별 {runs}판 (숙련 유저의 사용 규칙)")
    print(f"  {'단계':>4} {'아이템':16} {'등급':>4} {'목표V':>6} {'A)매출48주':>10} {'B)순자산96주':>12} {'±':>5} "
          f"{'가격V':>6} {'B÷가격':>7} {'사용률':>6}")
    for stage in stages:
        for item, (grade, stages_) in ITEMS.items():
            if stage not in stages_:
                continue
            da, db, used = [], [], 0
            for s in range(runs):
                rref = stage_scenario(c, prof, stage, s).r_ref()
                pair = []
                for give in (True, False):
                    for frozen, weeks in ((True, 48), (False, 96)):
                        st0 = stage_scenario(c, prof, stage, s)
                        if frozen:
                            st0.freeze_until = weeks
                        if give:
                            st0.inventory[item] = 1
                        r = simulate_run(c, prof, ExpertPolicy(purchase="none"), s, start=st0, max_weeks=weeks)
                        pair.append(r)
                        if give and not frozen and any(i == item for _, i in r.log["item_use"]):
                            used += 1
                inc = lambda r: sum(v for _, v in r.log["income"])
                da.append((inc(pair[0]) - inc(pair[2])) / rref * 100)
                db.append((pair[1].net_worth - pair[3].net_worth) / rref * 100)
            mb = mean(db)
            price_v = ITEM_RATE[grade] * 100
            out[(stage, item)] = mb
            print(f"  {stage:>4} {item:16} {grade:>4} {ITEM_TARGET_V[grade]:>6} {mean(da):>10.1f} {mb:>12.1f} "
                  f"{se(db):>5.1f} {price_v:>6.1f} {mb / price_v:>7.1f} {used / runs:>6.0%}")
    return out


def exp_items(cfg, runs, profile="P2_중급"):
    measured = measure_items(cfg, runs, profile)
    prof = PROFILES[profile]
    c = cfg_with(cfg, move_rule="locked")
    strategies = {"안 삼": ("none", False), "전부": ("all", False), "싼 것부터": ("cheap", False),
                  "목표V÷가격": ("target", False), "실측V÷가격": ("measured", False),
                  "실측V÷가격+저축": ("measured", True)}
    print(f"\n[구매 전략 비교] {profile}, 단계별 {runs}판 × 96주 — '안 삼' 대비 순자산(V, 남은 아이템은 구매가) / 파산률 / 구매 수")
    print(f"  {'단계':>4} " + " ".join(f"{k:>19}" for k in strategies))
    for stage in (1, 2, 3, 4):
        base_nw, cells = None, []
        for name, (strat, save) in strategies.items():
            def factory(strat=strat, save=save):
                p = ExpertPolicy(purchase=strat, save_for_merchant=save)
                p.measured_v = measured
                return p
            rs = scenario_runs(c, prof, stage, range(runs), policy_factory=factory)
            rrefs = [stage_scenario(c, prof, stage, s).r_ref() for s in range(runs)]
            nw = [(r.net_worth + r.inv_value) / rr * 100 for r, rr in zip(rs, rrefs)]
            base_nw = base_nw or nw
            cells.append(f"{mean(a - b for a, b in zip(nw, base_nw)):+7.1f}V "
                         f"{mean(r.bankrupt is not None for r in rs):3.0%} {mean(len(r.log['items']) for r in rs):3.1f}개")
        print(f"  {stage:>4} " + " ".join(f"{x:>19}" for x in cells))


# ── 9-7. 단계 도달 / 운·실력 / 주입 / 보정 ──
def exp_reach(cfg, runs, profiles=None, policies=("random", "heuristic", "expert")):
    print(f"[단계 도달] 이벤트 {cfg.event_spec}, {runs}판")
    print(f"{'스펙':10} {'정책':10} {'2단계':>6} {'3단계':>6} {'4단계':>6} {'생존(년)':>8} {'플레이(분)':>9} {'파산 원인':>18}")
    for pname in profiles or PROFILES:
        for pol in policies:
            rs = batch(cfg, PROFILES[pname], pol, range(runs))
            causes = {c: sum(r.bankrupt == c for r in rs) for c in ("salary", "rent")}
            print(f"{pname:10} {pol:10} {reach_rate(rs, 2):6.0%} {reach_rate(rs, 3):6.0%} {reach_rate(rs, 4):6.0%} "
                  f"{mean(r.years for r in rs):8.1f} {mean(r.play_min for r in rs):9.1f} {str(causes):>18}")


def exp_luck(cfg, runs, profile="P2_중급"):
    prof, seeds = PROFILES[profile], range(runs)
    score = lambda r: r.max_stage + min(r.years, cfg.max_years) / cfg.max_years
    by_pol = {p: [score(r) for r in batch(cfg, prof, p, seeds)] for p in POLICIES}
    print(f"[{profile}] 정책별 평균 진행도 (같은 시드 {runs}개)")
    for p, v in by_pol.items():
        print(f"  {p:10} 평균 {mean(v):.2f}  표준편차(운) {statistics.pstdev(v):.2f}")
    gap = mean(by_pol["expert"]) - mean(by_pol["random"])
    luck = statistics.pstdev(by_pol["heuristic"])
    print(f"  실력 폭 {gap:.2f} vs 운 폭 {luck:.2f} → 실력 비중 ≈ {gap / max(1e-9, gap + luck):.0%}")
    for stream in STREAMS:
        v = [score(simulate_run(cfg, prof, HeuristicPolicy(), 0, rng_overrides={stream: 10_000 + s})) for s in seeds]
        print(f"    {stream:12} {statistics.pstdev(v):.3f}")


def exp_inject(cfg, runs, profile="P2_중급", horizon_weeks=48):
    prof = PROFILES[profile]
    c = cfg_with(cfg, move_rule="locked")
    tests = {
        "1인 만족도 +25 (가이드 ≈5V)": lambda st: change_sat(min(st.emps, key=lambda e: e.sat), 25),
        "랜덤 파트 +9% (가이드 ≈5.2V)": lambda st: st.project.part_mult.__setitem__("P", st.project.part_mult["P"] + 0.09),
        "개발 지연 1단위 (가이드 −5~7.5V)": lambda st: setattr(
            st.project, "dev_weeks", st.project.dev_weeks + DELAY_UNIT[STAGE[st.stage]["scale"]]),
    }
    for stage in (1, 2, 3, 4):
        print(f"  [{stage}단계]")
        for name, fn in tests.items():
            d = []
            for s in range(runs):
                a0, b0 = stage_scenario(c, prof, stage, s), stage_scenario(c, prof, stage, s)
                for x in (a0, b0):
                    x.freeze_until = horizon_weeks
                rref = a0.r_ref()
                done = {}

                def once(st, done=done, fn=fn):
                    if not done and st.project is not None and st.project.week == 2:
                        done[1] = True
                        fn(st)
                a = simulate_run(c, prof, HeuristicPolicy(), s, start=a0, inject=once, max_weeks=horizon_weeks)
                b = simulate_run(c, prof, HeuristicPolicy(), s, start=b0, max_weeks=horizon_weeks)
                inc = lambda r: sum(v for _, v in r.log["income"])
                d.append((inc(a) - inc(b)) / rref * 100)
            print(f"    {name:32} 실측 {mean(d):+6.2f}V (±{se(d):.2f})")


def exp_calibrate(cfg, runs, policy="heuristic", targets=None):
    targets = targets or {("P0_신규", 2): 0.5, ("P2_중급", 3): 0.4, ("P4_최고", 4): 0.5}  # [임시] 기획 목표로 교체

    def rate_at(pname, stage, growth):
        g = list(cfg.rent.growth)
        g[stage - 2] = growth
        return reach_rate(batch(cfg_with(cfg, rent=RentConfig(cfg.rent.base, tuple(g))), PROFILES[pname], policy,
                                range(runs)), stage)

    for (pname, stage), goal in targets.items():
        lo, hi = 0.0, 0.6
        if rate_at(pname, stage, lo) < goal:
            print(f"  {pname} → {stage}단계 {goal:.0%}: 불가 (상승률 0%여도 미달 — 사무실비 말고 다른 병목)")
            continue
        if rate_at(pname, stage, hi) > goal:
            print(f"  {pname} → {stage}단계 {goal:.0%}: 상승률 {hi:.0%}로도 너무 쉬움 — 기준가를 올려야 함")
            continue
        for _ in range(8):
            mid = (lo + hi) / 2
            lo, hi = (mid, hi) if rate_at(pname, stage, mid) > goal else (lo, mid)
        print(f"  {pname} → {stage}단계 도달률 {goal:.0%}: {stage - 1}단계 상승률 ≈ {(lo + hi) / 2:.1%}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("cmd", choices=["one", "trace", "validate", "enhroi", "satvalue", "outgame", "items", "reach",
                                    "luck", "inject", "calibrate"])
    ap.add_argument("--runs", type=int, default=100)
    ap.add_argument("--profile", default="P2_중급")
    ap.add_argument("--stage", type=int, default=2)
    ap.add_argument("--years", type=int, default=40)
    ap.add_argument("--spec", default="guide", choices=["guide", "code"], help="이벤트 버전")
    ap.add_argument("--events", action="store_true", help="trace에서 이벤트·연봉협상 켜기")
    a = ap.parse_args()
    try:
        import sys
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    cfg = SimConfig(max_years=a.years, event_spec=a.spec)
    if a.cmd == "enhroi":
        rows = exp_enh_roi(cfg, profile="P0_신규")
        recommend_salary(rows)
        return
    exp_enh_roi(cfg, quiet=True, seeds=2, n_mc=40)   # 숙련 유저의 강화 판단에 쓰는 표
    if a.cmd == "trace":
        exp_trace(cfg, a.stage, a.profile, events=a.events)
    elif a.cmd == "validate":
        exp_validate(cfg, a.runs, a.profile)
    elif a.cmd == "satvalue":
        exp_satvalue(cfg, a.runs, a.profile)
    elif a.cmd == "outgame":
        exp_outgame(cfg, a.runs)
    elif a.cmd == "items":
        exp_items(cfg, a.runs, a.profile)
    elif a.cmd == "reach":
        exp_reach(cfg, a.runs)
    elif a.cmd == "luck":
        exp_luck(cfg, a.runs, a.profile)
    elif a.cmd == "inject":
        exp_inject(cfg, a.runs, a.profile)
    elif a.cmd == "calibrate":
        exp_calibrate(cfg, a.runs)
    else:
        r = simulate_run(cfg, PROFILES[a.profile], ExpertPolicy(), 1)
        print(r.years, r.max_stage, r.bankrupt, r.stage_year, f"{r.total_revenue:,.0f}", f"{r.play_min:.1f}분")
        for g in r.log["games"][:12]:
            print({k: (round(v, 2) if isinstance(v, float) else v) for k, v in g.items()
                   if k not in ("share", "leader_share", "pre_sat")})


if __name__ == "__main__":
    main()
