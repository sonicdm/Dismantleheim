using System;
using System.Collections.Generic;
using System.Linq;

namespace Dismantleheim.Core
{
	/// <summary>
	/// Toggle semantics: add if absent, remove if present (same network identity).
	/// Prefab filter changes do not drop already-queued entries.
	/// </summary>
	public sealed class SelectionQueue
	{
		private readonly List<TargetIdentity> _entries = new List<TargetIdentity>();

		public int Count => _entries.Count;

		public IReadOnlyList<TargetIdentity> Entries => _entries;

		public bool Contains(TargetIdentity identity)
		{
			if (identity == null)
			{
				return false;
			}

			for (int i = 0; i < _entries.Count; i++)
			{
				if (_entries[i].Equals(identity))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Toggle membership. Returns true if now present, false if removed/absent.
		/// </summary>
		public bool Toggle(TargetIdentity identity)
		{
			if (identity == null)
			{
				return false;
			}

			for (int i = 0; i < _entries.Count; i++)
			{
				if (_entries[i].Equals(identity))
				{
					_entries.RemoveAt(i);
					return false;
				}
			}

			_entries.Add(identity);
			return true;
		}

		public void Clear()
		{
			_entries.Clear();
		}

		public IReadOnlyList<TargetIdentity> Snapshot()
		{
			return _entries.ToList();
		}
	}
}
