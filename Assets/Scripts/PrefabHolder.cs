using UnityEngine;

public class PrefabHolder : MonoBehaviour
{
    public static PrefabHolder active;
    public GameObject prefab;
    public int numberToSpawn = 100;
    public float spawnAreaSize = 1f;
    public float accelerationScaler = 0.5f;
    [HideInInspector] public bool isStarted = false;

    void Start()
    {
        isStarted = true;
    }

    public static PrefabHolder GetActive()
    {
        if (PrefabHolder.active == null)
        {
            PrefabHolder.active = UnityEngine.Object.FindObjectOfType<PrefabHolder>();
        }
		
        return PrefabHolder.active;
    }
}
