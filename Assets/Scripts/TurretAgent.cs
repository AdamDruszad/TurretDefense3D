using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

// The TurretAgent learns to aim at and shoot a flying drone.
//
// Actions (3):
//   Continuous[0] - yaw rotation speed   (turn left / right), [-1, 1]
//   Continuous[1] - pitch rotation speed (turn up / down),    [-1, 1]
//   Discrete[0]   - shoot (0 = hold fire, 1 = fire)
//
// Observations:
//   10 vector observations (see CollectObservations)
//   + a RayPerceptionSensor3D on the muzzle that "sees" drones in front of the barrel
public class TurretAgent : Agent
{
    [Header("Turret parts")]
    public Transform YawPivot;      // rotates around the Y axis (left / right)
    public Transform PitchPivot;    // rotates around the X axis (up / down)
    public Transform Muzzle;        // tip of the barrel, shots start from here
    public Drone Drone;
    public LineRenderer ShotLine;   // optional, only for visuals

    [Header("Turret settings")]
    public float YawSpeed = 120f;   // degrees / second
    public float PitchSpeed = 60f;  // degrees / second
    public float MinPitch = -5f;
    public float MaxPitch = 50f;
    public float FireCooldown = 0.3f;
    public float ShotRange = 40f;

    [Header("Rewards")]
    public float HitReward = 1f;
    public float MissPenalty = 0.05f;
    // Small reward every step for keeping the barrel close to the drone (reward shaping)
    public float AimRewardScale = 0.002f;
    public float AimRewardAngle = 30f;

    [Header("Drone")]
    // Used when there is no trainer connected (heuristic / inference)
    public float DefaultDroneSpeed = 4f;
    // Used to scale the drone velocity observation into roughly [-1, 1]
    public float MaxDroneSpeed = 10f;

    private float pitch;
    private float cooldownTimer;
    private float shotLineTimer;
    private float droneSpeed;

    // Statistics of the current episode (shown in TensorBoard)
    private int hits;
    private int shots;

    // OnEpisodeBegin() gets called at the start of every episode
    public override void OnEpisodeBegin()
    {
        // Send the statistics of the previous episode to TensorBoard
        if (CompletedEpisodes > 0)
        {
            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Turret/Hits per episode", hits);
            if (shots > 0)
            {
                stats.Add("Turret/Accuracy", (float)hits / shots);
            }
        }
        hits = 0;
        shots = 0;

        // Reset the turret to a random direction
        YawPivot.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        pitch = 0f;
        ApplyPitch();
        cooldownTimer = 0f;

        // The drone speed comes from the YAML curriculum during training
        droneSpeed = Academy.Instance.EnvironmentParameters.GetWithDefault("drone_speed", DefaultDroneSpeed);
        Drone.Respawn(droneSpeed);
    }

    // CollectObservations() supplies the "hard-coded" inputs of the agent.
    // The RayPerceptionSensor3D on the muzzle is added automatically.
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 aimDirection = Muzzle.forward;
        Vector3 toDrone = Drone.transform.position - Muzzle.position;

        // Current yaw of the turret (sin / cos, so 359 and 1 degrees are close)  - 2 values
        float yaw = YawPivot.localEulerAngles.y * Mathf.Deg2Rad;
        sensor.AddObservation(Mathf.Sin(yaw));
        sensor.AddObservation(Mathf.Cos(yaw));

        // Current pitch of the turret                                            - 1 value
        sensor.AddObservation(pitch / MaxPitch);

        // How far the drone is to the left / right of the barrel                 - 1 value
        Vector3 up = transform.up;
        float horizontalError = Vector3.SignedAngle(
            Vector3.ProjectOnPlane(aimDirection, up),
            Vector3.ProjectOnPlane(toDrone, up),
            up);
        sensor.AddObservation(horizontalError / 180f);

        // How far the drone is above / below the barrel                          - 1 value
        float verticalError = Elevation(toDrone) - Elevation(aimDirection);
        sensor.AddObservation(verticalError / 90f);

        // Distance to the drone                                                  - 1 value
        sensor.AddObservation(toDrone.magnitude / ShotRange);

        // Drone velocity, relative to the direction the turret is facing         - 3 values
        Vector3 localVelocity = YawPivot.InverseTransformDirection(Drone.Velocity);
        sensor.AddObservation(localVelocity / MaxDroneSpeed);

        // Can we shoot right now?                                                - 1 value
        sensor.AddObservation(cooldownTimer <= 0f ? 1f : 0f);
    }

    // OnActionReceived() executes the actions chosen by the agent
    public override void OnActionReceived(ActionBuffers actions)
    {
        var continuousActions = actions.ContinuousActions;
        float yawAction = Mathf.Clamp(continuousActions[0], -1f, 1f);
        float pitchAction = Mathf.Clamp(continuousActions[1], -1f, 1f);
        bool fire = actions.DiscreteActions[0] == 1;

        // Rotate the turret
        YawPivot.Rotate(0f, yawAction * YawSpeed * Time.fixedDeltaTime, 0f, Space.Self);
        pitch = Mathf.Clamp(pitch + pitchAction * PitchSpeed * Time.fixedDeltaTime, MinPitch, MaxPitch);
        ApplyPitch();

        if (fire && cooldownTimer <= 0f)
        {
            Shoot();
        }

        // Reward shaping: a small reward for keeping the drone close to the crosshair
        float aimError = Vector3.Angle(Muzzle.forward, Drone.transform.position - Muzzle.position);
        AddReward(AimRewardScale * Mathf.Clamp01(1f - aimError / AimRewardAngle));
    }

    private void Shoot()
    {
        shots++;
        cooldownTimer = FireCooldown;

        Vector3 shotEnd = Muzzle.position + Muzzle.forward * ShotRange;
        bool hitDrone = false;

        // Hitscan shot: a raycast from the muzzle
        if (Physics.Raycast(Muzzle.position, Muzzle.forward, out RaycastHit hit, ShotRange))
        {
            shotEnd = hit.point;
            // We only count our own drone (and not the drones of the other arenas)
            hitDrone = hit.collider.GetComponent<Drone>() == Drone;
        }

        if (hitDrone)
        {
            hits++;
            AddReward(HitReward);
            Drone.Respawn(droneSpeed);
        }
        else
        {
            AddReward(-MissPenalty);
        }

        ShowShot(shotEnd);
    }

    private void FixedUpdate()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.fixedDeltaTime;
        }

        if (ShotLine != null && shotLineTimer > 0f)
        {
            shotLineTimer -= Time.fixedDeltaTime;
            ShotLine.enabled = shotLineTimer > 0f;
        }
    }

    private void ShowShot(Vector3 end)
    {
        if (ShotLine == null)
        {
            return;
        }
        ShotLine.useWorldSpace = true;
        ShotLine.SetPosition(0, Muzzle.position);
        ShotLine.SetPosition(1, end);
        ShotLine.enabled = true;
        shotLineTimer = 0.05f;
    }

    private void ApplyPitch()
    {
        // A negative rotation around X tilts the barrel up
        PitchPivot.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
    }

    // Angle of a direction above the horizon, in degrees
    private float Elevation(Vector3 direction)
    {
        return 90f - Vector3.Angle(transform.up, direction);
    }

    // The Heuristic() function lets us control the turret with the keyboard:
    // Left / Right arrows (A / D) - turn, Up / Down arrows (W / S) - aim up / down, Space - shoot
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");

        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }
}
