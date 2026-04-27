using System.Collections.Generic;
using UnityEngine;

public static class CreativityGameData
{
    public class BlockShape
    {
        public string name;
        public int[][] cells; // [i][0]=row, [i][1]=col, 오프셋 기준 (0,0)
        public Color color;
    }

    public class GridShape
    {
        public string name;
        public int rows, cols;
        public HashSet<(int r, int c)> validCells;
    }

    public static readonly BlockShape[] Blocks;       // 전체 풀 (저장 복원 시 이름 검색용)
    public static readonly BlockShape[] Blocks2;      // 2칸 (30%)
    public static readonly BlockShape[] Blocks3;      // 3칸 (40%)
    public static readonly BlockShape[] Blocks4;      // 4칸 (30%)

    public static readonly GridShape[]  Grids;        // 전체 풀 (저장 복원 시 이름 검색용)
    public static readonly GridShape[]  Level1Grids;  // 16칸
    public static readonly GridShape[]  Level2Grids;  // 25~32칸
    public static readonly GridShape[]  Level3Grids;  // 35~37칸

    public static GridShape[] GetGridsForLevel(int level)
    {
        switch (level)
        {
            case 1:  return Level1Grids;
            case 2:  return Level2Grids;
            case 3:  return Level3Grids;
            default: return Level3Grids;
        }
    }

    // 30/40/30 확률로 2/3/4칸 풀에서 블록 1개 추첨
    public static BlockShape DrawRandomBlock()
    {
        float roll = Random.value;
        BlockShape[] pool =
            roll < 0.30f ? Blocks2 :
            roll < 0.70f ? Blocks3 :
                           Blocks4;
        return pool[Random.Range(0, pool.Length)];
    }

