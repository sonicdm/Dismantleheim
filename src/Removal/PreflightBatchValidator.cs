using System.Collections.Generic;
using Dismantleheim.Core;
using Dismantleheim.Selection;
using UnityEngine;

namespace Dismantleheim.Removal
{
	internal static class PreflightBatchValidator
	{
		public static List<RemovalPlanEntry> BuildPlan(
			OperationMode mode,
			IReadOnlyList<TargetIdentity> queued,
			bool allowEnvironmentWithFilter,
			ISet<string> extraDeny,
			ISet<string> extraAllow,
			out List<TargetIdentity> missing)
		{
			var plan = new List<RemovalPlanEntry>();
			missing = new List<TargetIdentity>();
			IRemovalPolicy policy = RemovalPolicyFactory.For(mode);

			if (queued == null)
			{
				return plan;
			}

			foreach (TargetIdentity queuedId in queued)
			{
				if (queuedId == null)
				{
					continue;
				}

				if (RemovalPlan.LooksLikeWildcard(queuedId.PrefabName))
				{
					plan.Add(new RemovalPlanEntry(
						queuedId,
						mode,
						DropPolicy.None,
						RemovalSkipReason.WildcardRejected,
						"wildcard",
						false));
					continue;
				}

				if (!TargetResolver.TryRevalidate(queuedId, out TargetIdentity live, out string detail))
				{
					missing.Add(queuedId);
					plan.Add(new RemovalPlanEntry(
						queuedId,
						mode,
						DropPolicy.None,
						RemovalSkipReason.Stale,
						detail ?? "missing",
						false));
					continue;
				}

				bool contentsWarn = false;
				GameObject go = ExactObjectExecutor.ResolveInstance(live);
				if ((Object)(object)go != (Object)null)
				{
					contentsWarn = ContainerContentsInspector.HasStoredItems(go, out _);
				}

				RemovalPlanEntry entry = policy.PlanEntry(
					live,
					allowEnvironmentWithFilter,
					extraDeny,
					extraAllow,
					contentsWarn && mode == OperationMode.MassDelete);

				// Permission gate (ward / portal / etc.) — mode policies still apply.
				if (entry.SkipReason == RemovalSkipReason.None)
				{
					RemovalSkipReason perm = PermissionValidator.ValidateForMode(live, mode, out string msg);
					if (perm != RemovalSkipReason.None)
					{
						entry = new RemovalPlanEntry(live, mode, DropPolicy.None, perm, msg, entry.ContentsLossWarning);
					}
				}

				plan.Add(entry);
			}

			return plan;
		}
	}
}
