using System;
using System.Reflection;
using Dismantleheim.Core;
using HarmonyLib;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Dismantleheim.Selection
{
	internal static class TargetResolver
	{
		private static readonly FieldInfo RemoveRayMaskField =
			AccessTools.Field(typeof(Player), "m_removeRayMask");
		private static readonly FieldInfo MaxPlaceDistanceField =
			AccessTools.Field(typeof(Player), "m_maxPlaceDistance");

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

			// Build pieces: GetHoveringPiece. Environment (trees/rocks): often not a Piece — aim ray.
			GameObject hover = null;
			if (player.InPlaceMode())
			{
				Piece hoveringPiece = player.GetHoveringPiece();
				if ((UObject)(object)hoveringPiece != (UObject)null)
				{
					hover = hoveringPiece.gameObject;
				}
			}

			if ((UObject)(object)hover == (UObject)null)
			{
				hover = player.GetHoverObject();
			}

			if ((UObject)(object)hover != (UObject)null && !Player.IsPlacementGhost(hover)
			    && TryResolveObject(hover, out identity, out rejectDetail)
			    && identity != null
			    && identity.Kind != TargetKind.Unsupported)
			{
				return true;
			}

			// Trees/rocks / missed Piece hover: fresh remove-mask ray including environment roots.
			return TryResolveAim(player, out identity, out rejectDetail);
		}

		/// <summary>
		/// Fresh camera ray (Player remove mask). Resolves build Pieces and environment roots
		/// (TreeBase / MineRock / …) so Shift+M3 sample and Ctrl-paint work on sampled env targets.
		/// </summary>
		public static bool TryResolveAim(Player player, out TargetIdentity identity, out string rejectDetail)
		{
			identity = null;
			rejectDetail = null;
			if ((UObject)(object)player == (UObject)null)
			{
				rejectDetail = "no-player";
				return false;
			}

			if ((UObject)(object)GameCamera.instance == (UObject)null)
			{
				rejectDetail = "no-camera";
				return false;
			}

			Transform cam = GameCamera.instance.transform;
			int mask = RemoveRayMaskField != null ? (int)RemoveRayMaskField.GetValue(player) : 0;
			float maxDist = MaxPlaceDistanceField != null
				? (float)MaxPlaceDistanceField.GetValue(player)
				: 5f;

			if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 50f, mask))
			{
				rejectDetail = "aim-miss";
				return false;
			}

			if ((UObject)(object)player.m_eye != (UObject)null)
			{
				float eyeDist = Vector3.Distance(player.m_eye.position, hit.point);
				if (eyeDist >= maxDist)
				{
					rejectDetail = "aim-range";
					return false;
				}
			}

			GameObject root = ResolveRootFromCollider(hit.collider);
			if ((UObject)(object)root == (UObject)null)
			{
				rejectDetail = "aim-no-target";
				return false;
			}

			if (Player.IsPlacementGhost(root))
			{
				rejectDetail = "placement-ghost";
				return false;
			}

			return TryResolveObject(root, out identity, out rejectDetail);
		}

		private static GameObject ResolveRootFromCollider(Collider col)
		{
			if ((UObject)(object)col == (UObject)null)
			{
				return null;
			}

			ZNetView view = col.GetComponentInParent<ZNetView>();
			if ((UObject)(object)view != (UObject)null && view.IsValid())
			{
				return view.gameObject;
			}

			Piece piece = col.GetComponentInParent<Piece>();
			if ((UObject)(object)piece != (UObject)null)
			{
				return piece.gameObject;
			}

			TreeBase tree = col.GetComponentInParent<TreeBase>();
			if ((UObject)(object)tree != (UObject)null)
			{
				return tree.gameObject;
			}

			TreeLog log = col.GetComponentInParent<TreeLog>();
			if ((UObject)(object)log != (UObject)null)
			{
				return log.gameObject;
			}

			MineRock rock = col.GetComponentInParent<MineRock>();
			if ((UObject)(object)rock != (UObject)null)
			{
				return rock.gameObject;
			}

			MineRock5 rock5 = col.GetComponentInParent<MineRock5>();
			if ((UObject)(object)rock5 != (UObject)null)
			{
				return rock5.gameObject;
			}

			return null;
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
			if ((UObject)(object)piece == (UObject)null)
			{
				piece = root.GetComponentInChildren<Piece>();
			}

			bool hasPiece = (UObject)(object)piece != (UObject)null;
			bool canRemove = hasPiece && piece.m_canBeRemoved;
			bool isEnvironment = IsEnvironmentRoot(root);
			WearNTear wear = root.GetComponent<WearNTear>();
			if ((UObject)(object)wear == (UObject)null)
			{
				wear = root.GetComponentInChildren<WearNTear>();
			}

			// Trees often lack WearNTear; Mass Delete + sample still allows them (highlight best-effort).
			bool hasScopePreview = (UObject)(object)wear != (UObject)null || isEnvironment;
			TargetKind kind = Eligibility.Classify(hasPiece, canRemove, isEnvironment, hasScopePreview);

			if (!hasPiece && !isEnvironment)
			{
				kind = TargetKind.Unsupported;
			}

			string prefab = NormalizePrefabName(root.name ?? string.Empty);

			// Name-based fallback when components sit oddly (Rock_3, prop_*, etc.).
			if (!isEnvironment && LooksLikeEnvironmentPrefab(prefab))
			{
				isEnvironment = true;
				hasScopePreview = true;
				kind = TargetKind.Environment;
			}

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

			// Environment/props may lack WearNTear; commit still allowed for Mass Delete.
			if (!live.HasScopePreview && live.Kind != TargetKind.Environment)
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

			// Components may sit on children of the ZNetView root (e.g. Birch2 / prop_*).
			if ((UObject)(object)root.GetComponentInChildren<TreeBase>(true) != (UObject)null)
			{
				return true;
			}

			if ((UObject)(object)root.GetComponentInChildren<TreeLog>(true) != (UObject)null)
			{
				return true;
			}

			if ((UObject)(object)root.GetComponentInChildren<MineRock>(true) != (UObject)null)
			{
				return true;
			}

			if ((UObject)(object)root.GetComponentInChildren<MineRock5>(true) != (UObject)null)
			{
				return true;
			}

			// World props (prop_ashwood_bed, rubble, …): Destructible, usually no Piece.
			if ((UObject)(object)root.GetComponentInChildren<Destructible>(true) != (UObject)null)
			{
				return true;
			}

			return LooksLikeEnvironmentPrefab(NormalizePrefabName(root.name ?? string.Empty));
		}

		public static bool LooksLikeEnvironmentPrefab(string prefab)
		{
			if (string.IsNullOrEmpty(prefab))
			{
				return false;
			}

			if (prefab.StartsWith("prop_", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			// Common world rocks / debris without a stable component layout on the ZNetView root.
			if (prefab.StartsWith("Rock_", StringComparison.OrdinalIgnoreCase)
			    || prefab.StartsWith("rock_", StringComparison.OrdinalIgnoreCase)
			    || prefab.StartsWith("MineRock", StringComparison.OrdinalIgnoreCase)
			    || prefab.StartsWith("stone_environment", StringComparison.OrdinalIgnoreCase)
			    || prefab.StartsWith("Cliff", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return false;
		}

		public static string StripClone(string prefab)
		{
			return NormalizePrefabName(prefab);
		}

		/// <summary>
		/// Prefab identity for filters: strip "(Clone)" and Unity instance suffixes like " (1)".
		/// </summary>
		public static string NormalizePrefabName(string prefab)
		{
			if (string.IsNullOrEmpty(prefab))
			{
				return string.Empty;
			}

			string name = prefab.Trim();
			const string clone = "(Clone)";
			if (name.EndsWith(clone, StringComparison.Ordinal))
			{
				name = name.Substring(0, name.Length - clone.Length).TrimEnd();
			}

			// "iron_wall_2x2 (12)" → "iron_wall_2x2"
			int open = name.LastIndexOf('(');
			int close = name.LastIndexOf(')');
			if (open > 0 && close == name.Length - 1 && close > open + 1)
			{
				bool digits = true;
				for (int i = open + 1; i < close; i++)
				{
					if (!char.IsDigit(name[i]))
					{
						digits = false;
						break;
					}
				}

				if (digits)
				{
					name = name.Substring(0, open).TrimEnd();
				}
			}

			return name;
		}
	}
}
