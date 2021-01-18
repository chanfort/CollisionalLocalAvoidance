using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

public class MovementSystem : JobComponentSystem
{
    [BurstCompile]
    struct MovementJob : IJobChunk
    {
        public ComponentTypeHandle<Translation> TranslationType;
        [ReadOnly] public ComponentTypeHandle<Velocity> VelocityType;
        public float dt;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            NativeArray<Translation> chunkTranslations = chunk.GetNativeArray(TranslationType);
            NativeArray<Velocity> chunkVelocities = chunk.GetNativeArray(VelocityType);

            for (int i = 0; i < chunk.Count; i++)
            {
                Translation translation = chunkTranslations[i];
                Velocity velocity = chunkVelocities[i];

                chunkTranslations[i] = new Translation
                {
                    Value = translation.Value + velocity.Value * dt
                };
            }
        }
    }

    private EntityQuery m_Group;

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery(typeof(Translation), typeof(Velocity));
        Spawn();
    }

    void Spawn()
    {
        PrefabHolder prefabHolder = PrefabHolder.GetActive();
        GameObject prefab = prefabHolder.prefab;
        int numberToSpawn = prefabHolder.numberToSpawn;
        float spawnAreaSize = prefabHolder.spawnAreaSize;
        float accelerationScaler = prefabHolder.accelerationScaler;

        Entity prefabEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(prefab, GameObjectConversionSettings.FromWorld(this.World, null));
        EntityManager entityManager = this.World.EntityManager;
        Unity.Mathematics.Random rand = new Unity.Mathematics.Random(0x6E624EB7u);

        for (int i = 0; i < numberToSpawn; i++)
        {
            Entity instance = entityManager.Instantiate(prefabEntity);

            float randomAngle = rand.NextFloat();
            float randomRadius = rand.NextFloat();
            float2 pointOnCircle = GetPointOnCircle(randomAngle) * randomRadius;
            float3 randFloat3 = new float3(pointOnCircle.x, 0, pointOnCircle.y) * spawnAreaSize;

            entityManager.SetComponentData(instance, new Translation { Value = randFloat3 });
            entityManager.SetComponentData(instance, new Acceleration { Value = -randFloat3 * accelerationScaler });
        }
    }

    float2 GetPointOnCircle(float rand)
    {
        float randomAngle = rand * (2 * (float)math.PI - float.Epsilon);
        float2 pointOnCircle = new float2(math.cos(randomAngle), math.sin(randomAngle));
        return pointOnCircle;
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        ComponentTypeHandle<Translation> translationType = GetComponentTypeHandle<Translation>();
        ComponentTypeHandle<Velocity> velocityType = GetComponentTypeHandle<Velocity>();

        MovementJob job = new MovementJob
        {
            TranslationType = translationType,
            VelocityType = velocityType,
            dt = Time.DeltaTime
        };

        return job.Schedule(m_Group, inputDeps);
    }
}
