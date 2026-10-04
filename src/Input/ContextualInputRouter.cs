using Dismantleheim.Core;
using Dismantleheim.Integration;
using Dismantleheim.Selection;
using Jotunn.Managers;
using UnityEngine;

namespace Dismantleheim.Input
{
	internal static class ContextualInputRouter
	{
		private static bool _mouse3WasDown;
		private static bool _shiftSampleLatch;

		public static void Tick(DismantleSession session)
		{
			if (session == null || !DismantleheimPlugin.IsModEnabled())
			{
				return;
			}

			if (ConsoleIsOpen() || ChatIsOpen() || TextInputFocused())
			{
				if (session.ConfirmHold.IsHolding)
				{
					session.ConfirmHold.Cancel();
					session.StateMachine.TryTransition(DismantleTransition.CancelHold, session.Queue.Count, out _);
				}

				return;
			}

			if (ZInput.GetButtonDown(DismantleheimPlugin.ActivateButtonName))
			{
				session.ToggleActivate("hotkey");
			}

			ToolIdentityTracker.Tick(session);

			if (!session.IsActive)
			{
				_mouse3WasDown = ZInput.GetMouseButton(2);
				return;
			}

			Player player = Player.m_localPlayer;
			if ((Object)(object)player == (Object)null)
			{
				return;
			}

			bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
			bool mouse3 = ZInput.GetMouseButton(2);
			bool mouse3Down = mouse3 && !_mouse3WasDown;
			bool mouse3Up = !mouse3 && _mouse3WasDown;
			_mouse3WasDown = mouse3;

			// Hover resolve every frame while active.
			if (TargetResolver.TryResolveHover(player, out TargetIdentity hover, out string reject))
			{
				session.HoverTarget = hover;
				session.LastRejectReason = null;
			}
			else
			{
				session.HoverTarget = null;
				session.LastRejectReason = reject;
			}

			// Shift + Mouse3: prefab sample (never confirmation).
			if (shift && mouse3Down)
			{
				_shiftSampleLatch = true;
				string prefab = session.HoverTarget != null ? session.HoverTarget.PrefabName : null;
				if (session.Sampler.TrySample(prefab, out string filter))
				{
					DismantleheimPlugin.DebugLog("Prefab filter → " + (filter ?? "(cleared)"));
				}

				return;
			}

			if (shift)
			{
				// Block confirmation while shift held (H13).
				if (session.ConfirmHold.IsHolding)
				{
					session.ConfirmHold.Cancel();
					session.StateMachine.TryTransition(DismantleTransition.CancelHold, session.Queue.Count, out _);
				}

				if (mouse3Up)
				{
					_shiftSampleLatch = false;
					session.ConfirmHold.Rearm();
				}

				TryLeftClickSelect(session, player);
				return;
			}

			if (_shiftSampleLatch && mouse3Up)
			{
				_shiftSampleLatch = false;
				session.ConfirmHold.Rearm();
				return;
			}

			// Hold Mouse3 confirm.
			if (mouse3Down && session.Queue.Count > 0)
			{
				if (session.StateMachine.TryTransition(DismantleTransition.BeginHold, session.Queue.Count, out _))
				{
					session.ConfirmHold.Begin(Time.unscaledTime);
				}
			}

			if (session.ConfirmHold.IsHolding)
			{
				if (!mouse3)
				{
					session.ConfirmHold.Cancel();
					session.StateMachine.TryTransition(DismantleTransition.CancelHold, session.Queue.Count, out _);
					session.ConfirmHold.Rearm();
				}
				else
				{
					session.ConfirmHold.Update(Time.unscaledTime, DismantleheimPlugin.GetHoldSeconds());
					if (session.ConfirmHold.CompletedThisFrame)
					{
						session.CommitDryOrReal();
					}
				}
			}
			else if (mouse3Up)
			{
				session.ConfirmHold.Rearm();
			}

			TryLeftClickSelect(session, player);

			if (ZInput.GetButtonDown("JoyButtonB") || UnityEngine.Input.GetKeyDown(KeyCode.Escape))
			{
				// Escape cancels hold or clears selection when active.
				if (session.ConfirmHold.IsHolding)
				{
					session.ConfirmHold.Cancel();
					session.StateMachine.TryTransition(DismantleTransition.CancelHold, session.Queue.Count, out _);
				}
				else if (session.Queue.Count > 0)
				{
					session.Queue.Clear();
					session.SyncStateAfterSelectionChange();
				}
			}
		}

		private static void TryLeftClickSelect(DismantleSession session, Player player)
		{
			if (!ZInput.GetMouseButtonDown(0))
			{
				return;
			}

			// Do not steal clicks while inventory/build list UI has focus.
			if (InventoryGui.IsVisible())
			{
				return;
			}

			TargetIdentity target = session.HoverTarget;
			if (target == null)
			{
				return;
			}

			bool allowEnv = session.AllowEnvWithFilter;
			if (!Eligibility.IsEligible(
				    target,
				    session.Sampler.ActiveFilter,
				    allowEnv,
				    session.ExtraDeny(),
				    session.ExtraAllow(),
				    out EligibilityRejectReason reason))
			{
				session.LastRejectReason = reason.ToString() + " prefab=" + target.PrefabName;
				DismantleheimPlugin.DebugLog("Reject select: " + session.LastRejectReason);
				return;
			}

			bool nowIn = session.Queue.Toggle(target);
			session.SyncStateAfterSelectionChange();
			DismantleheimPlugin.DebugLog((nowIn ? "Queued " : "Unqueued ") + target.PrefabName + "#" + target.NetworkId
			                            + " count=" + session.Queue.Count);
		}

		private static bool ConsoleIsOpen()
		{
			return Console.IsVisible();
		}

		private static bool ChatIsOpen()
		{
			return Chat.instance != null && Chat.instance.HasFocus();
		}

		private static bool TextInputFocused()
		{
			return GUIUtility.keyboardControl != 0;
		}
	}
}
