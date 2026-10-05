using System;
using System.Collections.Generic;
using System.Linq;

namespace Dismantleheim.Core
{
	public sealed class RemovalPlan
	{
		private readonly List<TargetIdentity> _targets;

		private RemovalPlan(IEnumerable<TargetIdentity> targets)
		{
			_targets = targets.ToList();
		}

		public IReadOnlyList<TargetIdentity> Targets => _targets;

		public int Count => _targets.Count;

		/// <summary>
		/// Build an immutable plan from exact queued identities only.
		/// Rejects wildcard descriptors; skips stale/invalid via optional predicate.
		/// </summary>
		public static RemovalPlan FromQueue(
			IEnumerable<TargetIdentity> queued,
			Func<TargetIdentity, bool> isStillValid,
			out List<TargetIdentity> skipped)
		{
			skipped = new List<TargetIdentity>();
			var kept = new List<TargetIdentity>();
			if (queued == null)
			{
				return new RemovalPlan(kept);
			}

			foreach (TargetIdentity id in queued)
			{
				if (id == null)
				{
					continue;
				}

				if (LooksLikeWildcard(id.PrefabName))
				{
					skipped.Add(id);
					continue;
				}

				if (isStillValid != null && !isStillValid(id))
				{
					skipped.Add(id);
					continue;
				}

				kept.Add(id);
			}

			return new RemovalPlan(kept);
		}

		public static bool LooksLikeWildcard(string prefabOrDescriptor)
		{
			if (string.IsNullOrEmpty(prefabOrDescriptor))
			{
				return false;
			}

			string s = prefabOrDescriptor;
			if (s.IndexOf("id=*", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			if (s.IndexOf("*", StringComparison.Ordinal) >= 0 && s.IndexOf("remove", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}

			return false;
		}
	}

	public enum RemovalSkipReason
	{
		None,
		Stale,
		PermissionDenied,
		ContainerWithContents,
		Portal,
		Ward,
		UnsupportedEnvironment,
		WildcardRejected,
		DryRun,
		UnsafeDelete,
		ModeIneligible,
		/// <summary>Destroy RPC issued; instance still present — not counted as Removed.</summary>
		PendingNetwork
	}

	public sealed class RemovalPlanEntry
	{
		public RemovalPlanEntry(
			TargetIdentity target,
			OperationMode mode,
			DropPolicy dropPolicy,
			RemovalSkipReason skipReason,
			string message,
			bool contentsLossWarning)
		{
			Target = target;
			Mode = mode;
			DropPolicy = dropPolicy;
			SkipReason = skipReason;
			Message = message ?? string.Empty;
			ContentsLossWarning = contentsLossWarning;
		}

		public TargetIdentity Target { get; }

		public OperationMode Mode { get; }

		public DropPolicy DropPolicy { get; }

		public RemovalSkipReason SkipReason { get; }

		public string Message { get; }

		public bool ContentsLossWarning { get; }

		public bool IsExecutable => SkipReason == RemovalSkipReason.None || SkipReason == RemovalSkipReason.DryRun;
	}

	public sealed class RemovalResult
	{
		public RemovalResult(
			TargetIdentity target,
			bool removed,
			RemovalSkipReason skipReason,
			string message,
			DropPolicy dropPolicy = DropPolicy.None,
			OperationMode mode = OperationMode.MassDismantle)
		{
			Target = target;
			Removed = removed;
			SkipReason = skipReason;
			Message = message ?? string.Empty;
			DropPolicy = dropPolicy;
			Mode = mode;
		}

		public TargetIdentity Target { get; }

		public bool Removed { get; }

		public RemovalSkipReason SkipReason { get; }

		public string Message { get; }

		public DropPolicy DropPolicy { get; }

		public OperationMode Mode { get; }
	}
}
