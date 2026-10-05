using Dismantleheim.Core;
using UnityEngine;

namespace Dismantleheim.Removal
{
	/// <summary>
	/// Mass Dismantle live path: mirrors Player.RemovePiece refund semantics without raycast.
	/// Order observed in IL: WearNTear.Remove(bool) then Piece.DropResources(HitData).
	/// </summary>
	internal static class VanillaPieceRemovalAdapter
	{
		public static bool TryRemove(TargetIdentity target, out string error)
		{
			error = string.Empty;
			GameObject go = ExactObjectExecutor.ResolveInstance(target);
			if ((Object)(object)go == (Object)null)
			{
				error = "instance missing";
				return false;
			}

			Piece piece = go.GetComponent<Piece>();
			if ((Object)(object)piece == (Object)null)
			{
				error = "no Piece";
				return false;
			}

			Player player = Player.m_localPlayer;
			if ((Object)(object)player == (Object)null)
			{
				error = "no player";
				return false;
			}

			Vector3 pos = go.transform.position;
			if (!PrivateArea.CheckAccess(pos, 0f, flash: true, wardCheck: false))
			{
				error = "ward denied";
				return false;
			}

			if (!PlayerRemoveAccess.CanRemovePiece(player, piece))
			{
				error = "CheckCanRemovePiece=false";
				return false;
			}

			if (!piece.m_canBeRemoved || !piece.CanBeRemoved())
			{
				error = ContainerContentsInspector.VanillaOccupiedChestRefusal(go)
					? "occupied container (vanilla)"
					: "Piece.CanBeRemoved=false";
				return false;
			}

			// Match Player.RemovePiece: IRemoved hook, then WearNTear.Remove RPC.
			// Do NOT call Piece.DropResources after Remove — WNT path drops via RPC;
			// calling DropResources on a destroyed/invalid piece NREs (live commit crash).
			IRemoved removedHook = go.GetComponent<IRemoved>();
			if (removedHook != null)
			{
				removedHook.OnRemoved();
			}

			WearNTear wear = go.GetComponent<WearNTear>();
			if ((Object)(object)wear == (Object)null)
			{
				error = "no WearNTear";
				return false;
			}

			wear.Remove(false);
			return true;
		}
	}
}
