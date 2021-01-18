using Unity.Burst;
using Unity.Jobs;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

public class CollisionSystem : JobComponentSystem
{
    [BurstCompile]
    struct PrepareBucketsJob : IJob
    {
        [ReadOnly]
        public NativeArray<float3> positions;
        public NativeMultiHashMap<int, int> buckets;

        public void Execute()
        {
            for (int i = 0; i < positions.Length; i++)
            {
                int hash = Hash(positions[i]);
                buckets.Add(hash, i);
            }
        }

        const int fieldWidth = 4000;
        const int fieldWidthHalf = fieldWidth / 2;
        const int fieldHeight = 4000;
        const int fieldHeightHalf = fieldHeight / 2;

        int Hash(float3 position)
        {
            int2 quantized = new int2(math.floor(position.xz / 2f));
            return quantized.x + fieldWidthHalf + (quantized.y + fieldHeightHalf) * fieldWidth;
        }
    }

    [BurstCompile]
    struct CollisionJob : IJobChunk
    {
        [ReadOnly] public NativeMultiHashMap<int, int> buckets;
        [ReadOnly] public NativeArray<float3> positions;
        [ReadOnly] public NativeArray<float3> velocities;
        [ReadOnly] public float dt;
        [ReadOnly] public int maxIterations;
        [ReadOnly] public int maxClosestDone;
        [ReadOnly] public float maxForce;
        [ReadOnly] public float damping;
        [ReadOnly] public float radiusSqr;
        [ReadOnly] public float maxVelocity;
        [ReadOnly] public float maxVelocitySqr;
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
            float3 currentPosition = positions[i];
            float3 currentVelocity = velocities[i];

            float2 velocity = currentVelocity.xz;

            int j = 0;
            int hash = Hash(currentPosition);
            NativeMultiHashMapIterator<int> iterator;
            bool found = buckets.TryGetFirstValue(hash, out j, out iterator);

            int iterations = 0;
            int closestDone = 0;

            while (found)
            {
                if (j == i)
                {
                    // Exclude self
                    found = buckets.TryGetNextValue(out j, ref iterator);
                    continue;
                }

                float epsilon = 0.00001f;

                // Construct the force direction
                float2 relative = currentPosition.xz - positions[j].xz;

                if (math.dot(relative, relative) < epsilon || !math.any(relative)) relative = new float2(1, 0);

                float distanceSqr = math.dot(relative, relative);

                // Update velocity data
                float str = math.max(0, radiusSqr - distanceSqr) / radiusSqr;
                velocity += math.normalize(relative) * str * maxForce * dt;

                // Next iteration
                found = buckets.TryGetNextValue(out j, ref iterator);

                // We will check 3 cells in the corner
                if (!found && closestDone < maxClosestDone)
                {
                    float2 position = currentPosition.xz / step;
                    int2 flooredPosition = new int2(math.floor(position));

                    int2 nextQuantizedPosition = new int2(math.round(2f * position - (flooredPosition + new float2(0.5f))));
                    if (nextQuantizedPosition.x == flooredPosition.x) nextQuantizedPosition.x -= 1;
                    if (nextQuantizedPosition.y == flooredPosition.y) nextQuantizedPosition.y -= 1;

                    if (closestDone == 1) nextQuantizedPosition.x = flooredPosition.x;
                    else if (closestDone == 2) nextQuantizedPosition.y = flooredPosition.y;

                    int nextHash = nextQuantizedPosition.x + fieldWidthHalf + (nextQuantizedPosition.y + fieldHeightHalf) * fieldWidth;

                    found = buckets.TryGetFirstValue(nextHash, out j, out iterator);

                    closestDone++;
                }

                if (++iterations > maxIterations) break;
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

        const int fieldWidth = 4000;
        const int fieldWidthHalf = fieldWidth / 2;
        const int fieldHeight = 4000;
        const int fieldHeightHalf = fieldHeight / 2;
        const float step = 2f;

        int Hash(float3 position)
        {
            int2 quantized = new int2(math.floor(position.xz / 2f));
            return quantized.x + fieldWidthHalf + (quantized.y + fieldHeightHalf) * fieldWidth;
        }
    }

    [BurstCompile]
    struct CopyTranslations : IJobChunk
    {
        [WriteOnly] public NativeArray<float3> translations;
        [ReadOnly] public ComponentTypeHandle<Translation> translationType;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            NativeArray<Translation> chunkTranslations = chunk.GetNativeArray(translationType);

            for (int i = 0; i < chunk.Count; i++)
            {
                translations[firstEntityIndex + i] = chunkTranslations[i].Value;
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
    private NativeMultiHashMap<int, int> CollisionBuckets;
    private int m_maxIterations = 6;
    private int m_maxClosestDone = 3;
    private float m_maxForce = 200f;
    private float m_damping = 8f;
    private float m_radiusSqr = 1.5f;
    private float m_maxVelocity = 3.5f;
    private float m_maxVelocitySqr;
    private int m_updateFrequency = 1;

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery(typeof(Translation), typeof(Velocity), typeof(CollisionNode));
        CollisionSystemProperties collisionSystemProperties = CollisionSystemProperties.GetActive();

        if (collisionSystemProperties != null)
        {
            m_maxIterations = collisionSystemProperties.maxIterations;
            m_maxClosestDone = collisionSystemProperties.maxClosestDone;
            m_maxForce = collisionSystemProperties.maxForce;
            m_damping = collisionSystemProperties.damping;
            m_radiusSqr = collisionSystemProperties.radius * collisionSystemProperties.radius;
            m_maxVelocity = collisionSystemProperties.maxVelocity;
            m_updateFrequency = collisionSystemProperties.updateFrequency;
        }

        m_maxVelocitySqr = m_maxVelocity * m_maxVelocity;
    }

    protected override void OnDestroy()
    {
        if (translations.IsCreated) translations.Dispose();
        if (velocities.IsCreated) velocities.Dispose();
        if (CollisionBuckets.IsCreated) CollisionBuckets.Dispose();
    }

    NativeArray<float3> translations;
    NativeArray<float3> velocities;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        int count = m_Group.CalculateEntityCount();

        if (count == 0) return inputDeps;

        if (!translations.IsCreated)
        {
            translations = new NativeArray<float3>(count, Allocator.Persistent);
        }
        else if (translations.Length != count)
        {
            translations.Dispose();
            translations = new NativeArray<float3>(count, Allocator.Persistent);
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

        if (CollisionBuckets.IsCreated) CollisionBuckets.Dispose();
        CollisionBuckets = new NativeMultiHashMap<int, int>(count, Allocator.Persistent);

        PrepareBucketsJob prepareBucketsJob = new PrepareBucketsJob
        {
            positions = translations,
            buckets = CollisionBuckets
        };

        JobHandle prepareBucketsHandle = prepareBucketsJob.Schedule(copyJobsHandle);

        CollisionJob collisionJob = new CollisionJob
        {
            dt = Time.DeltaTime,
            positions = translations,
            velocities = velocities,
            buckets = CollisionBuckets,
            maxIterations = m_maxIterations,
            maxClosestDone = m_maxClosestDone,
            maxForce = m_maxForce,
            damping = m_damping,
            radiusSqr = m_radiusSqr,
            maxVelocity = m_maxVelocity,
            maxVelocitySqr = m_maxVelocitySqr,
            velocityType = GetComponentTypeHandle<Velocity>()
        };

        return collisionJob.Schedule(m_Group, prepareBucketsHandle);
    }
}