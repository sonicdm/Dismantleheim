using Dismantleheim.Core;
using UnityEngine;

namespace Dismantleheim.Removal
{
	/// <summary>
	/// Mass Delete live path: WearNTear.Remove without DropResources / harvest loot.
	/// </summary>
	internal static class AuthorizedExactDeletionAdapter
	{
		public static bool TryDelete(TargetIdentity target, out string error)
		{
			error = string.Empty;
			GameObject go = ExactObjectExecutor.ResolveInstance(target);
			if ((Object)(object)go == (Object)null)
			{
				error = "instance missing";
				return false;
			}

			Vector3 pos = go.transform.position;
			if (!PrivateArea.CheckAccess(pos, 0f, flash: true, wardCheck: false))
			{
				error = "ward denied";
				return false;
			}

			if ((Object)(object)go.GetComponent<TeleportWorld>() != (Object)null)
			{
				error = "portal unprotected lifecycle";
				return false;
			}

			if ((Object)(object)go.GetComponent<PrivateArea>() != (Object)null)
			{
				error = "ward object";
				return false;
			}

			Piece piece = go.GetComponent<Piece>();
			Player player = Player.m_localPlayer;
			if ((Object)(object)piece != (Object)null)
			{
				if (!piece.m_canBeRemoved)
				{
					error = "m_canBeRemoved=false";
					return false;
				}

				// Mass Delete does not require crafting-station range (that gate is for refund dismantle).
			}
			else if (target.Kind != TargetKind.Environment && target.Kind != TargetKind.Unsupported)
			{
				error = "unsupported type";
				return false;
			}

			if (target.Kind == TargetKind.Environment || target.Kind == TargetKind.Unsupported)
			{
				bool isServer = ZNet.instance != null && ZNet.instance.IsServer();
				if (!isServer)
				{
					error = "environment requires host";
					return false;
				}
			}

			WearNTear wear = go.GetComponent<WearNTear>();
			if ((Object)(object)wear == (Object)null)
			{
				wear = go.GetComponentInChildren<WearNTear>();
			}

			if ((Object)(object)wear != (Object)null)
			{
				// blockDrop=true skips Piece.DropResources inside WearNTear.Destroy.
				wear.Remove(true);
				return true;
			}

			// World props / rubble: Destructible.DestroyNow (no WearNTear).
			Destructible destructible = go.GetComponentInChildren<Destructible>(true);
			if ((Object)(object)destructible != (Object)null)
			{
				destructible.DestroyNow();
				return true;
			}

			// Trees/rocks/props often have no WearNTear — host exact destroy, no fabricated harvest.
			if ((target.Kind == TargetKind.Environment || target.Kind == TargetKind.Unsupported)
			    && ZNetScene.instance != null
			    && ZNet.instance != null
			    && ZNet.instance.IsServer())
			{
				ZNetScene.instance.Destroy(go);
				return true;
			}

			error = "Unsafe delete: no WearNTear/Destructible";
			return false;
		}
	}
}
