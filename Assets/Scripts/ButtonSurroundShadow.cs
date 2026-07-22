using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 버튼을 8방향(상하좌우 + 대각선)으로 감싸는 Shadow를 깔아 사방 전체(모서리 포함)에서 살짝
// 삐져나오는 그림자를 만든다. UnityEngine.UI.Shadow는 원본 도형을 오프셋만큼 복제해 색을
// 입히는 방식이라 블러는 없지만, 4방향(상하좌우)만 쓰면 사이 대각선 구간(특히 둥근 모서리)이
// 비어 보인다 — 그래서 8방향 전부 채운다.
//
// ⚠️ 이전 구현(자식 GameObject 8개, 각자 원본 스프라이트를 복제해 보여주고 그 위에 Shadow를 얹는 방식)은
// 두 가지 버그가 있었다 — 둘 다 "자식은 Unity 렌더 순서상 항상 부모(진짜 버튼)보다 위(앞)에 그려진다"는
// 사실 때문에 생김:
//  1) 복제본을 불투명하게 뒀더니, 진짜 버튼의 Button.Transition=SpriteSwap(Pressed/Selected 등) 변화가
//     스폰 시점에 얼어붙은 복제본에 항상 가려져서 하나도 안 보임.
//  2) 복제본을 투명하게 고쳐도(1차 수정), Shadow 이펙트 자체가 여전히 "자식"에서 그려지기 때문에
//     여전히 버튼 앞(위)에 그려짐 — "뒤에 은은히 깔리는 그림자"가 아니라 "버튼 앞을 덮는 얼룩"처럼 보임.
//
// UnityEngine.UI.Shadow는 BaseMeshEffect라서, 그 컴포넌트가 붙은 그래픽 자신의 메시 안에 오프셋+색만
// 입힌 복제 정점을 "그 그래픽 자신의 원본 정점보다 먼저(안쪽/아래)" 추가한다. 즉 버튼의 Image에 직접
// 여러 개를 붙이면, 버튼 자신의 현재 콘텐츠(어떤 스프라이트로 바뀌어 있든) 뒤에 정확히 그림자만 깔리고
// 앞을 가릴 일이 아예 없다. Shadow는 DisallowMultipleComponent가 아니라서 한 GameObject에 여러 개
// 붙일 수 있다.
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class ButtonSurroundShadow : MonoBehaviour
{
    public float distance = 5f;
    public Color color = new Color(0.1608f, 0.0235f, 0.0627f, 1f);

    const string OldRootName = "__SurroundShadow"; // 이전 버전(자식 복제 방식)의 잔재 정리용
    const float Diagonal = 0.70710678f; // 대각선 성분(1/sqrt(2)) — 대각선도 상하좌우와 같은 거리감이 되도록 정규화
    const int DirectionCount = 8;

    // OnEnable(구조 변경 허용) vs OnValidate(값만 갱신) — AddComponent/Destroy를 OnValidate 안에서
    // 실행하면 Unity가 "SendMessage cannot be called during Awake/CheckConsistency/OnValidate"
    // 경고를 뿌린다(OnDidAddComponent 등 내부 메시지가 그 타이밍에 금지되어 있음). 그래서 컴포넌트를
    // 새로 추가/제거하는 구조적 작업은 OnEnable에서만 하고, OnValidate는 이미 있는 Shadow들의 값만 갱신한다.
    void OnEnable() => Build(allowStructuralChanges: true);

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        Build(allowStructuralChanges: false);
    }
#endif

    void Build(bool allowStructuralChanges)
    {
        if (allowStructuralChanges)
        {
            // 이전 버전이 만들어둔 자식(버튼 앞을 가리던 원인)이 남아있으면 제거.
            var oldRoot = transform.Find(OldRootName);
            if (oldRoot != null)
            {
                if (Application.isPlaying) Destroy(oldRoot.gameObject);
                else                       DestroyImmediate(oldRoot.gameObject);
            }
        }

        float d = distance;
        float dd = distance * Diagonal;
        Vector2[] offsets =
        {
            new(0f, d),   new(0f, -d),
            new(d, 0f),   new(-d, 0f),
            new(dd, dd),  new(-dd, dd),
            new(dd, -dd), new(-dd, -dd),
        };

        // 이미 있는 Shadow 컴포넌트는 재사용(값만 갱신)하고, 모자란 만큼만 추가 — OnEnable이 반복
        // 호출돼도 매번 새로 쌓이지 않게 함.
        var shadows = GetComponents<Shadow>();
        if (shadows.Length < DirectionCount)
        {
            if (!allowStructuralChanges) return; // 값만 갱신 허용된 호출이면 개수가 안 맞아도 추가하지 않고 다음 OnEnable을 기다림
            var list = new List<Shadow>(shadows);
            while (list.Count < DirectionCount)
                list.Add(gameObject.AddComponent<Shadow>());
            shadows = list.ToArray();
        }

        for (int i = 0; i < DirectionCount; i++)
        {
            shadows[i].effectColor = color;
            shadows[i].effectDistance = offsets[i];
            shadows[i].useGraphicAlpha = true;
        }
    }
}
