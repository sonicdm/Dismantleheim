using System.Collections.Generic;
using Dismantleheim.Core;
using UnityEngine;

namespace Dismantleheim.Removal
{
	internal static class ExactObjectExecutor
	{
		public static List<RemovalResult> Execute(RemovalPlan plan, List<TargetIdentity> preSkipped)
		{
			var results = new List<RemovalResult>();
			if (preSkipped != null)
			{
				foreach (TargetIdentity s in preSkipped)
				{
					results.Add(new RemovalResult(s, false, RemovalSkipReason.Stale, "pre-validation skip"));
				}
			}

			bool dry = DismantleheimPlugin.DryRunOnly == null || DismantleheimPlugin.DryRunOnly.Value;
			if (plan == null)
			{
				return results;
			}

			foreach (TargetIdentity target in plan.Targets)
			{
				RemovalSkipReason reason = PermissionValidator.Validate(target, out string message);
				if (reason != RemovalSkipReason.None)
				{
					results.Add(new RemovalResult(target, false, reason, message));
					continue;
				}

				if (dry)
				{
					results.Add(new RemovalResult(target, false, RemovalSkipReason.DryRun, "dry-run plan member"));
					continue;
				}

				bool ok = TryRemoveAuthorized(target, out string err);
				results.Add(ok
					? new RemovalResult(target, true, RemovalSkipReason.None, "removed")
					: new RemovalResult(target, false, RemovalSkipReason.PermissionDenied, err));
			}

			return results;
		}

		/// <summary>
		/// Authorized exact remove: re-check Player.CheckCanRemovePiece, then WearNTear.Remove
		/// (network destroy path used by hammer remove). Does not claim Player.RemovePiece() was called.
		/// </summary>
		private static bool TryRemoveAuthorized(TargetIdentity target, out string error)
		{
			error = string.Empty;
			GameObject go = ResolveInstance(target);
			if ((Object)(object)go == (Object)null)
			{
				error = "instance not found at commit";
				return false;
			}

			Player player = Player.m_localPlayer;
			Piece piece = go.GetComponent<Piece>();
			if ((Object)(object)piece != (Object)null)
			{
				if ((Object)(object)player == (Object)null || !PlayerRemoveAccess.CanRemovePiece(player, piece))
				{
					error = "CheckCanRemovePiece failed at commit";
					return false;
				}
			}
			else if (target.Kind != TargetKind.Environment)
			{
				error = "not a supported piece/environment";
				return false;
			}

			WearNTear wear = go.GetComponent<WearNTear>();
			if ((Object)(object)wear == (Object)null)
			{
				// No preview-capable destroy path — refuse (no generic Destroy fallback).
				error = "no WearNTear scope; refused";
				return false;
			}

			wear.Remove();
			// Network destroy may complete asynchronously; observer/server confirmation is an acceptance gate.
			return true;
		}

		internal static GameObject ResolveInstance(TargetIdentity target)
		{
			if (target == null || ZDOMan.instance == null || ZNetScene.instance == null)
			{
				return null;
			}

			ZDOID zdoid = new ZDOID(target.UserId, target.ObjectId);
			ZDO zdo = ZDOMan.instance.GetZDO(zdoid);
			if (zdo == null)
			{
				return null;
			}

			ZNetView view = ZNetScene.instance.FindInstance(zdo);
			if ((Object)(object)view == (Object)null)
			{
				return null;
			}

			return view.gameObject;
		}
	}
}
