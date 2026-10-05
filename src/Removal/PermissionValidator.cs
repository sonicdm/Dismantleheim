using Dismantleheim.Core;
using Dismantleheim.Selection;
using UnityEngine;

namespace Dismantleheim.Removal
{
	internal static class PermissionValidator
	{
		public static RemovalSkipReason Validate(TargetIdentity target, out string message)
		{
			return ValidateForMode(target, OperationMode.MassDismantle, out message);
		}

		public static RemovalSkipReason ValidateForMode(TargetIdentity target, OperationMode mode, out string message)
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

			bool sampledWorldObject = target.Kind == TargetKind.Environment
			                          || target.Kind == TargetKind.Unsupported;

			if (mode == OperationMode.MassDismantle && sampledWorldObject)
			{
				message = "environment/props not dismantleable (use Mass Delete + sample)";
				return RemovalSkipReason.UnsupportedEnvironment;
			}

			if (target.Kind == TargetKind.Unsupported && mode != OperationMode.MassDelete)
			{
				message = "unsupported target kind";
				return RemovalSkipReason.PermissionDenied;
			}

			// Sampled props/trees may lack WearNTear highlight scope.
			if (!target.HasScopePreview && !sampledWorldObject)
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

			if (sampledWorldObject)
			{
				bool isServer = ZNet.instance != null && ZNet.instance.IsServer();
				if (!isServer)
				{
					message = "environment/props require host authority";
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

				// Crafting-station range is a Mass Dismantle (refund) rule only.
				if (mode == OperationMode.MassDismantle
				    && (Object)(object)player != (Object)null
				    && !PlayerRemoveAccess.CanRemovePiece(player, piece))
				{
					message = "missing crafting station in range (vanilla)";
					return RemovalSkipReason.PermissionDenied;
				}

				if (mode == OperationMode.MassDismantle && !piece.CanBeRemoved())
				{
					message = ContainerContentsInspector.VanillaOccupiedChestRefusal(go)
						? "occupied container (vanilla)"
						: "Piece.CanBeRemoved=false";
					return ContainerContentsInspector.HasStoredItems(go, out _)
						? RemovalSkipReason.ContainerWithContents
						: RemovalSkipReason.PermissionDenied;
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
