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

    public static readonly BlockShape[] Blocks;
    public static readonly GridShape[]  Grids;

    static CreativityGameData()
    {
        Blocks = new[]
        {
            // ── 1칸 ──
            B("Dot",  new Color(1f, 1f, 0.4f),
                (0,0)),

            // ── 2칸 (노랑) ──
            B("H2", new Color(1f, 0.92f, 0.25f),
                (0,0),(0,1)),
            B("V2", new Color(1f, 0.92f, 0.25f),
                (0,0),(1,0)),

            // ── 3칸 (초록) ──
            B("G_BR", new Color(0.32f, 0.82f, 0.46f),
                (0,0),(1,0),(1,1)),   // ┘ 모양
            B("G_BL", new Color(0.32f, 0.82f, 0.46f),
                (0,1),(1,0),(1,1)),   // └ 모양
            B("G_TR", new Color(0.32f, 0.82f, 0.46f),
                (0,0),(0,1),(1,0)),   // ┐ 모양
            B("G_TL", new Color(0.32f, 0.82f, 0.46f),
                (0,0),(0,1),(1,1)),   // ┌ 모양
            B("V3",   new Color(0.32f, 0.82f, 0.46f),
                (0,0),(1,0),(2,0)),
            B("H3",   new Color(0.32f, 0.82f, 0.46f),
                (0,0),(0,1),(0,2)),

            // ── 4칸 (분홍) ──
            B("Sq",  new Color(1f, 0.55f, 0.65f), (0,0),(0,1),(1,0),(1,1)),
            B("L_R", new Color(1f, 0.55f, 0.65f), (0,0),(1,0),(2,0),(2,1)),
            B("J_L", new Color(1f, 0.55f, 0.65f), (0,1),(1,1),(2,0),(2,1)),
            B("L_D", new Color(1f, 0.55f, 0.65f), (0,0),(0,1),(0,2),(1,0)),
            B("J_D", new Color(1f, 0.55f, 0.65f), (0,0),(0,1),(0,2),(1,2)),
            B("T_D", new Color(1f, 0.55f, 0.65f), (0,0),(0,1),(0,2),(1,1)),
            B("T_R", new Color(1f, 0.55f, 0.65f), (0,1),(1,0),(1,1),(2,1)),
            B("T_L", new Color(1f, 0.55f, 0.65f), (0,0),(1,0),(1,1),(2,0)),
            B("T_U", new Color(1f, 0.55f, 0.65f), (0,1),(1,0),(1,1),(1,2)),
            B("S",   new Color(1f, 0.55f, 0.65f), (0,1),(0,2),(1,0),(1,1)),
            B("Z",   new Color(1f, 0.55f, 0.65f), (0,0),(0,1),(1,1),(1,2)),
            B("S_V", new Color(1f, 0.55f, 0.65f), (0,0),(1,0),(1,1),(2,1)),
            B("Z_V", new Color(1f, 0.55f, 0.65f), (0,1),(1,0),(1,1),(2,0)),
            B("V4",  new Color(1f, 0.55f, 0.65f), (0,0),(1,0),(2,0),(3,0)),
            B("H4",  new Color(1f, 0.55f, 0.65f), (0,0),(0,1),(0,2),(0,3)),
        };

        Grids = new[]
        {
            G("3x3",   3, 3, Full(3, 3)),
            G("4x4",   4, 4, Full(4, 4)),
            G("5x5",   5, 5, Full(5, 5)),
            // 5×5 플러스 (꼭짓점 4개 제거)
            G("Plus",  5, 5, Full(5, 5, (0,0),(0,4),(4,0),(4,4))),
            // 4×4 오른쪽 상단 1칸 제거
            G("4x4-A", 4, 4, Full(4, 4, (0,3))),
            // 5×5 오른쪽 상단 2×2 제거
            G("5x5-TR",5, 5, Full(5, 5, (0,3),(0,4),(1,3),(1,4))),
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
