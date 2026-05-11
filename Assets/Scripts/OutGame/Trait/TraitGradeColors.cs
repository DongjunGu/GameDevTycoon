using UnityEngine;

// 특성 등급별 배경/텍스트 색상 팔레트 (S=황금, A=보라, B=청록, C=회색)
// UI 컴포넌트들이 공유하도록 한곳에 모음
public static class TraitGradeColors
{
    public static readonly Color C = new(0.92f, 0.92f, 0.92f);
    public static readonly Color B = new(0.55f, 0.85f, 0.95f);
    public static readonly Color A = new(0.65f, 0.40f, 0.90f);
    public static readonly Color S = new(1.00f, 0.85f, 0.30f);

    public static Color Get(TraitGrade g) => g switch
    {
        TraitGrade.S => S,
        TraitGrade.A => A,
        TraitGrade.B => B,
        _            => C,
    };
}
