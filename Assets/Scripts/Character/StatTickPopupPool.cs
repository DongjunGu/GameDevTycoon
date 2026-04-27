using System.Collections.Generic;
using UnityEngine;

public class StatTickPopupPool : MonoBehaviour
{
    public static StatTickPopupPool Instance { get; private set; }

    [Header("Pool Settings")]
    public int poolSize = 20;

    private readonly Queue<StatTickPopup> _pool = new();
    private GameObject _prefab;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        _prefab = Resources.Load<GameObject>("StatTickPopup");
        if (_prefab == null)
        {
            Debug.LogError("[StatTickPopupPool] Resources/StatTickPopup 프리팹을 찾을 수 없습니다.");
            return;
        }

        for (int i = 0; i < poolSize; i++)
            CreateAndEnqueue();
    }

    public StatTickPopup Get(Vector3 position)
    {
        var item = _pool.Count > 0 ? _pool.Dequeue() : CreateAndEnqueue();
        item.transform.position = position;
        item.gameObject.SetActive(true);
        return item;
    }

    public void Return(StatTickPopup item)
    {
        item.gameObject.SetActive(false);
        item.transform.SetParent(transform);
        _pool.Enqueue(item);
    }

    StatTickPopup CreateAndEnqueue()
    {
        var obj = Instantiate(_prefab, transform);
        obj.SetActive(false);
        var item = obj.GetComponent<StatTickPopup>();
        _pool.Enqueue(item);
        return item;
    }
}
