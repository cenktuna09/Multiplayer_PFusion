using UnityEngine;

namespace Helpers.Physics
{
    /// <summary>
    /// Helper class for getting the start and end points of a capsule collider
    /// </summary>
    public static class PhysicsUtil
	{
		public static (Vector3 point0, Vector3 point1) GetPoints(this CapsuleCollider coll)
		{
			(Vector3 point0, Vector3 point1) points;
			float offset = coll.height / 2 - coll.radius;
			Vector3 direction = new Vector3 { [coll.direction] = 1 };
			points.point0 = coll.transform.TransformPoint(coll.center + direction * offset);
			points.point1 = coll.transform.TransformPoint(coll.center - direction * offset);
			return points;
		}
	}
}