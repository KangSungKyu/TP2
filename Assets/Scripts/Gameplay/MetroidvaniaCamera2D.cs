using UnityEngine;

/// <summary>
/// 메트로배니아 2D 카메라 컴포넌트.
/// 플레이어 추적, 바라보는 방향 LookAhead 오프셋, 2D 룸 바운더리(Room Bounds) 가두기를 전담합니다.
/// </summary>
public class MetroidvaniaCamera2D : MonoBehaviour
{
    [Header("Target Tracking")]
    public Transform Target;
    public Vector3 Offset = new Vector3(0f, 1.2f, -10f);
    public float SmoothTime = 0.15f;
    public float LookAheadDistance = 2.0f;

    [Header("Stage Bounds")]
    public bool UseBounds = true;
    public Vector2 MinBounds = new Vector2(-29f, -1f);
    public Vector2 MaxBounds = new Vector2(29f, 17f);

    private Vector3 currentVelocity;
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (Target == null)
        {
            if (Player.Instance != null)
            {
                Target = Player.Instance.transform;
            }
            else return;
        }

        float lookAheadX = 0f;
        var spriteRend = Target.GetComponent<SpriteRenderer>();
        if (spriteRend != null)
        {
            lookAheadX = (spriteRend.flipX ? -1f : 1f) * LookAheadDistance;
        }

        Vector3 targetPos = Target.position + Offset + new Vector3(lookAheadX, 0f, 0f);

        Vector3 newPos = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, SmoothTime);

        if (UseBounds && cam != null && cam.orthographic)
        {
            float vertExtent = cam.orthographicSize;
            float horizExtent = vertExtent * cam.aspect;

            float minX = MinBounds.x + horizExtent;
            float maxX = MaxBounds.x - horizExtent;
            float minY = MinBounds.y + vertExtent;
            float maxY = MaxBounds.y - vertExtent;

            if (minX < maxX) newPos.x = Mathf.Clamp(newPos.x, minX, maxX);
            else newPos.x = (MinBounds.x + MaxBounds.x) * 0.5f;

            if (minY < maxY) newPos.y = Mathf.Clamp(newPos.y, minY, maxY);
            else newPos.y = (MinBounds.y + MaxBounds.y) * 0.5f;
        }

        newPos.z = Offset.z;
        transform.position = newPos;
    }

    public void SetBounds(Vector2 min, Vector2 max)
    {
        MinBounds = min;
        MaxBounds = max;
        UseBounds = true;
    }
}
