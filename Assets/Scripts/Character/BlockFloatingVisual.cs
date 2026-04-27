using System.Collections;
using UnityEngine;

public class BlockFloatingVisual : MonoBehaviour
{
    const float CellSize   = 0.12f;
    const float CellGap    = 0.02f;
    const float BgPadding  = 0.05f;
    const float FloatSpeed = 0.8f;
    const float Duration   = 1.5f;

    private SpriteRenderer[] _cellRenderers;
    private SpriteRenderer   _bgRenderer;
    private Color _baseColor;

    public System.Action OnFinish;

    public static BlockFloatingVisual Spawn(Vector3 worldPos, int[][] shape, Color color, System.Action onFinish = null)
    {
        var go = new GameObject("BlockFloatingVisual");
        go.transform.position = worldPos;
        var bfv = go.AddComponent<BlockFloatingVisual>();
        bfv.OnFinish = onFinish;
        bfv.Play(shape, color);
        return bfv;
    }

    void Play(int[][] shape, Color color)
    {
        _baseColor = color;
        float step = CellSize + CellGap;

        int minR = int.MaxValue, maxR = int.MinValue;
        int minC = int.MaxValue, maxC = int.MinValue;
        foreach (var cell in shape)
        {
            if (cell[0] < minR) minR = cell[0];
            if (cell[0] > maxR) maxR = cell[0];
            if (cell[1] < minC) minC = cell[1];
            if (cell[1] > maxC) maxC = cell[1];
        }
        float centerR = (minR + maxR) * 0.5f;
        float centerC = (minC + maxC) * 0.5f;

        var sprite = GetWhiteSprite();

        // 흰색 배경 (전체 블록 바운딩박스 + 패딩)
        float bgW = (maxC - minC + 1) * step - CellGap + BgPadding * 2f;
        float bgH = (maxR - minR + 1) * step - CellGap + BgPadding * 2f;
        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(transform, false);
        bgGO.transform.localPosition = Vector3.zero;
        bgGO.transform.localScale    = new Vector3(bgW, bgH, 1f);
        _bgRenderer = bgGO.AddComponent<SpriteRenderer>();
        _bgRenderer.sprite       = sprite;
        _bgRenderer.color        = Color.white;
        _bgRenderer.sortingOrder = 9;

        // 컬러 셀
        _cellRenderers = new SpriteRenderer[shape.Length];
        for (int i = 0; i < shape.Length; i++)
        {
            var cellGO = new GameObject("C");
            cellGO.transform.SetParent(transform, false);
            cellGO.transform.localPosition = new Vector3(
                (shape[i][1] - centerC) * step,
                -(shape[i][0] - centerR) * step,
                0f
            );
            cellGO.transform.localScale = Vector3.one * CellSize;

            var sr = cellGO.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.color        = color;
            sr.sortingOrder = 10;
            _cellRenderers[i] = sr;
        }

        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;

        while (elapsed < Duration)
        {
            if (GameTimeManager.Instance == null || GameTimeManager.Instance.IsRunning)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / Duration;
                transform.position = startPos + Vector3.up * FloatSpeed * t;
            }
            yield return null;
        }

        InvokeFinish();
        Destroy(gameObject);
    }

    void InvokeFinish()
    {
        var cb = OnFinish;
        OnFinish = null;
        cb?.Invoke();
    }

    void OnDestroy() => InvokeFinish();

    static Sprite _whiteSprite;
    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _whiteSprite;
    }
}
