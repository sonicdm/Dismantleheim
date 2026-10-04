using Dismantleheim.Core;
using UnityEngine;

namespace Dismantleheim.Selection
{
	internal static class TargetResolver
	{
		public static bool TryResolveHover(Player player, out TargetIdentity identity, out string rejectDetail)
		{
			identity = null;
			rejectDetail = null;
			if ((Object)(object)player == (Object)null)
			{
				rejectDetail = "no-player";
				return false;
			}

			GameObject hover = player.GetHoverObject();
			if ((Object)(object)hover == (Object)null)
			{
				rejectDetail = "no-hover";
				return false;
			}

			return TryResolveObject(hover, out identity, out rejectDetail);
		}

		public static bool TryResolveObject(GameObject go, out TargetIdentity identity, out string rejectDetail)
		{
			identity = null;
			rejectDetail = null;
			if ((Object)(object)go == (Object)null)
			{
				rejectDetail = "null";
				return false;
			}

			ZNetView view = go.GetComponentInParent<ZNetView>();
			if ((Object)(object)view == (Object)null || !view.IsValid())
			{
				rejectDetail = "no-znetview";
				return false;
			}

			GameObject root = view.gameObject;
			Piece piece = root.GetComponent<Piece>();
			bool hasPiece = (Object)(object)piece != (Object)null;
			bool canRemove = hasPiece && piece.m_canBeRemoved;
			bool isEnvironment = IsEnvironmentRoot(root);

			string prefab = root.name ?? string.Empty;
			const string clone = "(Clone)";
			if (prefab.EndsWith(clone))
			{
				prefab = prefab.Substring(0, prefab.Length - clone.Length).Trim();
			}

			string session = "local";
			if (ZNet.instance != null)
			{
				session = ZNet.instance.GetWorldName() ?? "local";
			}

			long userId = 0;
			uint objectId = 0;
			ZDO zdo = view.GetZDO();
			if (zdo != null)
			{
				ZDOID uid = zdo.m_uid;
				userId = uid.UserID;
				objectId = uid.ID;
			}

			identity = new TargetIdentity(
				userId,
				objectId,
				session,
				prefab,
				hasPiece,
				canRemove,
				isEnvironment,
				isStale: false);
			return true;
		}

		public static bool IsEnvironmentRoot(GameObject root)
		{
			if ((Object)(object)root == (Object)null)
			{
				return false;
			}

			if ((Object)(object)root.GetComponent<TreeBase>() != (Object)null)
			{
				return true;
			}

			if ((Object)(object)root.GetComponent<TreeLog>() != (Object)null)
			{
				return true;
			}

			if ((Object)(object)root.GetComponent<MineRock>() != (Object)null)
			{
				return true;
			}

			if ((Object)(object)root.GetComponent<MineRock5>() != (Object)null)
			{
				return true;
			}

			return false;
		}

		public static TargetIdentity MarkStale(TargetIdentity id)
		{
			if (id == null)
			{
				return null;
			}

			return new TargetIdentity(
				id.UserId,
				id.ObjectId,
				id.WorldSessionKey,
				id.PrefabName,
				id.HasPiece,
				id.CanBeRemoved,
				id.IsEnvironment,
				isStale: true);
		}
	}
}
