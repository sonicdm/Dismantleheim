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
		SessionMismatch
	}

	public static class Eligibility
	{
		/// <summary>
		/// Runtime classification rules (no generated piece allowlist).
		/// ExtraAllow only overrides ExtraDeny — never bypasses type/removability/environment rules.
		/// </summary>
		public static bool IsEligible(
			TargetIdentity target,
			string prefabFilter,
			bool allowEnvironmentWithFilter,
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

			bool filterActive = !string.IsNullOrEmpty(prefabFilter);
			if (filterActive)
			{
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
					if (!target.HasPiece || !target.CanBeRemoved)
					{
						reason = target.HasPiece ? EligibilityRejectReason.NotRemovable : EligibilityRejectReason.NoPiece;
						return false;
					}

					return true;
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

		public static TargetKind Classify(bool hasPiece, bool canBeRemoved, bool isEnvironment)
		{
			if (isEnvironment)
			{
				return TargetKind.Environment;
			}

			if (hasPiece && canBeRemoved)
			{
				return TargetKind.BuildPiece;
			}

			if (hasPiece)
			{
				// Piece that cannot be removed — still typed, but not eligible.
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
