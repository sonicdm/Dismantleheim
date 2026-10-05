using System.Collections.Generic;
using Dismantleheim.Core;
using UnityEngine;

namespace Dismantleheim.Removal
{
	internal static class ExactObjectExecutor
	{
		public static List<RemovalResult> Execute(OperationMode mode, List<RemovalPlanEntry> plan)
		{
			var results = new List<RemovalResult>();
			bool dry = DismantleheimPlugin.DryRunOnly != null && DismantleheimPlugin.DryRunOnly.Value;
			if (plan == null)
			{
				return results;
			}

			foreach (RemovalPlanEntry entry in plan)
			{
				if (entry == null || entry.Target == null)
				{
					continue;
				}

				if (entry.SkipReason != RemovalSkipReason.None)
				{
					results.Add(new RemovalResult(
						entry.Target,
						false,
						entry.SkipReason,
						entry.Message,
						entry.DropPolicy,
						mode));
					continue;
				}

				if (dry)
				{
					results.Add(new RemovalResult(
						entry.Target,
						false,
						RemovalSkipReason.DryRun,
						"dry-run " + entry.Message,
						entry.DropPolicy,
						mode));
					continue;
				}

				bool requested;
				string err;
				try
				{
					if (mode == OperationMode.MassDismantle)
					{
						requested = VanillaPieceRemovalAdapter.TryRemove(entry.Target, out err);
					}
					else
					{
						requested = AuthorizedExactDeletionAdapter.TryDelete(entry.Target, out err);
					}
				}
				catch (System.Exception ex)
				{
					results.Add(new RemovalResult(
						entry.Target,
						false,
						RemovalSkipReason.PermissionDenied,
						"adapter exception: " + ex.Message,
						DropPolicy.None,
						mode));
					DismantleheimPlugin.ModLogger?.LogWarning(
						"Dismantleheim remove exception on " + entry.Target.PrefabName + ": " + ex.Message);
					continue;
				}

				if (!requested)
				{
					results.Add(new RemovalResult(
						entry.Target,
						false,
						err != null && err.StartsWith("Unsafe")
							? RemovalSkipReason.UnsafeDelete
							: RemovalSkipReason.PermissionDenied,
						err,
						DropPolicy.None,
						mode));
					continue;
				}

				// Only report Removed when the instance is already gone (synchronous confirm).
				// Async RPC leave PendingNetwork — queue is not cleared for those.
				GameObject after = ResolveInstance(entry.Target);
				if ((Object)(object)after == (Object)null)
				{
					results.Add(new RemovalResult(
						entry.Target,
						true,
						RemovalSkipReason.None,
						"instance cleared",
						entry.DropPolicy,
						mode));
				}
				else
				{
					results.Add(new RemovalResult(
						entry.Target,
						false,
						RemovalSkipReason.PendingNetwork,
						"destroy requested; instance still present",
						entry.DropPolicy,
						mode));
				}
			}

			return results;
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
