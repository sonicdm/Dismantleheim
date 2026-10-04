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
		/// ExtraAllow only overrides ExtraDeny — never bypasses type/removability/environment/preview rules.
		/// </summary>
		public static bool IsEligible(
			TargetIdentity target,
			string prefabFilter,
			bool allowEnvironmentWithFilter,
			ISet<string> extraDeny,
			ISet<string> extraAllow,
			out EligibilityRejectReason reason)
		{
			if (!PassesHardSafety(target, extraDeny, extraAllow, out reason))
			{
				return false;
			}

			bool filterActive = !string.IsNullOrEmpty(prefabFilter);
			if (filterActive)
			{
				string prefab = target.PrefabName ?? string.Empty;
				if (!string.Equals(prefab, prefabFilter, StringComparison.OrdinalIgnoreCase))
				{
					reason = EligibilityRejectReason.PrefabFilterMismatch;
					return false;
				}

				if (target.Kind == TargetKind.Environment)
				{
					if (!allowEnvironmentWithFilter)
					{
						reason = EligibilityRejectReason.EnvironmentWithoutFilter;
						return false;
					}

					return true;
				}

				if (target.Kind == TargetKind.BuildPiece)
				{
					return PassesBuildPieceRemovability(target, out reason);
				}

				reason = EligibilityRejectReason.UnsupportedType;
				return false;
			}

			// Unfiltered: build pieces only.
			if (target.Kind != TargetKind.BuildPiece)
			{
				reason = target.Kind == TargetKind.Environment
					? EligibilityRejectReason.EnvironmentWithoutFilter
					: EligibilityRejectReason.UnsupportedType;
				return false;
			}

			return PassesBuildPieceRemovability(target, out reason);
		}

		/// <summary>
		/// Commit-time checks for already-queued identities.
		/// Does not re-apply the current candidate prefab filter (mixed queues stay valid).
		/// Environment entries keep provenance via their own prefab name.
		/// </summary>
		public static bool IsEligibleForCommit(
			TargetIdentity target,
			bool allowEnvironmentWithFilter,
			ISet<string> extraDeny,
			ISet<string> extraAllow,
			out EligibilityRejectReason reason)
		{
			if (!PassesHardSafety(target, extraDeny, extraAllow, out reason))
			{
				return false;
			}

			if (target.Kind == TargetKind.Environment)
			{
				if (!allowEnvironmentWithFilter)
				{
					reason = EligibilityRejectReason.EnvironmentWithoutFilter;
					return false;
				}

				return true;
			}

			if (target.Kind == TargetKind.BuildPiece)
			{
				return PassesBuildPieceRemovability(target, out reason);
			}

			reason = EligibilityRejectReason.UnsupportedType;
			return false;
		}

		private static bool PassesHardSafety(
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

			if (target.Kind == TargetKind.Unsupported)
			{
				reason = EligibilityRejectReason.UnsupportedType;
				return false;
			}

			if (!target.HasScopePreview)
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
				// Environment without a whole-object preview path is unsupported (H08/H23).
				return hasScopePreview ? TargetKind.Environment : TargetKind.Unsupported;
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

			foreach (string part in csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string trimmed = part.Trim();
				if (trimmed.Length > 0)
				{
					set.Add(trimmed);
				}
			}

			return set;
		}
	}
}
