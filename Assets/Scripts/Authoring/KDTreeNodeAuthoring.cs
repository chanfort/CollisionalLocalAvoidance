using Unity.Entities;
using UnityEngine;

public class KDTreeNodeAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager manager, GameObjectConversionSystem conversionSystem)
    {
        var data = new KDTreeNode { };
        manager.AddComponentData(entity, data);
    }
}
