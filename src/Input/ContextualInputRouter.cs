using Dismantleheim.Core;
using Dismantleheim.Integration;
using Dismantleheim.Selection;
using UnityEngine;

namespace Dismantleheim.Input
{
	internal static class ContextualInputRouter
	{
		private static bool _mouse3WasDown;
		private static bool _shiftSampleLatch;
		private static bool _needsPhysicalMouse3Up;

		public static void ResetLatches()
		{
			_mouse3WasDown = false;
			_shiftSampleLatch = false;
			_needsPhysicalMouse3Up = true;
		}

		public static void Tick(DismantleSession session)
		{
			if (session == null || !DismantleheimPlugin.IsModEnabled())
			{
				return;
			}

			if (session.IgnorePieceChangeFrames > 0)
			{
				session.IgnorePieceChangeFrames--;
			}

			bool menuOrFocus = ConsoleIsOpen() || ChatIsOpen() || TextInputFocused()
			                   || InventoryGui.IsVisible() || MenuIsOpen() || !Application.isFocused;

			// Escape / focus cancel MUST run before hold completion.
			bool mouse3 = ZInput.GetMouseButton(2);
			bool mouse3Down = mouse3 && !_mouse3WasDown;
			bool mouse3Up = !mouse3 && _mouse3WasDown;
			_mouse3WasDown = mouse3;

			if (_needsPhysicalMouse3Up)
			{
				if (!mouse3)
				{
					_needsPhysicalMouse3Up = false;
					session.ConfirmHold.Rearm();
				}
			}

			if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB"))
			{
				if (session.IsActive)
				{
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
					else
					{
						session.Deactivate("escape");
					}

					return;
				}
			}

			if (menuOrFocus)
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
				if (!DismantleheimPlugin.PatchesReady)
				{
					return;
				}

				session.ToggleActivate("hotkey");
				ResetLatches();
				_needsPhysicalMouse3Up = true;
			}

			ToolIdentityTracker.Tick(session);

			if (!session.IsActive)
			{
				return;
			}

			Player player = Player.m_localPlayer;
			if ((Object)(object)player == (Object)null)
			{
				return;
			}

			bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

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
				if (session.ConfirmHold.IsHolding)
				{
					session.ConfirmHold.Cancel();
					session.StateMachine.TryTransition(DismantleTransition.CancelHold, session.Queue.Count, out _);
				}

				if (mouse3Up)
				{
					_shiftSampleLatch = false;
				}

				TryLeftClickSelect(session);
				return;
			}

			if (_shiftSampleLatch && mouse3Up)
			{
				_shiftSampleLatch = false;
				return;
			}

			// Freeze queue edits during hold: selection clicks ignored while HoldingConfirm.
			bool holding = session.StateMachine.State == DismantleState.HoldingConfirm
			               || session.ConfirmHold.IsHolding;

			if (!holding)
			{
				TryLeftClickSelect(session);
			}

			if (_needsPhysicalMouse3Up)
			{
				return;
			}

			if (mouse3Down && session.Queue.Count > 0 && !holding)
			{
				if (session.StateMachine.TryTransition(DismantleTransition.BeginHold, session.Queue.Count, out _))
				{
					session.BeginConfirmSnapshot();
					session.ConfirmHold.Begin(Time.unscaledTime);
				}
			}

			if (session.ConfirmHold.IsHolding)
			{
				if (!mouse3)
				{
					session.ConfirmHold.Cancel();
					session.StateMachine.TryTransition(DismantleTransition.CancelHold, session.Queue.Count, out _);
					session.ClearConfirmSnapshot();
					session.ConfirmHold.Rearm();
				}
				else
				{
					session.ConfirmHold.Update(Time.unscaledTime, DismantleheimPlugin.GetHoldSeconds());
					if (session.ConfirmHold.CompletedThisFrame)
					{
						session.CommitDryOrReal();
						_needsPhysicalMouse3Up = true;
					}
				}
			}
			else if (mouse3Up)
			{
				session.ConfirmHold.Rearm();
			}
		}

		private static void TryLeftClickSelect(DismantleSession session)
		{
			if (!ZInput.GetMouseButtonDown(0))
			{
				return;
			}

			TargetIdentity target = session.HoverTarget;
			if (target == null)
			{
				return;
			}

			if (!Eligibility.IsEligible(
				    target,
				    session.Sampler.ActiveFilter,
				    session.AllowEnvWithFilter,
				    session.ExtraDeny(),
				    session.ExtraAllow(),
				    out EligibilityRejectReason reason))
			{
				session.LastRejectReason = reason + " prefab=" + target.PrefabName + " kind=" + target.Kind;
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

		private static bool MenuIsOpen()
		{
			return Menu.IsVisible();
		}
	}
}
