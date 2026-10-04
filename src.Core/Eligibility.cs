using System;
using System.Collections.Generic;

namespace Dismantleheim.Core
{
	public enum EligibilityRejectReason
	{
		None,
		NullTarget,
		Stale,
		NoPiece,
		NotRemovable,
		EnvironmentWithoutFilter,
		PrefabFilterMismatch,
		ExtraDeny
	}

	public static class Eligibility
	{
		/// <summary>
		/// Runtime classification rules (no generated piece allowlist).
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
			if (extraDeny != null && extraDeny.Contains(prefab))
			{
				reason = EligibilityRejectReason.ExtraDeny;
				return false;
			}

			if (extraAllow != null && extraAllow.Contains(prefab))
			{
				return true;
			}

			bool filterActive = !string.IsNullOrEmpty(prefabFilter);
			if (filterActive)
			{
				if (!string.Equals(prefab, prefabFilter, StringComparison.OrdinalIgnoreCase))
				{
					reason = EligibilityRejectReason.PrefabFilterMismatch;
					return false;
				}

				if (target.IsEnvironment && !allowEnvironmentWithFilter)
				{
					reason = EligibilityRejectReason.EnvironmentWithoutFilter;
					return false;
				}

				return true;
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

			if (target.IsEnvironment)
			{
				reason = EligibilityRejectReason.EnvironmentWithoutFilter;
				return false;
			}

			return true;
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
