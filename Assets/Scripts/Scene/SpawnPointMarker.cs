using UnityEngine;

public enum SpawnType
{
    None,    // 스폰을 하지 않음 (스킵)
    Player,  // 플레이어 스폰
    Monster, // 일반 몬스터 스폰
    Boss     // 보스 몬스터 스폰
}

/// <summary>
/// 룸 청크 내 유닛 스폰 위치 및 유닛 타입을 지정하는 스폰 마커 컴포넌트.
/// 유니티 에디터 Scene 뷰에서 Gizmo 아이콘으로 위치를 한눈에 식별하고 조정할 수 있습니다.
/// </summary>
public class SpawnPointMarker : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("체크 해제 시 해당 마커에서의 유닛 생성을 완전히 하지 않고 스킵합니다.")]
    public bool EnableSpawn = true;

    public SpawnType Type = SpawnType.Monster;

    [Tooltip("지정할 몬스터 ID (비어있거나 0이면 스테이지 데이터 테이블에서 무작위 추첨)")]
    public uint MonsterId;

    private void OnDrawGizmos()
    {
        if (!this.EnableSpawn || this.Type == SpawnType.None)
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f); // 회색 X 표시
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.25f, new Vector3(0.3f, 0.3f, 0.3f));
            return;
        }
        switch (this.Type)
        {
            case SpawnType.Player:
                Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.85f); // 청색
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
                Gizmos.DrawCube(transform.position + Vector3.up * 0.5f, new Vector3(0.4f, 1.0f, 0.4f));
                break;

            case SpawnType.Monster:
                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.85f); // 적색
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
                Gizmos.DrawCube(transform.position + Vector3.up * 0.5f, new Vector3(0.5f, 1.0f, 0.5f));
                break;

            case SpawnType.Boss:
                Gizmos.color = new Color(0.7f, 0.2f, 0.9f, 0.9f); // 자색 대형
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.0f, 1.0f);
                Gizmos.DrawCube(transform.position + Vector3.up * 1.0f, new Vector3(1.2f, 2.0f, 1.2f));
                break;
        }
    }
}
