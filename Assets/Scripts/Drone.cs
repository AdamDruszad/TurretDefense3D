using UnityEngine;

// The Drone flies around the turret on random paths.
// Its position is stored in polar coordinates (angle, radius, height)
// around the arena center, so it always stays in front of the turret
// and inside shooting range, no matter how it moves.
public class Drone : MonoBehaviour
{
    [Header("Flight area (relative to the arena center)")]
    public float MinRadius = 8f;
    public float MaxRadius = 20f;
    public float MinHeight = 1.5f;
    public float MaxHeight = 8f;

    // How far around the turret (in degrees) a single path can go
    public float MaxPathAngle = 90f;

    // Speed in units / second, set by the agent (curriculum parameter)
    public float Speed { get; private set; }

    // World space velocity, the agent observes this
    public Vector3 Velocity { get; private set; }

    // Current position in polar coordinates
    private float angle;
    private float radius;
    private float height;

    // The waypoint the drone is currently flying to
    private float targetAngle;
    private float targetRadius;
    private float targetHeight;

    // Respawn() places the drone at a random position and sets its speed.
    // A speed of 0 means a static drone.
    public void Respawn(float speed)
    {
        Speed = speed;

        angle = Random.Range(0f, 360f);
        radius = Random.Range(MinRadius, MaxRadius);
        height = Random.Range(MinHeight, MaxHeight);

        PickNewWaypoint();

        Velocity = Vector3.zero;
        transform.localPosition = PolarToLocal(angle, radius, height);
    }

    private void PickNewWaypoint()
    {
        targetAngle = angle + Random.Range(-MaxPathAngle, MaxPathAngle);
        targetRadius = Random.Range(MinRadius, MaxRadius);
        targetHeight = Random.Range(MinHeight, MaxHeight);
    }

    private void FixedUpdate()
    {
        if (Speed <= 0f)
        {
            Velocity = Vector3.zero;
            return;
        }

        // Remaining distance to the waypoint (arc length + radius change + height change)
        float arc = Mathf.Abs(targetAngle - angle) * Mathf.Deg2Rad * radius;
        float radiusDiff = targetRadius - radius;
        float heightDiff = targetHeight - height;
        float distance = Mathf.Sqrt(arc * arc + radiusDiff * radiusDiff + heightDiff * heightDiff);

        float step = Speed * Time.fixedDeltaTime;
        Vector3 oldPosition = transform.position;

        if (step >= distance)
        {
            // We reached the waypoint, so we pick a new one
            angle = Mathf.Repeat(targetAngle, 360f);
            radius = targetRadius;
            height = targetHeight;
            PickNewWaypoint();
        }
        else
        {
            // Otherwise we move a bit closer to the waypoint
            float t = step / distance;
            angle += (targetAngle - angle) * t;
            radius += radiusDiff * t;
            height += heightDiff * t;
        }

        transform.localPosition = PolarToLocal(angle, radius, height);
        Velocity = (transform.position - oldPosition) / Time.fixedDeltaTime;
    }

    private static Vector3 PolarToLocal(float angleDegrees, float r, float h)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(rad) * r, h, Mathf.Cos(rad) * r);
    }
}
