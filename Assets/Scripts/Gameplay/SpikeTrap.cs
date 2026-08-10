using UnityEngine;

/// <summary>
/// 가시 함정 클래스 (Spike Trap).
/// 지면/벽/천장 정렬 Transform 회전 및 법선 방향 노크백 전달.
/// </summary>
public class SpikeTrap : HazardBase
{
    [Header("Spike Trap Settings")]
    [SerializeField] private Vector2 surfaceNormal = Vector2.up;
    [SerializeField] private bool autoAlignToSurface = true;

    public Vector2 SurfaceNormal => surfaceNormal;

    private void Awake()
    {
        hazardId = 1070; // ResourceData idx: 1070 (Hazard_SpikeTrap)
        damage = 15;
        knockbackForce = 9.0f;
        cooldownBetweenHits = 0.5f;
    }

    private void Start()
    {
        if (autoAlignToSurface)
        {
            surfaceNormal = transform.up;
        }
    }

    public void AlignToSurface(Vector2 normal)
    {
        surfaceNormal = normal.normalized;
        float angle = Mathf.Atan2(surfaceNormal.y, surfaceNormal.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    protected override Vector2 CalculateHitNormal(Collider2D col)
    {
        // ponytail: knockback always follows spike surface normal
        return surfaceNormal != Vector2.zero ? surfaceNormal : (Vector2)transform.up;
    }
}
