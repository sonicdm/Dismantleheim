using System.Collections.Generic;
using Dismantleheim.Core;
using Dismantleheim.Selection;
using UnityEngine;

namespace Dismantleheim.UI
{
	internal static class HoverHighlighter
	{
		// Sticky selection tint (re-applied every frame; WearNTear.Highlight resets after 0.2s).
		private static readonly Color SelectedColor = new Color(1f, 0.35f, 0.15f, 1f);
		private static readonly Color HoverColor = new Color(0.35f, 0.85f, 1f, 1f);

		// Props/trees often have no WearNTear — MaterialMan tint sticks until ResetValue.
		private static readonly HashSet<int> _activeTintIds = new HashSet<int>();
		private static readonly HashSet<int> _frameTintIds = new HashSet<int>();
		private static readonly Dictionary<int, GameObject> _tintRoots = new Dictionary<int, GameObject>();

		public static void Tick(DismantleSession session)
		{
			if (session == null || !session.IsActive)
			{
				ClearAll();
				return;
			}

			_frameTintIds.Clear();

			foreach (TargetIdentity id in session.Queue.Entries)
			{
				Apply(id, SelectedColor);
			}

			TargetIdentity hover = session.HoverTarget;
			if (hover != null
			    && Eligibility.IsEligible(
				    hover,
				    session.ActiveMode,
				    session.Sampler.ActiveFilter,
				    session.AllowEnvWithFilter,
				    session.ExtraDeny(),
				    session.ExtraAllow(),
				    out _)
			    && !session.Queue.Contains(hover))
			{
				Apply(hover, HoverColor);
			}

			// Drop tint on anything no longer hovered/queued this frame.
			if (_activeTintIds.Count > 0)
			{
				List<int> stale = null;
				foreach (int instanceId in _activeTintIds)
				{
					if (!_frameTintIds.Contains(instanceId))
					{
						if (stale == null)
						{
							stale = new List<int>();
						}

						stale.Add(instanceId);
					}
				}

				if (stale != null)
				{
					foreach (int instanceId in stale)
					{
						ResetTracked(instanceId);
					}
				}
			}

			_activeTintIds.Clear();
			foreach (int instanceId in _frameTintIds)
			{
				_activeTintIds.Add(instanceId);
			}
		}

		public static void ClearAll()
		{
			if (_activeTintIds.Count == 0 && _tintRoots.Count == 0)
			{
				return;
			}

			List<int> ids = new List<int>(_activeTintIds);
			foreach (int instanceId in ids)
			{
				ResetTracked(instanceId);
			}

			_activeTintIds.Clear();
			_frameTintIds.Clear();
			_tintRoots.Clear();
		}

		private static void Apply(TargetIdentity id, Color color)
		{
			if (id == null)
			{
				return;
			}

			GameObject go = Removal.ExactObjectExecutor.ResolveInstance(id);
			if ((Object)(object)go == (Object)null)
			{
				return;
			}

			WearNTear wear = go.GetComponent<WearNTear>();
			if ((Object)(object)wear == (Object)null)
			{
				wear = go.GetComponentInChildren<WearNTear>();
			}

			if ((Object)(object)wear != (Object)null)
			{
				// Prefer vanilla Highlight path (MaterialMan + ResetHighlight) so visuals match hammer.
				// Re-call every frame so the 0.2s ResetHighlight never sticks off while queued.
				wear.Highlight();
				ApplyMaterialTint(wear.gameObject, color);
				try
				{
					wear.CancelInvoke("ResetHighlight");
					wear.Invoke("ResetHighlight", 0.35f);
				}
				catch
				{
					// Timed reset is best-effort; Tick tracking also clears.
				}

				return;
			}

			// Trees / rocks / Destructible props: no WearNTear — tint via MaterialMan directly.
			ApplyMaterialTint(go, color);
		}

		private static void ApplyMaterialTint(GameObject go, Color color)
		{
			if ((Object)(object)go == (Object)null)
			{
				return;
			}

			try
			{
				MaterialMan man = MaterialMan.instance;
				if ((Object)(object)man == (Object)null)
				{
					return;
				}

				man.SetValue(go, ShaderProps._EmissionColor, color * 0.55f, false);
				man.SetValue(go, ShaderProps._Color, color, false);

				int instanceId = go.GetInstanceID();
				_frameTintIds.Add(instanceId);
				_tintRoots[instanceId] = go;
			}
			catch
			{
				// MaterialMan/ShaderProps shape differs — skip tint rather than break selection.
			}
		}

		private static void ResetTracked(int instanceId)
		{
			GameObject go;
			if (!_tintRoots.TryGetValue(instanceId, out go))
			{
				_activeTintIds.Remove(instanceId);
				return;
			}

			_tintRoots.Remove(instanceId);
			_activeTintIds.Remove(instanceId);

			if ((Object)(object)go == (Object)null)
			{
				return;
			}

			try
			{
				MaterialMan man = MaterialMan.instance;
				if ((Object)(object)man == (Object)null)
				{
					return;
				}

				man.ResetValue(go, ShaderProps._Color);
				man.ResetValue(go, ShaderProps._EmissionColor);
			}
			catch
			{
				// Ignore cleanup failures on destroyed objects.
			}
		}
	}
}
