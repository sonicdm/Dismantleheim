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

				bool ok = TryRemove(target, out string err);
				if (ok)
				{
					results.Add(new RemovalResult(target, true, RemovalSkipReason.None, "removed"));
				}
				else
				{
					results.Add(new RemovalResult(target, false, RemovalSkipReason.PermissionDenied, err));
				}
			}

			return results;
		}

		private static bool TryRemove(TargetIdentity target, out string error)
		{
			error = string.Empty;
			GameObject go = ResolveInstance(target);
			if ((Object)(object)go == (Object)null)
			{
				error = "instance not found at commit";
				return false;
			}

			Piece piece = go.GetComponent<Piece>();
			if ((Object)(object)piece != (Object)null && !piece.m_canBeRemoved)
			{
				error = "m_canBeRemoved=false";
				return false;
			}

			WearNTear wear = go.GetComponent<WearNTear>();
			if ((Object)(object)wear != (Object)null)
			{
				wear.Remove();
				return true;
			}

			if (ZNetScene.instance != null)
			{
				ZNetScene.instance.Destroy(go);
				return true;
			}

			error = "no WearNTear / ZNetScene";
			return false;
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
