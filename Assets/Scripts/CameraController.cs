using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public string targetTag = "Player";
    public Vector3 offset = new Vector3(0f, 8f, -10f);

    [Header("Adaptive Follow")]
    public float nearSmoothTime = 0.35f;
    public float farSmoothTime = 0.08f;
    public float catchupDistance = 10f;
    public float maxSpeed = 50f;

    [Header("Look At Target")]
    public bool lookAtTarget = true;
    public float rotationSpeed = 5f;
    
    private Vector3 currentVelocity;

    void Start()
    {
        if (target == null)
        {
            GameObject targetTemp = GameObject.FindGameObjectWithTag(targetTag);
            if (targetTemp != null)
                target = targetTemp.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        float distance = Vector3.Distance(transform.position, desiredPosition);

        float t = Mathf.Clamp01(distance / catchupDistance);
        float currentSmoothTime = Mathf.Lerp(nearSmoothTime, farSmoothTime, t);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            currentSmoothTime,
            maxSpeed
        );

        if (lookAtTarget)
            LookAtTarget();
    }

    void LookAtTarget()
    {
        Vector3 directionToTarget = target.position - transform.position;
        
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}