using Dismantleheim.Core;
using UnityEngine;

namespace Dismantleheim.Removal
{
	internal static class PermissionValidator
	{
		public static RemovalSkipReason Validate(TargetIdentity target, out string message)
		{
			message = string.Empty;
			if (target == null || target.IsStale)
			{
				message = "stale or null";
				return RemovalSkipReason.Stale;
			}

			if (RemovalPlan.LooksLikeWildcard(target.PrefabName))
			{
				message = "wildcard rejected";
				return RemovalSkipReason.WildcardRejected;
			}

			GameObject go = ExactObjectExecutor.ResolveInstance(target);
			if ((Object)(object)go == (Object)null)
			{
				message = "instance missing";
				return RemovalSkipReason.Stale;
			}

			Container container = go.GetComponentInChildren<Container>();
			if ((Object)(object)container != (Object)null)
			{
				Inventory inv = container.GetInventory();
				if (inv != null && inv.NrOfItems() > 0)
				{
					message = "container has contents";
					return RemovalSkipReason.ContainerWithContents;
				}
			}

			TeleportWorld portal = go.GetComponent<TeleportWorld>();
			if ((Object)(object)portal != (Object)null)
			{
				message = "portal protected";
				return RemovalSkipReason.Portal;
			}

			PrivateArea ward = go.GetComponent<PrivateArea>();
			if ((Object)(object)ward != (Object)null)
			{
				message = "ward";
				return RemovalSkipReason.Ward;
			}

			if (target.IsEnvironment)
			{
				bool isServer = ZNet.instance != null && ZNet.instance.IsServer();
				if (!isServer)
				{
					message = "environment requires host/admin authority (unsupported on this client)";
					return RemovalSkipReason.UnsupportedEnvironment;
				}
			}

			Piece piece = go.GetComponent<Piece>();
			if ((Object)(object)piece != (Object)null && !piece.m_canBeRemoved)
			{
				message = "m_canBeRemoved=false";
				return RemovalSkipReason.PermissionDenied;
			}

			return RemovalSkipReason.None;
		}
	}
}
