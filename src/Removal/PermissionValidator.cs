using Dismantleheim.Core;
using Dismantleheim.Selection;
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

			if (!string.Equals(target.WorldSessionKey, TargetResolver.CurrentWorldSessionKey(), System.StringComparison.Ordinal))
			{
				message = "session mismatch";
				return RemovalSkipReason.Stale;
			}

			if (RemovalPlan.LooksLikeWildcard(target.PrefabName))
			{
				message = "wildcard rejected";
				return RemovalSkipReason.WildcardRejected;
			}

			if (target.Kind == TargetKind.Unsupported)
			{
				message = "unsupported target kind";
				return RemovalSkipReason.PermissionDenied;
			}

			if (!target.HasScopePreview)
			{
				message = "no whole-object preview scope";
				return RemovalSkipReason.PermissionDenied;
			}

			GameObject go = ExactObjectExecutor.ResolveInstance(target);
			if ((Object)(object)go == (Object)null)
			{
				message = "instance missing";
				return RemovalSkipReason.Stale;
			}

			Vector3 pos = go.transform.position;
			if (!PrivateArea.CheckAccess(pos, 0f, flash: true, wardCheck: false))
			{
				message = "ward/private area denied";
				return RemovalSkipReason.Ward;
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

			PrivateArea wardComponent = go.GetComponent<PrivateArea>();
			if ((Object)(object)wardComponent != (Object)null)
			{
				message = "ward object";
				return RemovalSkipReason.Ward;
			}

			if (target.Kind == TargetKind.Environment)
			{
				bool isServer = ZNet.instance != null && ZNet.instance.IsServer();
				if (!isServer)
				{
					message = "environment requires host authority";
					return RemovalSkipReason.UnsupportedEnvironment;
				}
			}

			Piece piece = go.GetComponent<Piece>();
			Player player = Player.m_localPlayer;
			if ((Object)(object)piece != (Object)null)
			{
				if (!piece.m_canBeRemoved)
				{
					message = "m_canBeRemoved=false";
					return RemovalSkipReason.PermissionDenied;
				}

				if ((Object)(object)player != (Object)null && !PlayerRemoveAccess.CanRemovePiece(player, piece))
				{
					message = "CheckCanRemovePiece=false";
					return RemovalSkipReason.PermissionDenied;
				}
			}
			else if (target.Kind == TargetKind.BuildPiece)
			{
				message = "expected piece missing";
				return RemovalSkipReason.Stale;
			}

			return RemovalSkipReason.None;
		}
	}
}
