using Dismantleheim.Core;
using UnityEngine;

namespace Dismantleheim.Selection
{
	/// <summary>
	/// Satisfactory-style mass select: hold Ctrl and wave aim (or Ctrl+LMB drag) to paint-add.
	/// Plain LMB is click-add only (ContextualInputRouter). Unselect is Ctrl+click only.
	/// </summary>
	internal static class DragBoxSelector
	{
		private const float CtrlReleaseGraceSeconds = 0.18f;

		private static bool _ctrlActive;
		private static bool _paintedAny;
		private static TargetIdentity _lastPainted;
		private static TargetIdentity _ctrlClickCandidate;
		private static float _ctrlReleasedAt = -999f;

		/// <summary>True while Ctrl mass-select is consuming aim input.</summary>
		public static bool IsDragging => _ctrlActive;

		/// <summary>Ctrl paint blocks Mouse3 confirm.</summary>
		public static bool BlocksConfirm => _ctrlActive;

		public static bool ConsumeClickSuppression()
		{
			// Kept for router call sites; plain LMB no longer suppresses clicks via paint.
			return false;
		}

		public static void Cancel()
		{
			_ctrlActive = false;
			_paintedAny = false;
			_lastPainted = null;
			_ctrlClickCandidate = null;
			_ctrlReleasedAt = -999f;
		}

		public static void Tick(DismantleSession session)
		{
			if (session == null || !session.IsActive)
			{
				Cancel();
				return;
			}

			bool ctrlRaw = IsCtrlHeld();

			// Debounce Ctrl release so brief flicker does not end the paint session.
			if (ctrlRaw)
			{
				_ctrlReleasedAt = -999f;
				_ctrlActive = true;
			}
			else if (_ctrlActive)
			{
				if (_ctrlReleasedAt < 0f)
				{
					_ctrlReleasedAt = Time.unscaledTime;
				}

				if (Time.unscaledTime - _ctrlReleasedAt < CtrlReleaseGraceSeconds)
				{
					ctrlRaw = true; // keep session alive through brief release
				}
				else
				{
					_ctrlActive = false;
					_lastPainted = null;
					_ctrlClickCandidate = null;
					_ctrlReleasedAt = -999f;
				}
			}

			if (_ctrlActive && ctrlRaw)
			{
				TickCtrlPaint(session);
			}
		}

		public static void DrawGui()
		{
			if (!_ctrlActive)
			{
				return;
			}

			GUI.Label(new Rect(16f, 16f, 420f, 22f), "Ctrl: mass-select (wave / drag aim)");
		}

		private static void TickCtrlPaint(DismantleSession session)
		{
			bool mouse0Down = MouseButtons.GetDown(0);
			bool mouse0Up = MouseButtons.GetUp(0);

			if (mouse0Down)
			{
				// Fresh stroke: Ctrl+click unselect only if this press never paint-adds.
				_paintedAny = false;
				_ctrlClickCandidate = ResolveAimTarget(session);
				// Prevent same-frame paint from immediately re-adding after an unselect path;
				// still allow paint-add when the candidate was not queued (see below).
				if (_ctrlClickCandidate != null && session.Queue.Contains(_ctrlClickCandidate))
				{
					_lastPainted = _ctrlClickCandidate;
				}
				else
				{
					_lastPainted = null;
				}
			}

			// Ctrl + wave OR Ctrl + LMB drag both paint-add (Satisfactory-style).
			PaintCurrentAim(session, remove: false);

			if (mouse0Up)
			{
				// Discrete Ctrl+click: no new paint-adds during the press → unselect.
				if (!_paintedAny && _ctrlClickCandidate != null)
				{
					if (session.Queue.TryRemove(_ctrlClickCandidate))
					{
						session.SyncStateAfterSelectionChange();
						Log("Unqueued " + _ctrlClickCandidate.PrefabName + "  (" + session.Queue.Count + " selected)");
					}

					_lastPainted = _ctrlClickCandidate;
				}

				_ctrlClickCandidate = null;
			}
		}

		private static TargetIdentity ResolveAimTarget(DismantleSession session)
		{
			Player player = Player.m_localPlayer;
			if ((Object)(object)player != (Object)null
			    && TargetResolver.TryResolveAim(player, out TargetIdentity aim, out _))
			{
				session.HoverTarget = aim;
				return aim;
			}

			return session.HoverTarget;
		}

		private static void PaintCurrentAim(DismantleSession session, bool remove)
		{
			TargetIdentity target = ResolveAimTarget(session);

			if (target == null)
			{
				// Gap between pieces: allow the next hover to paint even if it's the same prefab instance.
				_lastPainted = null;
				return;
			}

			if (_lastPainted != null && _lastPainted.Equals(target))
			{
				return;
			}

			if (ApplyPaint(session, target, remove))
			{
				_paintedAny = true;
				_lastPainted = target;
			}
			else if (!remove && session.Queue.Contains(target))
			{
				// Already queued — advance so we don't stall the brush on this piece.
				_lastPainted = target;
			}
			else if (remove && !session.Queue.Contains(target))
			{
				_lastPainted = target;
			}
			// Ineligible / failed add: do not sticky-lock last, so a later eligible frame can retry.
		}

		private static bool ApplyPaint(DismantleSession session, TargetIdentity target, bool remove)
		{
			if (target == null)
			{
				return false;
			}

			if (remove)
			{
				if (!session.Queue.TryRemove(target))
				{
					return false;
				}

				session.SyncStateAfterSelectionChange();
				Log("Unqueued " + target.PrefabName + "  (" + session.Queue.Count + " selected)");
				return true;
			}

			if (!Eligibility.IsEligible(
				    target,
				    session.ActiveMode,
				    session.Sampler.ActiveFilter,
				    session.AllowEnvWithFilter,
				    session.ExtraDeny(),
				    session.ExtraAllow(),
				    out _))
			{
				return false;
			}

			if (!session.Queue.TryAdd(target, session.MaximumTargets))
			{
				return false;
			}

			session.SyncStateAfterSelectionChange();
			Log("Queued " + target.PrefabName + "  (" + session.Queue.Count + " selected)");
			return true;
		}

		private static void Log(string msg)
		{
			DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim " + msg);
		}

		private static bool IsCtrlHeld()
		{
			return UnityEngine.Input.GetKey(KeyCode.LeftControl)
			       || UnityEngine.Input.GetKey(KeyCode.RightControl)
			       || UnityEngine.Input.GetKey(KeyCode.LeftCommand)
			       || UnityEngine.Input.GetKey(KeyCode.RightCommand);
		}
	}

	/// <summary>ZInput mouse buttons with Unity Input fallback (shift/modifier combos).</summary>
	internal static class MouseButtons
	{
		public static bool Get(int button)
		{
			try
			{
				if (ZInput.GetMouseButton(button))
				{
					return true;
				}
			}
			catch
			{
				// ignored
			}

			return UnityEngine.Input.GetMouseButton(button);
		}

		public static bool GetDown(int button)
		{
			try
			{
				if (ZInput.GetMouseButtonDown(button))
				{
					return true;
				}
			}
			catch
			{
				// ignored
			}

			return UnityEngine.Input.GetMouseButtonDown(button);
		}

		public static bool GetUp(int button)
		{
			try
			{
				if (ZInput.GetMouseButtonUp(button))
				{
					return true;
				}
			}
			catch
			{
				// ignored
			}

			return UnityEngine.Input.GetMouseButtonUp(button);
		}
	}
}
