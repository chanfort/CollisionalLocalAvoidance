using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct Acceleration : IComponentData
{
	public float3 Value;
}
