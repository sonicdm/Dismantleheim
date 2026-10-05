using UnityEngine;

namespace Dismantleheim.Removal
{
	internal static class ContainerContentsInspector
	{
		public static bool HasStoredItems(GameObject root, out int itemCount)
		{
			itemCount = 0;
			if ((Object)(object)root == (Object)null)
			{
				return false;
			}

			Container container = root.GetComponentInChildren<Container>();
			if ((Object)(object)container == (Object)null)
			{
				return false;
			}

			Inventory inv = container.GetInventory();
			if (inv == null)
			{
				return false;
			}

			itemCount = inv.NrOfItems();
			return itemCount > 0;
		}

		/// <summary>True when vanilla Piece.CanBeRemoved would refuse due to container contents.</summary>
		public static bool VanillaOccupiedChestRefusal(GameObject root)
		{
			if ((Object)(object)root == (Object)null)
			{
				return false;
			}

			Piece piece = root.GetComponent<Piece>();
			if ((Object)(object)piece == (Object)null)
			{
				return false;
			}

			return !piece.CanBeRemoved() && HasStoredItems(root, out _);
		}
	}
}
