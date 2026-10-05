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
		/// When adding, respects maxCount (0 = unlimited).
		/// </summary>
		public bool Toggle(TargetIdentity identity, int maxCount = 0)
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

			return TryAdd(identity, maxCount);
		}

		/// <summary>
		/// Add if absent. Returns true if newly added. Does not remove existing entries.
		/// </summary>
		public bool TryAdd(TargetIdentity identity, int maxCount = 0)
		{
			if (identity == null || Contains(identity))
			{
				return false;
			}

			if (maxCount > 0 && _entries.Count >= maxCount)
			{
				return false;
			}

			_entries.Add(identity);
			return true;
		}

		/// <summary>
		/// Remove if present. Returns true if an entry was removed.
		/// </summary>
		public bool TryRemove(TargetIdentity identity)
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
					return true;
				}
			}

			return false;
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
