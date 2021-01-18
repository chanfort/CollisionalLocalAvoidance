using UnityEngine;

public class CollisionSystemProperties : MonoBehaviour
{
    public static CollisionSystemProperties active;

    public int maxIterations = 6;
    public int maxClosestDone = 3;
    public float maxForce = 200f;
    public float damping = 8f;
    public float radius = 1.5f;
    public float maxVelocity = 3.5f;
    public int updateFrequency = 1;

    void Start()
    {

    }

    public static CollisionSystemProperties GetActive()
    {
        if (CollisionSystemProperties.active == null)
        {
            CollisionSystemProperties.active = UnityEngine.Object.FindObjectOfType<CollisionSystemProperties>();
        }
		
        return CollisionSystemProperties.active;
    }

}
