using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class VelocityAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public float3 Value;
    
    public void Convert(Entity entity, EntityManager manager, GameObjectConversionSystem conversionSystem)
    {
        var data = new Velocity { Value = Value };
        manager.AddComponentData(entity, data);
    }
}
