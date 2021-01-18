using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct Velocity : IComponentData
{
	public float3 Value;
	public float3 CollisionVelocity;
}
