using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// CreativityBlockSpriteConfig 커스텀 인스펙터.
// 창의성 미니게임 블록 20종을 모양 미리보기(칸 그리드)와 함께 나열하고,
// 각 모양 옆에서 셀 스프라이트를 직접 드래그해 넣을 수 있게 한다.
[CustomEditor(typeof(CreativityBlockSpriteConfig))]
public class CreativityBlockSpriteConfigEditor : Editor
{
    const float CellPx    = 14f;
    const float SlotPx    = 48f;
    const float PreviewPx = 40f;

    SerializedProperty _entriesProp;

    void OnEnable()
    {
        _entriesProp = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "블록 모양별로 셀에 적용할 스프라이트를 지정합니다. 스프라이트를 2개 이상 넣으면 " +
            "CreativityGameBlockUI 가 뱀모양 경로(머리/몸통/꼬리)를 자동 판별해 배정합니다. " +
            "1개만 넣으면 모든 셀에 동일하게 적용됩니다.\n" +
            "회전(도)은 자동보정 없이 입력한 값이 그대로 셀에 적용됩니다. " +
            "↺/↻ 로 90도씩, 필드에 직접 입력해 미세조정할 수 있고 미리보기가 그 값 그대로 회전됩니다.",
            MessageType.Info);

        if (GUILayout.Button("블록 정의와 동기화 (새로 추가된 블록 반영)"))
            SyncEntries();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            string label = Application.isPlaying
                ? "게임에 즉시 적용 (열려있는 미니게임의 블록 다시 그리기)"
                : "게임에 즉시 적용 — Play 모드에서만 가능";
            if (GUILayout.Button(label))
                ApplyToRunningGame();
        }

        EditorGUILayout.Space(8);

        DrawGroup("2칸 블록", CreativityGameData.Blocks2);
        DrawGroup("3칸 블록", CreativityGameData.Blocks3);
        DrawGroup("4칸 블록", CreativityGameData.Blocks4);
        DrawGroup("레거시 (저장 호환용, 추첨 풀 제외)", LegacyBlocks());

        serializedObject.ApplyModifiedProperties();
    }

    static CreativityGameData.BlockShape[] LegacyBlocks()
    {
        var known = new HashSet<string>();
        foreach (var b in CreativityGameData.Blocks2) known.Add(b.name);
        foreach (var b in CreativityGameData.Blocks3) known.Add(b.name);
        foreach (var b in CreativityGameData.Blocks4) known.Add(b.name);
        return CreativityGameData.Blocks.Where(b => !known.Contains(b.name)).ToArray();
    }

    void DrawGroup(string title, CreativityGameData.BlockShape[] shapes)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        foreach (var shape in shapes)
            DrawEntry(shape);
        EditorGUILayout.Space(6);
    }

    void DrawEntry(CreativityGameData.BlockShape shape)
    {
        int idx = FindEntryIndex(shape.name);

        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label(shape.name, GUILayout.Width(56));
        DrawShapePreview(shape.cells, shape.color);
        GUILayout.Space(12);

        if (idx < 0)
        {
            EditorGUILayout.HelpBox("미등록 — 위 [블록 정의와 동기화] 버튼을 눌러주세요.", MessageType.Warning);
            EditorGUILayout.EndHorizontal();
            return;
        }

        var entryProp    = _entriesProp.GetArrayElementAtIndex(idx);
        var spritesProp  = entryProp.FindPropertyRelative("cellSprites");
        var rotationsProp = entryProp.FindPropertyRelative("cellRotations");
        DrawSpriteSlots(spritesProp, rotationsProp);

        EditorGUILayout.EndHorizontal();
    }

    int FindEntryIndex(string blockName)
    {
        for (int i = 0; i < _entriesProp.arraySize; i++)
        {
            var e = _entriesProp.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("blockName").stringValue == blockName) return i;
        }
        return -1;
    }

    static void DrawSpriteSlots(SerializedProperty spritesProp, SerializedProperty rotationsProp)
    {
        SyncArraySize(rotationsProp, spritesProp.arraySize);

        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < spritesProp.arraySize; i++)
        {
            var spriteEl = spritesProp.GetArrayElementAtIndex(i);
            var rotEl    = rotationsProp.GetArrayElementAtIndex(i);
            var sprite   = spriteEl.objectReferenceValue as Sprite;

            EditorGUILayout.BeginVertical(GUILayout.Width(SlotPx));

            var previewRect = GUILayoutUtility.GetRect(SlotPx, PreviewPx, GUILayout.Width(SlotPx), GUILayout.Height(PreviewPx));
            DrawRotatedPreview(previewRect, sprite, rotEl.floatValue);

            spriteEl.objectReferenceValue = EditorGUILayout.ObjectField(
                sprite, typeof(Sprite), false, GUILayout.Width(SlotPx), GUILayout.Height(18));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("↺", GUILayout.Width(16))) rotEl.floatValue = NormalizeAngle(rotEl.floatValue - 90f);
            rotEl.floatValue = NormalizeAngle(EditorGUILayout.FloatField(rotEl.floatValue, GUILayout.Width(SlotPx - 34)));
            if (GUILayout.Button("↻", GUILayout.Width(16))) rotEl.floatValue = NormalizeAngle(rotEl.floatValue + 90f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 스프라이트 슬롯", GUILayout.Width(110)))
        {
            spritesProp.arraySize++;
            SyncArraySize(rotationsProp, spritesProp.arraySize);
        }
        if (spritesProp.arraySize > 0 && GUILayout.Button("- 마지막 제거", GUILayout.Width(90)))
        {
            spritesProp.arraySize--;
            SyncArraySize(rotationsProp, spritesProp.arraySize);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    static void SyncArraySize(SerializedProperty prop, int size)
    {
        if (prop.arraySize != size) prop.arraySize = size;
    }

    static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a < 0f) a += 360f;
        return a;
    }

    // 회전 미리보기: 실제 게임에서 적용될 각도로 스프라이트를 회전해 그린다 (드래그&드롭은 아래 ObjectField 담당).
    // ⚠️ IMGUI 의 GUIUtility.RotateAroundPivot 은 스크린 좌표계(Y 아래로 증가) 기준이라, 실제 게임의
    // RectTransform.localEulerAngles.z(Y 위로 증가, 표준 반시계 방향) 와 회전 방향이 반대로 보인다.
    // 그래서 부호를 뒤집어서(-rotationDeg) 그려야 게임과 같은 방향으로 보인다.
    static void DrawRotatedPreview(Rect rect, Sprite sprite, float rotationDeg)
    {
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.06f));
        if (sprite == null || Event.current.type != EventType.Repaint) return;

        var tex = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
        if (tex == null) return;

        var prevMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(-rotationDeg, rect.center);
        GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
        GUI.matrix = prevMatrix;
    }

    // shape.cells 를 바운딩 박스 기준으로 정렬해 채워진/빈 칸을 작은 격자로 그린다.
    static void DrawShapePreview(int[][] cells, Color color)
    {
        int minR = int.MaxValue, maxR = int.MinValue, minC = int.MaxValue, maxC = int.MinValue;
        foreach (var c in cells)
        {
            minR = Mathf.Min(minR, c[0]); maxR = Mathf.Max(maxR, c[0]);
            minC = Mathf.Min(minC, c[1]); maxC = Mathf.Max(maxC, c[1]);
        }
        int rows = maxR - minR + 1, cols = maxC - minC + 1;

        var occupied = new HashSet<(int, int)>();
        foreach (var c in cells) occupied.Add((c[0] - minR, c[1] - minC));

        var rect = GUILayoutUtility.GetRect(cols * CellPx, rows * CellPx,
            GUILayout.Width(cols * CellPx), GUILayout.Height(rows * CellPx));

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var cellRect = new Rect(rect.x + c * CellPx, rect.y + r * CellPx, CellPx - 1, CellPx - 1);
            bool on = occupied.Contains((r, c));
            EditorGUI.DrawRect(cellRect, on ? color : new Color(0f, 0f, 0f, 0.08f));
        }
    }

    // Play 모드에서 인스펙터 수정 내용을 즉시 게임에 반영 — 이미 스폰된 블록은 Init() 시점에만 스프라이트가
    // 고정되므로, 패널을 껐다 켜지 않고도 바로 확인할 수 있게 CreativityGameUI.ReapplyBlockSprites 를 호출.
    void ApplyToRunningGame()
    {
        var ui = CreativityGameUI.Instance;
        if (ui == null)
        {
            Debug.LogWarning("[CreativityBlockSpriteConfigEditor] CreativityGameUI.Instance 를 찾을 수 없음 — 씬에 미니게임 패널이 있는지 확인하세요.");
            return;
        }
        ui.ReapplyBlockSprites();
    }

    void SyncEntries()
    {
        var known = new HashSet<string>();
        for (int i = 0; i < _entriesProp.arraySize; i++)
            known.Add(_entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("blockName").stringValue);

        foreach (var shape in CreativityGameData.Blocks)
        {
            if (known.Contains(shape.name)) continue;
            int newIdx = _entriesProp.arraySize;
            _entriesProp.arraySize++;
            var e = _entriesProp.GetArrayElementAtIndex(newIdx);
            e.FindPropertyRelative("blockName").stringValue = shape.name;
            e.FindPropertyRelative("cellSprites").arraySize = 0;
            e.FindPropertyRelative("cellRotations").arraySize = 0;
        }
        serializedObject.ApplyModifiedProperties();
    }
}
