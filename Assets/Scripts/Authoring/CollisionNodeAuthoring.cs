using Unity.Entities;
using UnityEngine;

public class CollisionNodeAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager manager, GameObjectConversionSystem conversionSystem)
    {
        var data = new CollisionNode { };
        manager.AddComponentData(entity, data);
    }
}
