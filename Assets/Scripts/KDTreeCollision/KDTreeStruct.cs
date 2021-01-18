// KDTree.cs - A Stark, September 2009.

//	This class implements a data structure that stores a list of points in space.
//	A common task in game programming is to take a supplied point and discover which
//	of a stored set of points is nearest to it. For example, in path-plotting, it is often
//	useful to know which waypoint is nearest to the player's current
//	position. The kd-tree allows this "nearest neighbour" search to be carried out quickly,
//	or at least much more quickly than a simple linear search through the list.

//	At present, the class only allows for construction (using the MakeFromPoints static method)
//	and nearest-neighbour searching (using FindNearest). More exotic kd-trees are possible, and
//	this class may be extended in the future if there seems to be a need.

//	The nearest-neighbour search returns an integer index - it is assumed that the original
//	array of points is available for the lifetime of the tree, and the index refers to that
//	array.

// Jobified to work with Unity DOTS by chanfort.

using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;

[BurstCompile]
public struct KDTreeStruct : IJob
{
    public NativeArray<int> pivotIndex;

    //	Change this value to 2 if you only need two-dimensional X,Y points. The search will
    //	be quicker in two dimensions.
    // const int numDims = 2;

    public NativeArray<float2> points;
    public NativeArray<int> leftChilds;
    public NativeArray<int> rightChilds;
    public NativeArray<int> axies;
    public NativeArray<int> indicesGlobal;

    public bool isCreated;

    public void Execute()
    {
        MakeFromPoints();
    }

	public void InitializeArrays(NativeArray<float2> points1)
    {
        points = points1;

		int count = points1.Length;

		if (!indicesGlobal.IsCreated)
		{
			indicesGlobal = new NativeArray<int>(count, Allocator.Persistent);
		}
		else if(indicesGlobal.IsCreated && indicesGlobal.Length != count)
		{
			indicesGlobal.Dispose();
			indicesGlobal = new NativeArray<int>(count, Allocator.Persistent);
		}

		if (!leftChilds.IsCreated)
		{
			leftChilds = new NativeArray<int>(count, Allocator.Persistent);
		}
		else if(leftChilds.IsCreated && leftChilds.Length != count)
		{
			leftChilds.Dispose();
			leftChilds = new NativeArray<int>(count, Allocator.Persistent);
		}

		if (!rightChilds.IsCreated)
		{
			rightChilds = new NativeArray<int>(count, Allocator.Persistent);
		}
		else if(rightChilds.IsCreated && rightChilds.Length != count)
		{
			rightChilds.Dispose();
			rightChilds = new NativeArray<int>(count, Allocator.Persistent);
		}

		if (!axies.IsCreated)
		{
			axies = new NativeArray<int>(count, Allocator.Persistent);
		}
		else if(axies.IsCreated && axies.Length != count)
		{
			axies.Dispose();
			axies = new NativeArray<int>(count, Allocator.Persistent);
		}

        if(!pivotIndex.IsCreated)
        {
            pivotIndex = new NativeArray<int>(1, Allocator.Persistent);
            pivotIndex[0] = -1;
        }

        for (int i = 0; i < points.Length; i++)
        {
            leftChilds[i] = -1;
            rightChilds[i] = -1;
            axies[i] = -1;
			indicesGlobal[i] = i;
        }

		isCreated = true;
	}

    //	Make a new tree from a list of points.
    public void MakeFromPoints()
    {
        for (int i = 0; i < points.Length; i++)
        {
            leftChilds[i] = -1;
            rightChilds[i] = -1;
            axies[i] = -1;
            indicesGlobal[i] = i;
        }

        MakeFromPointsInner(0, 0, points.Length - 1, -1, -1, true);
    }

