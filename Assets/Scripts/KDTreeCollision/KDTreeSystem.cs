using UnityEngine;
using Unity.Burst;
using Unity.Jobs;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

public class KDTreeSystem : JobComponentSystem
{
    [BurstCompile]
    public struct KDCollisions : IJobChunk
    {
        [ReadOnly] public KDTreeStruct kdTree;
        [ReadOnly] public NativeArray<float2> positions;
        [ReadOnly] public NativeArray<float3> velocities;
        [ReadOnly] public float dt;
        public ComponentTypeHandle<Velocity> velocityType;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            NativeArray<Velocity> chunkVelocities = chunk.GetNativeArray(velocityType);

            for (int i = 0; i < chunk.Count; i++)
            {
                chunkVelocities[i] = Calculate(firstEntityIndex + i);
            }
        }

        Velocity Calculate(int i)
        {
            float2 posi = positions[i];
            float3 currentVelocity = velocities[i];
            float2 velocity = currentVelocity.xz;

            float previousMaxDistanceSqr = 0.01f;

            for (int k = 0; k < 1; k++)
            {
                int j = kdTree.FindNearest(positions[i], previousMaxDistanceSqr);

                if (i != j && j >= 0)
                {
                    float2 posj = positions[j];

                    float2 relative = posi - posj;
                    float distanceSqr = math.dot(relative, relative);
                    previousMaxDistanceSqr = distanceSqr;

                    float str = math.max(0, radiusSqr - distanceSqr) / radiusSqr;
                    velocity += math.normalize(relative) * str * maxForce * dt;
                }
            }

            float2 dampingStr = velocity * damping * dt;
            velocity -= dampingStr;

            float3 collisionVelocity = new float3(velocity.x, 0, velocity.y) - currentVelocity;

            currentVelocity += collisionVelocity;

            if (math.dot(currentVelocity, currentVelocity) > maxVelocitySqr)
            {
                currentVelocity = math.normalize(currentVelocity) * maxVelocity;
            }

            Velocity velocityComponent = new Velocity
            {
                Value = currentVelocity,
                CollisionVelocity = collisionVelocity
            };

            return velocityComponent;
        }

        const float radiusSqr = 1.5f * 1.5f;
        const float maxForce = 200f;
        const float damping = 8f;
        const float maxVelocity = 3.5f;
        const float maxVelocitySqr = maxVelocity * maxVelocity;
    }

    [BurstCompile]
    struct CopyTranslations : IJobChunk
    {
        [WriteOnly] public NativeArray<float2> translations;
        [ReadOnly] public ComponentTypeHandle<Translation> translationType;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            NativeArray<Translation> chunkTranslations = chunk.GetNativeArray(translationType);

            for (int i = 0; i < chunk.Count; i++)
            {
                translations[firstEntityIndex + i] = chunkTranslations[i].Value.xz;
            }
        }
    }

    [BurstCompile]
    struct CopyVelocities : IJobChunk
    {
        [WriteOnly] public NativeArray<float3> velocities;
        [ReadOnly] public ComponentTypeHandle<Velocity> velocityType;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            NativeArray<Velocity> chunkVelocities = chunk.GetNativeArray(velocityType);

            for (int i = 0; i < chunk.Count; i++)
            {
                velocities[firstEntityIndex + i] = chunkVelocities[i].Value;
            }
        }
    }

    private EntityQuery m_Group;
    private KDTreeStruct kd;

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery(typeof(Translation), typeof(Velocity), typeof(KDTreeNode));
        kd = new KDTreeStruct();
        PrintMessages();
    }

    void PrintMessages()
    {
        if (buildKDInJob)
        {
            Debug.Log("Building KDTree in IJob. Press A to switch building on main thread.");
        }
        else
        {
            Debug.Log("Building KDTree on main thread. Press A to switch building on IJob.");
        }
    }

    protected override void OnDestroy()
    {
        if (translations.IsCreated) translations.Dispose();
        if (velocities.IsCreated) velocities.Dispose();
        kd.DisposeArrays();
    }

    NativeArray<float2> translations;
    NativeArray<float3> velocities;
    bool buildKDInJob = true;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            buildKDInJob = !buildKDInJob;
            PrintMessages();
        }

        int count = m_Group.CalculateEntityCount();

        if (count == 0) return inputDeps;

        if (!translations.IsCreated)
        {
            translations = new NativeArray<float2>(count, Allocator.Persistent);
        }
        else if (translations.Length != count)
        {
            translations.Dispose();
            translations = new NativeArray<float2>(count, Allocator.Persistent);
        }

        if (!velocities.IsCreated)
        {
            velocities = new NativeArray<float3>(count, Allocator.Persistent);
        }
        else if (velocities.Length != count)
        {
            velocities.Dispose();
            velocities = new NativeArray<float3>(count, Allocator.Persistent);
        }

        CopyTranslations copyTranslationsJob = new CopyTranslations
        {
            translations = translations,
            translationType = GetComponentTypeHandle<Translation>()
        };

        CopyVelocities copyVelocitiesJob = new CopyVelocities
        {
            velocities = velocities,
            velocityType = GetComponentTypeHandle<Velocity>()
        };

        JobHandle copyTranslationsJobHandle = copyTranslationsJob.Schedule(m_Group, inputDeps);
        JobHandle copyVelocitiesJobHandle = copyVelocitiesJob.Schedule(m_Group, inputDeps);
        JobHandle copyJobsHandle = JobHandle.CombineDependencies(copyTranslationsJobHandle, copyVelocitiesJobHandle);

        if (!kd.isCreated)
        {
            kd.InitializeArrays(translations);
        }

        JobHandle kdBuildHandle = copyJobsHandle;
		
        if (buildKDInJob)
        {
            kdBuildHandle = kd.Schedule(copyJobsHandle);
        }
        else
        {
            copyJobsHandle.Complete();
            kd.Execute();
        }

        KDCollisions kDCollisionsJob = new KDCollisions
        {
            kdTree = kd,
            positions = translations,
            velocities = velocities,
            dt = Time.DeltaTime,
            velocityType = GetComponentTypeHandle<Velocity>()
        };
        JobHandle kDCollisionsJobHandle = kDCollisionsJob.Schedule(m_Group, kdBuildHandle);

        return kDCollisionsJobHandle;
    }
}
