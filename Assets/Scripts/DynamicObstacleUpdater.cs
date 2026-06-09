using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DynamicObstacleUpdater : MonoBehaviour
{
    public float updateInterval = 0.1f;
    public float movementThreshold = 0.05f;

    private Collider col;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float timer;

    void Awake()
    {
        col = GetComponent<Collider>();
        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    void Start()
    {
        ForceUpdate();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        timer = updateInterval;

        bool moved =
            Vector3.Distance(transform.position, lastPosition) > movementThreshold ||
            Quaternion.Angle(transform.rotation, lastRotation) > 1f;

        if (!moved)
            return;

        ForceUpdate();
    }

    public void ForceUpdate()
    {
        if (GridManager.Instance == null || col == null)
            return;

        Bounds expanded = col.bounds;
        expanded.Expand(GridManager.Instance.NodeDiameter + GridManager.Instance.maxClearanceRadius * 2f);
        GridManager.Instance.UpdateGrid(expanded);

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }
}