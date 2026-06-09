using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        Chase,
        Attack
    }

    [Header("Other")]
    public string enemyName = "";
    public TMP_Text stateText;
    public bool destroyingOnAttack = false;

    [Header("Refs")]
    public TeamMember teamMember;

    [Header("State")]
    public EnemyState currentState = EnemyState.Idle;

    [Header("Detection")]
    public float detectionRadius = 8f;
    public float forgetTargetRadius = 20f;
    public float attackRadius = 1.5f;
    public float scanInterval = 0.2f;
    public LayerMask detectionMask;
    public LayerMask obstacleMask;

    [Header("Sight")]
    public float eyeHeight = 0.5f;
    public float targetEyeHeight = 0.5f;

    [Header("Pathfinding")]
    public float pathRefreshInterval = 0.35f;
    public float waypointReachDistance = 0.55f;
    public float waypointAdvanceLookahead = 1.2f;
    public float waypointSlowdownDistance = 1.4f;

    [Header("Move")]
    public float moveAcceleration = 22f;
    public float maxSpeed = 7f;
    public float steeringResponsiveness = 8f;
    public float idleBrake = 5f;

    [Header("Hover")]
    public float hoverHeight = 2f;
    public float hoverForce = 80f;
    public float hoverDamping = 8f;
    public float groundedDrag = 2f;
    public float airDrag = 0.2f;
    public LayerMask groundMask;

    private Rigidbody rb;
    private Transform currentTarget;
    private float scanTimer;
    private float pathTimer;

    private List<Vector3> currentPath = new List<Vector3>();
    private int currentWaypointIndex = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        if (teamMember == null)
            teamMember = GetComponent<TeamMember>();

        if (enemyName == "")
            enemyName = gameObject.name;

        if (!stateText)
            stateText = GetComponentInChildren<TMP_Text>();
    }

    void FixedUpdate()
    {
        HandleHover();
        UpdateStateMachine();

        switch (currentState)
        {
            case EnemyState.Idle:
                ApplyIdleBraking();
                TryFindTarget();
                stateText.text = "Idle...";
                break;

            case EnemyState.Chase:
                UpdatePathToTarget();
                FollowPathSmooth();
                stateText.text = "Chasing ->";
                break;

            case EnemyState.Attack:
                ApplyIdleBraking();
                stateText.text = "ATTACK!!!";
                if (currentTarget != null)
                {
                    if (destroyingOnAttack)
                        Destroy(gameObject);
                    else
                    {
                        if (currentTarget.TryGetComponent<EnemyController>(out EnemyController targetEnemy))
                            Debug.Log(enemyName + " attacked " + targetEnemy.enemyName);
                        else
                            Debug.Log(enemyName + " attacked YOU\nYOU: -1hp :)");
                    }
                }
                break;
        }
    }

    void UpdateStateMachine()
    {
        if (currentTarget == null)
        {
            currentState = EnemyState.Idle;
            ClearPath();
            return;
        }

        float sqrDistance = (currentTarget.position - transform.position).sqrMagnitude;

        if (sqrDistance > forgetTargetRadius * forgetTargetRadius)
        {
            currentTarget = null;
            currentState = EnemyState.Idle;
            ClearPath();
            return;
        }

        if (sqrDistance <= attackRadius * attackRadius)
            currentState = EnemyState.Attack;
        else
            currentState = EnemyState.Chase;
    }

    void TryFindTarget()
    {
        scanTimer -= Time.fixedDeltaTime;
        if (scanTimer > 0f) return;

        scanTimer = scanInterval;

        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, detectionMask);

        Transform bestTarget = null;
        float bestSqrDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform) continue;

            TeamMember other = hit.GetComponent<TeamMember>();
            if (other == null || teamMember == null) continue;
            if (other.team == EnemyTeam.Neutral) continue;
            if (other.team == teamMember.team) continue;
            if (!HasLineOfSight(hit.transform)) continue;

            float sqrDistance = (hit.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestTarget = hit.transform;
            }
        }

        if (bestTarget != null)
        {
            currentTarget = bestTarget;
            currentState = EnemyState.Chase;
            RefreshPath(force: true);
        }
    }

    void UpdatePathToTarget()
    {
        if (currentTarget == null)
            return;

        pathTimer -= Time.fixedDeltaTime;
        if (pathTimer > 0f)
            return;

        pathTimer = pathRefreshInterval;
        RefreshPath(force: false);
    }

    void RefreshPath(bool force)
    {
        if (AStarPathfinder.Instance == null || currentTarget == null)
            return;

        List<Vector3> newPath = new();
        if (TryGetComponent<SphereCollider>(out SphereCollider sphereCol))
            newPath = AStarPathfinder.Instance.FindPath(transform.position, currentTarget.position, sphereCol.radius);
        else if (TryGetComponent<CapsuleCollider>(out CapsuleCollider capsuleCol))
            newPath = AStarPathfinder.Instance.FindPath(transform.position, currentTarget.position, capsuleCol.radius);

        if (newPath == null || newPath.Count == 0)
            return;

        if (force || ShouldReplacePath(newPath))
        {
            currentPath = newPath;
            currentWaypointIndex = GetBestStartingWaypointIndex(currentPath);
        }
    }

    bool ShouldReplacePath(List<Vector3> newPath)
    {
        if (currentPath == null || currentPath.Count == 0)
            return true;

        Vector3 currentEnd = currentPath[currentPath.Count - 1];
        Vector3 newEnd = newPath[newPath.Count - 1];

        if ((currentEnd - newEnd).sqrMagnitude > 1f)
            return true;

        if (currentWaypointIndex >= currentPath.Count)
            return true;

        return false;
    }

    int GetBestStartingWaypointIndex(List<Vector3> path)
    {
        int bestIndex = 0;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < path.Count; i++)
        {
            float sqr = (path[i] - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    void FollowPathSmooth()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            ApplyIdleBraking();
            return;
        }

        AdvanceWaypointIfNeeded();

        if (currentWaypointIndex >= currentPath.Count)
        {
            ApplyIdleBraking();
            return;
        }

        Vector3 steeringTarget = GetSteeringTarget();
        Vector3 toTarget = steeringTarget - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance < 0.05f)
        {
            ApplyIdleBraking();
            return;
        }

        Vector3 desiredDir = toTarget / distance;

        Vector3 wallAvoid = GetCornerAvoidanceVector();
        Vector3 finalDir = (desiredDir + wallAvoid).normalized;

        float speedFactor = 1f;
        if (distance < waypointSlowdownDistance)
            speedFactor = Mathf.Clamp01(distance / waypointSlowdownDistance);

        Vector3 desiredVelocity = finalDir * (maxSpeed * speedFactor);
        Vector3 currentFlatVelocity = GetFlatVelocity();

        Vector3 velocityDelta = desiredVelocity - currentFlatVelocity;
        Vector3 accel = Vector3.ClampMagnitude(velocityDelta * steeringResponsiveness, moveAcceleration);

        rb.AddForce(accel, ForceMode.Acceleration);

        Vector3 flat = GetFlatVelocity();
        flat = Vector3.ClampMagnitude(flat, maxSpeed);
        rb.linearVelocity = new Vector3(flat.x, rb.linearVelocity.y, flat.z);
    }

    Vector3 GetSteeringTarget()
    {
        Vector3 current = currentPath[currentWaypointIndex];

        if (currentWaypointIndex == 0)
            return current;

        Vector3 prev = currentPath[currentWaypointIndex - 1];
        Vector3 segment = current - prev;
        segment.y = 0f;

        float segmentLength = segment.magnitude;
        if (segmentLength < 0.001f)
            return current;

        Vector3 dir = segment / segmentLength;
        Vector3 projected = ProjectPointOnSegment(prev, current, transform.position);

        float lookAhead = Mathf.Min(waypointAdvanceLookahead, segmentLength);
        Vector3 target = projected + dir * lookAhead;

        float distToCurrent = Vector3.Distance(projected, current);
        if (distToCurrent < lookAhead)
            target = current;

        target.y = transform.position.y;
        return target;
    }

    Vector3 ProjectPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ap = p - a;
        Vector3 ab = b - a;

        float abSqr = ab.sqrMagnitude;
        if (abSqr <= 0.0001f)
            return a;

        float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / abSqr);
        return a + ab * t;
    }

    Vector3 GetCornerAvoidanceVector()
    {
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        Vector3 flatVel = GetFlatVelocity();

        Vector3 forwardDir = flatVel.sqrMagnitude > 0.04f
            ? flatVel.normalized
            : transform.forward;

        forwardDir.y = 0f;

        if (forwardDir.sqrMagnitude < 0.001f)
            return Vector3.zero;

        Vector3 left = Quaternion.Euler(0f, -35f, 0f) * forwardDir;
        Vector3 right = Quaternion.Euler(0f, 35f, 0f) * forwardDir;

        bool hitFront = Physics.Raycast(origin, forwardDir, out RaycastHit frontHit, 0.9f, obstacleMask);
        bool hitLeft = Physics.Raycast(origin, left, out RaycastHit leftHit, 0.75f, obstacleMask);
        bool hitRight = Physics.Raycast(origin, right, out RaycastHit rightHit, 0.75f, obstacleMask);

        if (!hitFront && !hitLeft && !hitRight)
            return Vector3.zero;

        Vector3 avoid = Vector3.zero;

        if (hitFront)
            avoid += Vector3.ProjectOnPlane(-frontHit.normal, Vector3.up).normalized * 1.2f;

        if (hitLeft)
            avoid += Vector3.ProjectOnPlane(leftHit.normal, Vector3.up).normalized * 0.8f;

        if (hitRight)
            avoid += Vector3.ProjectOnPlane(rightHit.normal, Vector3.up).normalized * 0.8f;

        avoid.y = 0f;
        return avoid.normalized;
    }

    void AdvanceWaypointIfNeeded()
    {
        while (currentWaypointIndex < currentPath.Count)
        {
            Vector3 waypoint = currentPath[currentWaypointIndex];
            Vector3 toWaypoint = waypoint - transform.position;
            toWaypoint.y = 0f;

            float dist = toWaypoint.magnitude;
            if (dist <= waypointReachDistance)
            {
                currentWaypointIndex++;
                continue;
            }

            Vector3 flatVel = GetFlatVelocity();
            if (flatVel.sqrMagnitude > 0.04f)
            {
                Vector3 velDir = flatVel.normalized;
                Vector3 dirToWaypoint = toWaypoint.normalized;

                float dot = Vector3.Dot(velDir, dirToWaypoint);

                if (dot < -0.15f)
                {
                    currentWaypointIndex++;
                    continue;
                }
            }

            if (currentWaypointIndex + 1 < currentPath.Count)
            {
                Vector3 next = currentPath[currentWaypointIndex + 1];
                Vector3 toNext = next - transform.position;
                toNext.y = 0f;

                if (toNext.magnitude < waypointAdvanceLookahead)
                {
                    currentWaypointIndex++;
                    continue;
                }
            }

            break;
        }
    }

    Vector3 GetFlatVelocity()
    {
        return new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
    }

    void ApplyIdleBraking()
    {
        Vector3 flat = GetFlatVelocity();
        flat = Vector3.Lerp(flat, Vector3.zero, idleBrake * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(flat.x, rb.linearVelocity.y, flat.z);
    }

    void HandleHover()
    {
        bool grounded = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, hoverHeight + 1f, groundMask);

        if (grounded)
        {
            float heightError = hoverHeight - hit.distance;
            float upwardSpeed = Vector3.Dot(rb.linearVelocity, Vector3.up);
            float lift = (heightError * hoverForce) - (upwardSpeed * hoverDamping);

            rb.AddForce(Vector3.up * lift, ForceMode.Acceleration);
            rb.linearDamping = groundedDrag;
        }
        else
        {
            rb.linearDamping = airDrag;
        }
    }

    bool HasLineOfSight(Transform target)
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPoint = target.position + Vector3.up * targetEyeHeight;
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
            return true;

        direction /= distance;
        LayerMask combinedMask = detectionMask | obstacleMask;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, combinedMask))
            return hit.transform == target || hit.transform.IsChildOf(target);

        return false;
    }

    void ClearPath()
    {
        currentPath.Clear();
        currentWaypointIndex = 0;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (currentTarget == null) return;
        if (collision.transform != currentTarget) return;

        TeamMember other = collision.gameObject.GetComponent<TeamMember>();
        if (other == null || teamMember == null) return;
        if (other.team == EnemyTeam.Neutral) return;
        if (other.team == teamMember.team) return;

        // Destroy(gameObject);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(transform.position.x, transform.position.y - 5f, transform.position.z), new Vector3(transform.position.x, transform.position.y + 5f, transform.position.z));

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, forgetTargetRadius);

        if (currentPath != null && currentPath.Count > 0)
        {
            Gizmos.color = Color.cyan;

            Vector3 prev = transform.position;
            for (int i = currentWaypointIndex; i < currentPath.Count; i++)
            {
                Gizmos.DrawLine(prev, currentPath[i]);
                Gizmos.DrawSphere(currentPath[i], 0.12f);
                prev = currentPath[i];
            }
        }
    }
}
