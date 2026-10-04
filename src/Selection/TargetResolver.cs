using System;
using Dismantleheim.Core;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Dismantleheim.Selection
{
	internal static class TargetResolver
	{
		/// <summary>
		/// World UID + peer/session epoch so reconnects do not reuse prior queue identities.
		/// </summary>
		public static string CurrentWorldSessionKey()
		{
			if (ZNet.instance == null)
			{
				return "local-offline";
			}

			long worldUid = ZNet.instance.GetWorldUID();
			long peer = ZNet.GetUID();
			return "uid:" + worldUid + "|peer:" + peer;
		}

		public static bool TryResolveHover(Player player, out TargetIdentity identity, out string rejectDetail)
		{
			identity = null;
			rejectDetail = null;
			if ((UObject)(object)player == (UObject)null)
			{
				rejectDetail = "no-player";
				return false;
			}

			GameObject hover = player.GetHoverObject();
			if ((UObject)(object)hover == (UObject)null)
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
			if ((UObject)(object)go == (UObject)null)
			{
				rejectDetail = "null";
				return false;
			}

			ZNetView view = go.GetComponentInParent<ZNetView>();
			if ((UObject)(object)view == (UObject)null || !view.IsValid())
			{
				rejectDetail = "no-znetview";
				return false;
			}

			GameObject root = view.gameObject;
			Piece piece = root.GetComponent<Piece>();
			bool hasPiece = (UObject)(object)piece != (UObject)null;
			bool canRemove = hasPiece && piece.m_canBeRemoved;
			bool isEnvironment = IsEnvironmentRoot(root);
			WearNTear wear = root.GetComponent<WearNTear>();
			bool hasScopePreview = (UObject)(object)wear != (UObject)null;
			TargetKind kind = Eligibility.Classify(hasPiece, canRemove, isEnvironment, hasScopePreview);

			if (!hasPiece && !isEnvironment)
			{
				kind = TargetKind.Unsupported;
			}

			// Environment without WearNTear cannot show whole-object highlight — unsupported.
			if (isEnvironment && !hasScopePreview)
			{
				kind = TargetKind.Unsupported;
				rejectDetail = "env-no-scope-preview";
			}

			string prefab = StripClone(root.name ?? string.Empty);
			string session = CurrentWorldSessionKey();

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
				kind,
				hasScopePreview,
				isStale: false);

			// Still return identity for diagnostics; eligibility will reject Unsupported.
			return true;
		}

		public static bool TryRevalidate(TargetIdentity queued, out TargetIdentity live, out string detail)
		{
			live = null;
			detail = null;
			if (queued == null)
			{
				detail = "null";
				return false;
			}

			if (!string.Equals(queued.WorldSessionKey, CurrentWorldSessionKey(), StringComparison.Ordinal))
			{
				detail = "session-mismatch";
				return false;
			}

			GameObject go = Removal.ExactObjectExecutor.ResolveInstance(queued);
			if ((UObject)(object)go == (UObject)null)
			{
				detail = "missing-instance";
				return false;
			}

			if (!TryResolveObject(go, out live, out detail))
			{
				return false;
			}

			if (!string.Equals(live.PrefabName, queued.PrefabName, StringComparison.OrdinalIgnoreCase))
			{
				detail = "prefab-mismatch";
				live = null;
				return false;
			}

			if (live.Kind != queued.Kind)
			{
				detail = "kind-mismatch";
				live = null;
				return false;
			}

			if (!live.HasScopePreview)
			{
				detail = "no-scope-preview";
				live = null;
				return false;
			}

			return true;
		}

		public static bool IsEnvironmentRoot(GameObject root)
		{
			if ((UObject)(object)root == (UObject)null)
			{
				return false;
			}

			if ((UObject)(object)root.GetComponent<TreeBase>() != (UObject)null)
			{
				return true;
			}

			if ((UObject)(object)root.GetComponent<TreeLog>() != (UObject)null)
			{
				return true;
			}

			if ((UObject)(object)root.GetComponent<MineRock>() != (UObject)null)
			{
				return true;
			}

			if ((UObject)(object)root.GetComponent<MineRock5>() != (UObject)null)
			{
				return true;
			}

			return false;
		}

		public static string StripClone(string prefab)
		{
			const string clone = "(Clone)";
			if (!string.IsNullOrEmpty(prefab) && prefab.EndsWith(clone))
			{
				return prefab.Substring(0, prefab.Length - clone.Length).Trim();
			}

			return prefab ?? string.Empty;
		}
	}
}
