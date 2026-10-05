using System;
using System.Collections.Generic;

namespace Dismantleheim.Core
{
	public enum TargetKind
	{
		Unsupported = 0,
		BuildPiece = 1,
		Environment = 2
	}

	public enum EligibilityRejectReason
	{
		None,
		NullTarget,
		Stale,
		UnsupportedType,
		NoPiece,
		NotRemovable,
		EnvironmentWithoutFilter,
		PrefabFilterMismatch,
		ExtraDeny,
		SessionMismatch,
		NoScopePreview
	}

	public static class Eligibility
	{
		/// <summary>
		/// Candidate selection rules. Prefab filter gates what may enter the queue next.
		/// Mass Delete + exact sample authorizes that prefab (including props/trees/Unsupported).
		/// ExtraAllow only overrides ExtraDeny.
		/// </summary>
		public static bool IsEligible(
			TargetIdentity target,
			string prefabFilter,
			bool allowEnvironmentWithFilter,
			ISet<string> extraDeny,
			ISet<string> extraAllow,
			out EligibilityRejectReason reason)
		{
			return IsEligible(
				target,
				OperationMode.MassDelete,
				prefabFilter,
				allowEnvironmentWithFilter,
				extraDeny,
				extraAllow,
				out reason);
		}

		public static bool IsEligible(
			TargetIdentity target,
			OperationMode mode,
			string prefabFilter,
			bool allowEnvironmentWithFilter,
			ISet<string> extraDeny,
			ISet<string> extraAllow,
			out EligibilityRejectReason reason)
		{
			if (!PassesDenyAllow(target, extraDeny, extraAllow, out reason))
			{
				return false;
			}

			bool filterActive = !string.IsNullOrEmpty(prefabFilter);
			bool filterMatches = filterActive
			                     && string.Equals(
				                     target.PrefabName ?? string.Empty,
				                     prefabFilter,
				                     StringComparison.OrdinalIgnoreCase);

			// Mass Delete + explicit sample: that prefab is eligible regardless of Kind
			// (props/rocks often resolve as Unsupported or Environment without WearNTear).
			if (mode == OperationMode.MassDelete && filterMatches && allowEnvironmentWithFilter)
			{
				if (target.Kind == TargetKind.BuildPiece)
				{
					return PassesBuildPieceRemovability(target, out reason);
				}

				reason = EligibilityRejectReason.None;
				return true;
			}

			if (!PassesHardSafety(target, out reason))
			{
				return false;
			}

			// Mass Dismantle: build pieces only — never environment/props (no harvest).
			if (mode == OperationMode.MassDismantle)
			{
				if (target.Kind == TargetKind.Environment)
				{
					reason = EligibilityRejectReason.EnvironmentWithoutFilter;
					return false;
				}

				if (target.Kind != TargetKind.BuildPiece)
				{
					reason = EligibilityRejectReason.UnsupportedType;
					return false;
				}

				if (filterActive && !filterMatches)
				{
					reason = EligibilityRejectReason.PrefabFilterMismatch;
					return false;
				}

				return PassesBuildPieceRemovability(target, out reason);
			}

			// Mass Delete without filter: build pieces only.
			if (filterActive && !filterMatches)
			{
				reason = EligibilityRejectReason.PrefabFilterMismatch;
				return false;
			}

			if (target.Kind == TargetKind.Environment)
			{
				reason = EligibilityRejectReason.EnvironmentWithoutFilter;
				return false;
			}

			if (target.Kind != TargetKind.BuildPiece)
			{
				reason = EligibilityRejectReason.UnsupportedType;
				return false;
			}

			return PassesBuildPieceRemovability(target, out reason);
		}

		/// <summary>
		/// Commit-time checks for already-queued identities.
		/// Does not re-apply the current candidate prefab filter (mixed queues stay valid).
		/// </summary>
		public static bool IsEligibleForCommit(
			TargetIdentity target,
			bool allowEnvironmentWithFilter,
			ISet<string> extraDeny,
			ISet<string> extraAllow,
			out EligibilityRejectReason reason)
		{
			return IsEligibleForCommit(
				target,
				OperationMode.MassDelete,
				allowEnvironmentWithFilter,
				extraDeny,
				extraAllow,
				out reason);
		}