    static CreativityGameData()
    {
        var c1 = new Color(1f, 1f, 0.4f);          // 1칸 (legacy Dot용)
        var c2 = new Color(1f, 0.92f, 0.25f);      // 2칸 노랑
        var c3 = new Color(0.32f, 0.82f, 0.46f);   // 3칸 초록
        var c4 = new Color(1f, 0.55f, 0.65f);      // 4칸 분홍

        // ═══════════════════════════════════════════════════════════════════
        //   추첨 풀 (c7.png 기준)
        // ═══════════════════════════════════════════════════════════════════

        // ── 2칸 (노랑) ──────────────────────────────────────────────────────
        // X X        X
        //            X
        var bH2 = B("H2", c2, (0,0),(0,1));
        var bV2 = B("V2", c2, (0,0),(1,0));

        // ── 3칸 (초록) ──────────────────────────────────────────────────────
        // X X       X X       X .       . X       X       X X X
        // X .       . X       X X       X X       X
        //                                         X
        var bG_TR = B("G_TR", c3, (0,0),(0,1),(1,0));
        var bG_TL = B("G_TL", c3, (0,0),(0,1),(1,1));
        var bG_BR = B("G_BR", c3, (0,0),(1,0),(1,1));
        var bG_BL = B("G_BL", c3, (0,1),(1,0),(1,1));
        var bV3   = B("V3",   c3, (0,0),(1,0),(2,0));
        var bH3   = B("H3",   c3, (0,0),(0,1),(0,2));

        // ── 4칸 (분홍) ──────────────────────────────────────────────────────
        // 1) Sq    2x2
        // X X
        // X X
        var bSq = B("Sq", c4, (0,0),(0,1),(1,0),(1,1));

        // 2) L_R: vertical 3 + bottom-right foot
        // X .
        // X .
        // X X
        var bL_R = B("L_R", c4, (0,0),(1,0),(2,0),(2,1));

        // 3) J_L: vertical 3 + bottom-left foot
        // . X
        // . X
        // X X
        var bJ_L = B("J_L", c4, (0,1),(1,1),(2,0),(2,1));

        // 4) L_U: top 2 + right column down 2
        // X X
        // . X
        // . X
        var bL_U = B("L_U", c4, (0,0),(0,1),(1,1),(2,1));

        // 5) J_U: top 2 + left column down 2
        // X X
        // X .
        // X .
        var bJ_U = B("J_U", c4, (0,0),(0,1),(1,0),(2,0));

        // 6) T_U: top spike + 3-wide bottom
        // . X .
        // X X X
        var bT_U = B("T_U", c4, (0,1),(1,0),(1,1),(1,2));

        // 7) T_D: 3-wide top + bottom spike
        // X X X
        // . X .
        var bT_D = B("T_D", c4, (0,0),(0,1),(0,2),(1,1));

        // 8) V4: vertical 4
        // X
        // X
        // X
        // X
        var bV4 = B("V4", c4, (0,0),(1,0),(2,0),(3,0));

        // 9) H4: horizontal 4
        // X X X X
        var bH4 = B("H4", c4, (0,0),(0,1),(0,2),(0,3));

        // 10) S
        // . X X
        // X X .
        var bS = B("S", c4, (0,1),(0,2),(1,0),(1,1));

        // 11) Z
        // X X .
        // . X X
        var bZ = B("Z", c4, (0,0),(0,1),(1,1),(1,2));

        // 12) Z_V: vertical Z
        // . X
        // X X
        // X .
        var bZ_V = B("Z_V", c4, (0,1),(1,0),(1,1),(2,0));

        // ═══════════════════════════════════════════════════════════════════
        //   Legacy (저장 호환용 — 추첨 풀에서 제외)
        // ═══════════════════════════════════════════════════════════════════
        var bDot = B("Dot", c1, (0,0));
        var bL_D = B("L_D", c4, (0,0),(0,1),(0,2),(1,0));
        var bJ_D = B("J_D", c4, (0,0),(0,1),(0,2),(1,2));
        var bT_R = B("T_R", c4, (0,1),(1,0),(1,1),(2,1));
        var bT_L = B("T_L", c4, (0,0),(1,0),(1,1),(2,0));
        var bS_V = B("S_V", c4, (0,0),(1,0),(1,1),(2,1));

        Blocks2 = new[] { bH2, bV2 };
        Blocks3 = new[] { bG_TR, bG_TL, bG_BR, bG_BL, bV3, bH3 };
        Blocks4 = new[] { bSq, bL_R, bJ_L, bL_U, bJ_U, bT_U, bT_D, bV4, bH4, bS, bZ, bZ_V };

        Blocks = new[]
        {
            bH2, bV2,
            bG_TR, bG_TL, bG_BR, bG_BL, bV3, bH3,
            bSq, bL_R, bJ_L, bL_U, bJ_U, bT_U, bT_D, bV4, bH4, bS, bZ, bZ_V,
            // legacy
            bDot, bL_D, bJ_D, bT_R, bT_L, bS_V,
        };

        // ── 기존 그리드 (저장 복원 호환용으로 유지) ──────────────────────────
        var g3x3   = G("3x3",   3, 3, Full(3, 3));
        var g4x4A  = G("4x4-A", 4, 4, Full(4, 4, (0,3)));
        var g5x5TR = G("5x5-TR",5, 5, Full(5, 5, (0,3),(0,4),(1,3),(1,4)));
        var gPlus  = G("Plus",  5, 5, Full(5, 5, (0,0),(0,4),(4,0),(4,4)));
        var g7x7   = G("7x7",   7, 7, Full(7, 7));

        // ── Level 1: 16칸 ──────────────────────────────────────────────────
        var g4x4 = G("4x4", 4, 4, Full(4, 4));   // 16

        // ── Level 2: 25~32칸 ───────────────────────────────────────────────
        var g5x5 = G("5x5", 5, 5, Full(5, 5));   // 25

        // House5: 6×6 위 2칸 피크 = 32
        // . . X X . .
        // X X X X X X
        // X X X X X X
        // X X X X X X
        // X X X X X X
        // X X X X X X
        var gHouse5 = G("House5", 6, 6,
            Full(6, 6, (0,0),(0,1),(0,4),(0,5)));   // 36 - 4 = 32

        // Octa5: 5×6 네 모서리 1칸씩 컷 = 26
        // . X X X X .
        // X X X X X X
        // X X X X X X
        // X X X X X X
        // . X X X X .
        var gOcta5 = G("Octa5", 5, 6,
            Full(5, 6, (0,0),(0,5),(4,0),(4,5)));   // 30 - 4 = 26

        // Robot5: 머리·몸통·다리 + 우측 팔 돌출 = 26
        // X . . . X .
        // X X X X X .
        // X X X X X X
        // X X X X X X
        // X X X X X .
        // X . . . X .
        var gRobot5 = G("Robot5", 6, 6,
            Full(6, 6,
                (0,1),(0,2),(0,3),(0,5),
                (1,5),
                (4,5),
                (5,1),(5,2),(5,3),(5,5)));  // 36 - 10 = 26

        // ── Level 3: 35~37칸 ───────────────────────────────────────────────
        var g6x6 = G("6x6", 6, 6, Full(6, 6));   // 36

        // House6: 7×7 피크+꼬리 = 35
        // . . . X . . .
        // . X X X X X .
        // X X X X X X X
        // X X X X X X X
        // X X X X X X X
        // X X X X X X X
        // . . . X . . .
        var gHouse6 = G("House6", 7, 7,
            Full(7, 7,
                (0,0),(0,1),(0,2),(0,4),(0,5),(0,6),
                (1,0),(1,6),
                (6,0),(6,1),(6,2),(6,4),(6,5),(6,6)));   // 49 - 14 = 35

        // Octa6: 7×7 네 모서리 L자(3칸) 컷 = 37
        // . . X X X . .
        // . X X X X X .
        // X X X X X X X
        // X X X X X X X
        // X X X X X X X
        // . X X X X X .
        // . . X X X . .
        var gOcta6 = G("Octa6", 7, 7,
            Full(7, 7,
                (0,0),(0,1),(0,5),(0,6),
                (1,0),(1,6),
                (5,0),(5,6),
                (6,0),(6,1),(6,5),(6,6)));   // 49 - 12 = 37

        Level1Grids = new[] { g4x4 };
        Level2Grids = new[] { g5x5, gHouse5, gOcta5, gRobot5 };
        Level3Grids = new[] { g6x6, gHouse6, gOcta6 };

        Grids = new[]
        {
            g3x3, g4x4, g5x5, gPlus, g4x4A, g5x5TR, g7x7,
            gHouse5, gOcta5, gRobot5,
            g6x6, gHouse6, gOcta6,
        };
    }

    static BlockShape B(string name, Color color, params (int r, int c)[] cells)
    {
        var arr = new int[cells.Length][];
        for (int i = 0; i < cells.Length; i++)
            arr[i] = new[] { cells[i].r, cells[i].c };
        return new BlockShape { name = name, color = color, cells = arr };
    }

    static GridShape G(string name, int rows, int cols, HashSet<(int, int)> cells)
        => new GridShape { name = name, rows = rows, cols = cols, validCells = cells };

    static HashSet<(int, int)> Full(int rows, int cols, params (int, int)[] exclude)
    {
        var s = new HashSet<(int, int)>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                s.Add((r, c));
        foreach (var e in exclude) s.Remove(e);
        return s;
    }
}
