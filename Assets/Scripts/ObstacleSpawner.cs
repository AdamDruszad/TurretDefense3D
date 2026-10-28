using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

// The ObstacleSpawner places the walls (the children of this object)
// at random positions between the turret and the drone at the start
// of every episode. The walls block the shots and the ray sensor,
// so the turret has to wait until the drone comes out from behind them.
public class ObstacleSpawner : MonoBehaviour
{
    [Header("Placement (relative to the arena center)")]
    public float MinRadius = 4.5f;
    public float MaxRadius = 6.5f;

    // Minimum angle between two walls, so they don't overlap
    public float MinAngleBetween = 45f;

    // Randomize() moves every active wall to a new random place.
    // The number of walls can be set from the YAML file (obstacle_count),
    // by default every child wall is used.
    public void Randomize()
    {
        int count = Mathf.RoundToInt(
            Academy.Instance.EnvironmentParameters.GetWithDefault("obstacle_count", transform.childCount));

        var usedAngles = new List<float>();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform wall = transform.GetChild(i);
            bool active = i < count;
            wall.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            float angle = PickAngle(usedAngles);
            usedAngles.Add(angle);
            float radius = Random.Range(MinRadius, MaxRadius);
            float rad = angle * Mathf.Deg2Rad;

            // The wall stands on the floor and faces the turret
            wall.localPosition = new Vector3(Mathf.Sin(rad) * radius, wall.localScale.y / 2f, Mathf.Cos(rad) * radius);
            wall.localRotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    // Picks a random angle that is not too close to the other walls
    private float PickAngle(List<float> usedAngles)
    {
        float angle = 0f;
        for (int tries = 0; tries < 20; tries++)
        {
            angle = Random.Range(0f, 360f);
            bool farEnough = true;
            foreach (float other in usedAngles)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(angle, other)) < MinAngleBetween)
                {
                    farEnough = false;
                    break;
                }
            }
            if (farEnough)
            {
                break;
            }
        }
        return angle;
    }
}
