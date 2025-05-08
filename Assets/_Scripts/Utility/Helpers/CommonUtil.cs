using UnityEngine;

namespace Helpers.Common
{
    /// <summary>
    /// Static class that contains a DestroyAllChildren method.
    /// </summary>
    public static class CommonUtil
	{
		public static void DestroyAllChildren(this Transform tf)
		{
			foreach (Transform child in tf)
			{
				Object.Destroy(child.gameObject);
			}
		}
	}
}