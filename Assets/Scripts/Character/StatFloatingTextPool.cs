using System.Collections.Generic;
using UnityEngine;

public class StatFloatingTextPool : MonoBehaviour
{
    public static StatFloatingTextPool Instance { get; private set; }

    [Header("Pool Settings")]
    public int poolSize = 20;

    private readonly Queue<StatFloatingText> _pool = new();
    private GameObject _prefab;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        _prefab = Resources.Load<GameObject>("StatFloatingText");
        if (_prefab == null)
        {
            Debug.LogError("[StatFloatingTextPool] Resources/StatFloatingText 프리팹을 찾을 수 없습니다.");
            return;
        }

        for (int i = 0; i < poolSize; i++)
            CreateAndEnqueue();
    }

    public StatFloatingText Get(Vector3 position)
    {
        var item = _pool.Count > 0 ? _pool.Dequeue() : CreateAndEnqueue();
        item.transform.position = position;
        item.gameObject.SetActive(true);
        return item;
    }

    public void Return(StatFloatingText item)
    {
        item.gameObject.SetActive(false);
        item.transform.SetParent(transform);
        _pool.Enqueue(item);
    }

    StatFloatingText CreateAndEnqueue()
    {
        var obj = Instantiate(_prefab, transform);
        obj.SetActive(false);
        var item = obj.GetComponent<StatFloatingText>();
        _pool.Enqueue(item);
        return item;
    }
}