		public static bool IsEligibleForCommit(
			TargetIdentity target,
			OperationMode mode,
			bool allowEnvironmentWithFilter,
			ISet<string> extraDeny,
			ISet<string> extraAllow,
			out EligibilityRejectReason reason)
		{
			if (!PassesDenyAllow(target, extraDeny, extraAllow, out reason))
			{
				return false;
			}

			if (mode == OperationMode.MassDismantle)
			{
				if (!PassesHardSafety(target, out reason))
				{
					return false;
				}

				if (target.Kind != TargetKind.BuildPiece)
				{
					reason = target.Kind == TargetKind.Environment
						? EligibilityRejectReason.EnvironmentWithoutFilter
						: EligibilityRejectReason.UnsupportedType;
					return false;
				}

				return PassesBuildPieceRemovability(target, out reason);
			}

			// Mass Delete commit: Environment or sampled Unsupported world objects (queued via filter).
			if (target.Kind == TargetKind.Environment || target.Kind == TargetKind.Unsupported)
			{
				if (!allowEnvironmentWithFilter)
				{
					reason = EligibilityRejectReason.EnvironmentWithoutFilter;
					return false;
				}

				reason = EligibilityRejectReason.None;
				return true;
			}

			if (target.Kind == TargetKind.BuildPiece)
			{
				if (!PassesHardSafety(target, out reason))
				{
					return false;
				}

				return PassesBuildPieceRemovability(target, out reason);
			}

			reason = EligibilityRejectReason.UnsupportedType;
			return false;
		}

		private static bool PassesDenyAllow(
			TargetIdentity target,
			ISet<string> extraDeny,
			ISet<string> extraAllow,
			out EligibilityRejectReason reason)
		{
			reason = EligibilityRejectReason.None;
			if (target == null)
			{
				reason = EligibilityRejectReason.NullTarget;
				return false;
			}

			if (target.IsStale)
			{
				reason = EligibilityRejectReason.Stale;
				return false;
			}

			string prefab = target.PrefabName ?? string.Empty;
			bool denied = extraDeny != null && extraDeny.Contains(prefab);
			bool allowOverride = extraAllow != null && extraAllow.Contains(prefab);
			if (denied && !allowOverride)
			{
				reason = EligibilityRejectReason.ExtraDeny;
				return false;
			}

			return true;
		}

		private static bool PassesHardSafety(TargetIdentity target, out EligibilityRejectReason reason)
		{
			reason = EligibilityRejectReason.None;

			if (target.Kind == TargetKind.Unsupported)
			{
				reason = EligibilityRejectReason.UnsupportedType;
				return false;
			}

			// Build pieces need WearNTear preview. Environment may not have it.
			if (!target.HasScopePreview && target.Kind != TargetKind.Environment)
			{
				reason = EligibilityRejectReason.NoScopePreview;
				return false;
			}

			return true;
		}

		private static bool PassesBuildPieceRemovability(TargetIdentity target, out EligibilityRejectReason reason)
		{
			reason = EligibilityRejectReason.None;
			if (!target.HasPiece)
			{
				reason = EligibilityRejectReason.NoPiece;
				return false;
			}

			if (!target.CanBeRemoved)
			{
				reason = EligibilityRejectReason.NotRemovable;
				return false;
			}

			return true;
		}

		public static TargetKind Classify(bool hasPiece, bool canBeRemoved, bool isEnvironment, bool hasScopePreview)
		{
			if (isEnvironment)
			{
				return TargetKind.Environment;
			}

			if (hasPiece)
			{
				return TargetKind.BuildPiece;
			}

			return TargetKind.Unsupported;
		}

		public static HashSet<string> ParsePrefabList(string csv)
		{
			var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrWhiteSpace(csv))
			{
				return set;
			}

			foreach (string part in csv.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string t = part.Trim();
				if (t.Length > 0)
				{
					set.Add(t);
				}
			}

			return set;
		}
	}
}
