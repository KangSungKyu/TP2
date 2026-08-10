using UnityEngine;

/// <summary>
/// 둥근 톱날 함정 클래스 (Saw Blade Trap).
/// Z축 지속 회전(rotationSpeed) 및 Waypoint 이동(moveSpeed, PingPong/Loop) 제어 모터 구현.
/// </summary>
public class SawBladeTrap : HazardBase
{
    public enum MovementMode
    {
        PingPong = 0,
        Loop = 1
    }

    [Header("Saw Blade Trap Settings")]
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private bool enableMovement = false;
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float moveSpeed = 3.0f;
    [SerializeField] private MovementMode moveMode = MovementMode.PingPong;

    private int currentWaypointIndex = 0;
    private int moveDirection = 1;

    public float RotationSpeed => rotationSpeed;
    public bool EnableMovement => enableMovement;
    public float MoveSpeed => moveSpeed;

    private void Awake()
    {
        hazardId = 1071; // ResourceData idx: 1071 (Hazard_SawBladeTrap)
        damage = 20;
        knockbackForce = 11.0f;
        cooldownBetweenHits = 0.4f;
    }

    private void Update()
    {
        UpdateRotation();
        if (enableMovement)
        {
            UpdateMovement();
        }
    }

    private void UpdateRotation()
    {
        transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    private void UpdateMovement()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        Transform targetPoint = waypoints[currentWaypointIndex];
        if (targetPoint == null) return;

        transform.position = Vector3.MoveTowards(transform.position, targetPoint.position, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPoint.position) < 0.05f)
        {
            AdvanceWaypoint();
        }
    }

    private void AdvanceWaypoint()
    {
        if (moveMode == MovementMode.PingPong)
        {
            if (currentWaypointIndex >= waypoints.Length - 1)
            {
                moveDirection = -1;
            }
            else if (currentWaypointIndex <= 0)
            {
                moveDirection = 1;
            }
            currentWaypointIndex += moveDirection;
        }
        else if (moveMode == MovementMode.Loop)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }

    public void SetupWaypoints(Transform[] points, float speed = 3.0f, MovementMode mode = MovementMode.PingPong)
    {
        waypoints = points;
        moveSpeed = speed;
        moveMode = mode;
        enableMovement = waypoints != null && waypoints.Length >= 2;
        currentWaypointIndex = 0;
        moveDirection = 1;
    }
}