    //	Recursively build a tree by separating points at plane boundaries.
    void MakeFromPointsInner(
        int depth,
        int stIndex, int enIndex,
        int parentPivotIndex,
        int direction,
        bool isFirstTime
    )
    {
        int axis1 = depth % 2;
        int splitPoint = FindPivotIndex(stIndex, enIndex, axis1);

        if (isFirstTime)
        {
            isFirstTime = false;
            pivotIndex[0] = indicesGlobal[splitPoint];
        }

        int pivotIndex1 = indicesGlobal[splitPoint];

        axies[pivotIndex1] = axis1;

        if (parentPivotIndex > -1)
        {
            if (direction == 0)
            {
                leftChilds[parentPivotIndex] = pivotIndex1;
            }
            else if (direction == 1)
            {
                rightChilds[parentPivotIndex] = pivotIndex1;
            }
        }

        int leftEndIndex = splitPoint - 1;

        if (leftEndIndex >= stIndex)
        {
            MakeFromPointsInner(depth + 1, stIndex, leftEndIndex, pivotIndex1, 0, false);
        }

        int rightStartIndex = splitPoint + 1;

        if (rightStartIndex <= enIndex)
        {
            MakeFromPointsInner(depth + 1, rightStartIndex, enIndex, pivotIndex1, 1, false);
        }
    }

    void SwapElements(int a, int b)
    {
        int temp = indicesGlobal[a];
        indicesGlobal[a] = indicesGlobal[b];
        indicesGlobal[b] = temp;
    }


    //	Simple "median of three" heuristic to find a reasonable splitting plane.
    int FindSplitPoint(int stIndex, int enIndex, int axis)
    {
        int stIndexCp = indicesGlobal[stIndex];
        int enIndexCp = indicesGlobal[enIndex];

        float a = points[stIndexCp][axis];
        float b = points[enIndexCp][axis];
        int midIndex = (stIndex + enIndex) / 2;
        int midIndexCp = indicesGlobal[midIndex];

        float m = points[midIndexCp][axis];

        if (a > b)
        {
            if (m > a)
            {
                return stIndex;
            }

            if (b > m)
            {
                return enIndex;
            }

            return midIndex;
        }
        else
        {
            if (a > m)
            {
                return stIndex;
            }

            if (m > b)
            {
                return enIndex;
            }

            return midIndex;
        }
    }

    //	Find a new pivot index from the range by splitting the points that fall either side
    //	of its plane.
    public int FindPivotIndex(int stIndex, int enIndex, int axis)
    {
        int splitPoint = FindSplitPoint(stIndex, enIndex, axis);

        int initialIndex = indicesGlobal[splitPoint];
        float2 pivot = points[initialIndex];
        SwapElements(stIndex, splitPoint);

        int currPt = stIndex + 1;
        int endPt = enIndex;

        while (currPt <= endPt)
        {
            int currPtIndex = indicesGlobal[currPt];
            float2 curr = points[currPtIndex];

            if ((curr[axis] > pivot[axis]))
            {
                SwapElements(currPt, endPt);
                endPt--;
            }
            else
            {
                SwapElements(currPt - 1, currPt);
                currPt++;
            }
        }

        return currPt - 1;
    }

    public int FindNearest(float2 pt)
    {
        float bestSqDist = float.MaxValue;
        int bestIndex = -1;

        if (isCreated == false)
        {
            return bestIndex;
        }

        Search(pt, ref bestSqDist, ref bestIndex, pivotIndex[0]);

        return bestIndex;
    }

    public int FindNearest(float2 pt, float innerRSq)
    {
        float bestSqDist = float.MaxValue;
        int bestIndex = -1;
        if (isCreated == false)
        {
            return bestIndex;
        }

        SearchK(pt, ref bestSqDist, ref innerRSq, ref bestIndex, pivotIndex[0]);
        
        return bestIndex;
    }

    // Find and returns	k-th nearest neighbour
    public int FindNearestK(float2 pt, int k)
    {
        float bestSqDist = float.MaxValue;
        float minSqDist = 0f;
        int bestIndex = -1;

        if (isCreated == false)
        {
            return bestIndex;
        }

        for (int i = 0; i < k - 1; i++)
        {
            SearchK(pt, ref bestSqDist, ref minSqDist, ref bestIndex, pivotIndex[0]);

            minSqDist = bestSqDist;
            bestSqDist = float.MaxValue;
            bestIndex = -1;
        }

        SearchK(pt, ref bestSqDist, ref minSqDist, ref bestIndex, pivotIndex[0]);
        
        return bestIndex;
    }

