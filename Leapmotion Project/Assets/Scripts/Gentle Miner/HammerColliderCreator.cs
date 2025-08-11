//using UnityEngine;

///// <summary>
///// 망치 머리 부분에 정확한 타격 감지용 길쭉한 박스 콜라이더를 생성하는 클래스
///// </summary>
//public class HammerColliderCreator : MonoBehaviour
//{
//    [Header("설정")]
//    public Transform hammerHeadTransform; // 망치 머리(쇠 부분)의 Transform을 할당
//    public string hammerTag = "Hammer";   // 콜라이더에 할당할 태그 이름

//    void Start()
//    {
//        if (hammerHeadTransform == null)
//        {
//            Debug.LogError("HammerColliderCreator: 망치 머리 Transform이 할당되지 않았습니다!");
//            return;
//        }

//        // 1. 망치 머리 오브젝트 자체에 콜라이더가 있는지 확인하고 없으면 추가
//        BoxCollider collider = hammerHeadTransform.GetComponent<BoxCollider>();
//        if (collider == null)
//        {
//            collider = hammerHeadTransform.gameObject.AddComponent<BoxCollider>();
//        }

//        // 2. 콜라이더 설정 (길쭉한 형태로 변경)
//        collider.isTrigger = true;

//        // ⭐ 길쭉한 형태로 만들기 위해 크기를 설정합니다.
//        //    (x: 얇게, y: 얇게, z: 길게)
//        collider.size = new Vector3(0.01f, 0.01f, 0.1f);

//        // ⭐ 길쭉한 콜라이더의 중심을 망치 머리에서 Z축 방향으로 이동시킵니다.
//        //    이렇게 해야 콜라이더가 망치 머리 앞쪽으로 뻗어나갑니다.
//        collider.center = new Vector3(0, 0, 0.05f);

//        // 3. 콜라이더가 붙은 게임 오브젝트(망치 머리)에 해머 태그를 부여
//        hammerHeadTransform.gameObject.tag = hammerTag;

//        Debug.Log($"<color=blue>HammerColliderCreator:</color> 망치 머리({hammerHeadTransform.name})에 태그 '{hammerTag}'가 붙은 박스 콜라이더 생성 완료. Size: {collider.size}, Center: {collider.center}");
//    }
//}