using UnityEngine;

[ExecuteAlways]
public class IsometricSorter : MonoBehaviour
{
    public float sortMultiplier = 1000f;
    public float yOffset = 0f;

    private SpriteRenderer _sr;
    private float _lastY;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        UpdateOrder();
    }

    void Update()
    {
        if (_sr == null) return;
        _sr.sortingOrder = Mathf.RoundToInt(-(transform.position.y + yOffset) * sortMultiplier);
    }

    void UpdateOrder()
    {
        _lastY = transform.position.y;
        _sr.sortingOrder = Mathf.RoundToInt(-transform.position.y * sortMultiplier);
    }
}