    // Find and returns	k-th nearest neighbour distance
    public float FindNearestK_R(float2 pt, int k)
    {
        float bestSqDist = float.MaxValue;

        if (isCreated == false)
        {
            return bestSqDist;
        }

        float minSqDist = 0f;
        int bestIndex = -1;

        for (int i = 0; i < k - 1; i++)
        {
            SearchK(pt, ref bestSqDist, ref minSqDist, ref bestIndex, pivotIndex[0]);

            minSqDist = bestSqDist;
            bestSqDist = float.MaxValue;
            bestIndex = -1;
        }

        SearchK(pt, ref bestSqDist, ref minSqDist, ref bestIndex, pivotIndex[0]);

        return (Mathf.Sqrt(bestSqDist));
    }

    //	Recursively search the tree.
    void Search(float2 pt, ref float bestSqSoFar, ref int bestIndex, int pind)
    {
        float2 pt1 = points[pind];
        int leftChild = leftChilds[pind];
        int rightChild = rightChilds[pind];
        int ax = axies[pind];

        float2 relative = pt1 - pt;
        float mySqDist = math.dot(relative, relative);

        if (mySqDist < bestSqSoFar)
        {
            bestSqSoFar = mySqDist;
            bestIndex = pind;
        }

        float planeDist = pt[ax] - pt1[ax];

        int selector = planeDist <= 0 ? 0 : 1;

        int ichild = -1;
        if (selector == 0)
        {
            ichild = leftChild;
        }
        else if (selector == 1)
        {
            ichild = rightChild;
        }

        if (ichild > -1)
        {
            Search(pt, ref bestSqSoFar, ref bestIndex, ichild);
        }

        selector = (selector + 1) % 2;
        ichild = -1;

        if (selector == 0)
        {
            ichild = leftChild;
        }
        else if (selector == 1)
        {
            ichild = rightChild;
        }

        float sqPlaneDist = planeDist * planeDist;

        if ((ichild > -1) && (bestSqSoFar > sqPlaneDist))
        {
            Search(pt, ref bestSqSoFar, ref bestIndex, ichild);
        }
    }

    void SearchK(float2 pt, ref float bestSqSoFar, ref float minSqDist, ref int bestIndex, int pind)
    {
        float2 pt1 = points[pind];
        int leftChild = leftChilds[pind];
        int rightChild = rightChilds[pind];
        int ax = axies[pind];

        float2 relative = pt1 - pt;
        float mySqDist = math.dot(relative, relative);

        if (mySqDist < bestSqSoFar)
        {
            if (mySqDist > minSqDist)
            {
                bestSqSoFar = mySqDist;
                bestIndex = pind;
            }
        }

        float planeDist = pt[ax] - pt1[ax];

        int selector = planeDist <= 0 ? 0 : 1;

        int ichild = -1;
        if (selector == 0)
        {
            ichild = leftChild;
        }
        else if (selector == 1)
        {
            ichild = rightChild;
        }

        if (ichild > -1)
        {
            SearchK(pt, ref bestSqSoFar, ref minSqDist, ref bestIndex, ichild);
        }

        selector = (selector + 1) % 2;
        ichild = -1;

        if (selector == 0)
        {
            ichild = leftChild;
        }
        else if (selector == 1)
        {
            ichild = rightChild;
        }

        float sqPlaneDist = planeDist * planeDist;


        if ((ichild > -1) && (bestSqSoFar > sqPlaneDist))
        {
            SearchK(pt, ref bestSqSoFar, ref minSqDist, ref bestIndex, ichild);
        }
    }

    public void DisposeArrays()
    {
        if (leftChilds.IsCreated)
        {
            leftChilds.Dispose();
        }
        if (rightChilds.IsCreated)
        {
            rightChilds.Dispose();
        }
        if (axies.IsCreated)
        {
            axies.Dispose();
        }
        if (indicesGlobal.IsCreated)
        {
            indicesGlobal.Dispose();
        }
        if (pivotIndex.IsCreated)
        {
            pivotIndex.Dispose();
        }
    }
}
