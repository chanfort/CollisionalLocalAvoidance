using Unity.Jobs;
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;

public class AccelerationSystem : JobComponentSystem
{
    [BurstCompile]
    struct VelocityUpdateJob : IJobChunk
    {
        public ComponentTypeHandle<Velocity> VelocityType;
        [ReadOnly] public ComponentTypeHandle<Acceleration> AccelerationType;
        public float dt;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            NativeArray<Velocity> chunkVelocities = chunk.GetNativeArray(VelocityType);
            NativeArray<Acceleration> chunkAccelerations = chunk.GetNativeArray(AccelerationType);

            for (int i = 0; i < chunk.Count; i++)
            {
                Velocity velocity = chunkVelocities[i];
                Acceleration acceleration = chunkAccelerations[i];

                chunkVelocities[i] = new Velocity
                {
                    Value = velocity.Value + acceleration.Value * dt
                };
            }
        }
    }

    private EntityQuery m_Group;

    protected override void OnCreate()
    {
        m_Group = GetEntityQuery(typeof(Velocity), typeof(Acceleration));
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        ComponentTypeHandle<Velocity> velocityType = GetComponentTypeHandle<Velocity>();
        ComponentTypeHandle<Acceleration> accelerationType = GetComponentTypeHandle<Acceleration>();

        VelocityUpdateJob job = new VelocityUpdateJob
        {
            VelocityType = velocityType,
            AccelerationType = accelerationType,
            dt = Time.DeltaTime
        };

        return job.Schedule(m_Group, inputDeps);
    }
}
