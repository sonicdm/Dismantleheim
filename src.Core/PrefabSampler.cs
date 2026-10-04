using System;

namespace Dismantleheim.Core
{
	/// <summary>
	/// Shift+Mouse3 style prefab filter: sample sets, same again clears, other switches.
	/// </summary>
	public sealed class PrefabSampler
	{
		public string ActiveFilter { get; private set; }

		public bool HasFilter => !string.IsNullOrEmpty(ActiveFilter);

		/// <summary>
		/// Apply sample. Returns the new filter (null/empty if cleared). Rejects null/empty hover.
		/// </summary>
		public bool TrySample(string prefabName, out string resultingFilter)
		{
			resultingFilter = ActiveFilter;
			if (string.IsNullOrWhiteSpace(prefabName))
			{
				return false;
			}

			string name = prefabName.Trim();
			if (string.Equals(ActiveFilter, name, StringComparison.OrdinalIgnoreCase))
			{
				ActiveFilter = null;
				resultingFilter = null;
				return true;
			}

			ActiveFilter = name;
			resultingFilter = ActiveFilter;
			return true;
		}

		public void Clear()
		{
			ActiveFilter = null;
		}
	}
}
