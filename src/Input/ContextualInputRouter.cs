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

			// Ownership / world epoch must run even while menus steal selection input.
			ToolIdentityTracker.Tick(session);

			// NOTE: Do NOT treat Hud.IsPieceSelectionVisible() as a hard input block while active.
			// Holding Ctrl (Satisfactory paint) / picking an IH instant tool often keeps the piece
			// HUD flagged visible; blocking Cancel()'d paint and hid cursor HUD under that panel.
			// Also skip GUIUtility.keyboardControl — our own OnGUI labels false-trigger it.
			// Piece changes still deactivate via ToolIdentityTracker when the table is used.
			bool menuOrFocus = ConsoleIsOpen() || ChatIsOpen()
			                   || InventoryGui.IsVisible() || MenuIsOpen()
			                   || !Application.isFocused;

			bool mouse3 = MouseButtons.Get(2);
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
					session.ClearConfirmSnapshot();
					session.StateMachine.TryTransition(DismantleTransition.CancelHold, session.Queue.Count, out _);
				}

				DragBoxSelector.Cancel();

				if (session.IsActive && MouseButtons.GetDown(0))
				{
					string why = ConsoleIsOpen() ? "console"
						: ChatIsOpen() ? "chat"
						: InventoryGui.IsVisible() ? "inventory"
						: MenuIsOpen() ? "menu"
						: !Application.isFocused ? "unfocused"
						: "ui";
					DismantleheimPlugin.ModLogger?.LogInfo(
						"Dismantleheim click blocked by UI focus (" + why + ")");
				}

				return;
			}

			if (DismantleheimPlugin.WasActivatePressed())
			{
				if (!DismantleheimPlugin.PatchesReady)
				{
					return;
				}

				session.ToggleActivate("hotkey");
				ResetLatches();
				_needsPhysicalMouse3Up = true;
			}

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

			// Shift + Mouse3: sample prefab filter (never confirmation). Vanilla CopyPiece is suppressed.
			if (shift && mouse3Down)
			{
				if (session.ConfirmHold.IsHolding)
				{
					session.ConfirmHold.Cancel();
					session.ClearConfirmSnapshot();
					session.StateMachine.TryTransition(DismantleTransition.CancelHold, session.Queue.Count, out _);
				}

				_shiftSampleLatch = true;
				ApplyPrefabSample(session);
				return;
			}

			if (shift)
			{
				if (session.ConfirmHold.IsHolding)
				{
					session.ConfirmHold.Cancel();
					session.ClearConfirmSnapshot();
					session.StateMachine.TryTransition(DismantleTransition.CancelHold, session.Queue.Count, out _);
				}

				if (mouse3Up)
				{
					_shiftSampleLatch = false;
				}

				TryLeftClickSelect(session, ctrl: false);
				return;
			}

			if (_shiftSampleLatch && mouse3Up)
			{
				_shiftSampleLatch = false;
				return;
			}

			bool holding = session.StateMachine.State == DismantleState.HoldingConfirm
			               || session.ConfirmHold.IsHolding;

			bool ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl)
			            || UnityEngine.Input.GetKey(KeyCode.RightControl)
			            || UnityEngine.Input.GetKey(KeyCode.LeftCommand)
			            || UnityEngine.Input.GetKey(KeyCode.RightCommand);

			// Plain LMB = click-add only. Ctrl+wave / Ctrl+LMB drag = paint (DragBoxSelector).
			// Ctrl+click unselect is owned by DragBoxSelector on mouse-up if the press never painted.
			if (!holding && !ctrl)
			{
				TryLeftClickSelect(session, ctrl: false);
			}

			DragBoxSelector.Tick(session);
			if (DragBoxSelector.BlocksConfirm)
			{
				return;
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

		private static void ApplyPrefabSample(DismantleSession session)
		{
			// Prefer fresh aim ray so trees/rocks (no Piece hover) can be sampled for Mass Delete.
			TargetIdentity sampleTarget = null;
			if (TargetResolver.TryResolveAim(Player.m_localPlayer, out TargetIdentity aim, out string aimReject))
			{
				sampleTarget = aim;
				session.HoverTarget = aim;
				session.LastRejectReason = null;
			}
			else if (session.HoverTarget != null)
			{
				sampleTarget = session.HoverTarget;
			}
			else
			{
				session.LastRejectReason = "sample-no-aim " + (aimReject ?? "");
			}

			string prefab = sampleTarget != null ? sampleTarget.PrefabName : null;
			if (string.IsNullOrWhiteSpace(prefab))
			{
				if (session.Sampler.HasFilter)
				{
					session.Sampler.Clear();
					Announce("Prefab filter cleared (no hover)");
				}
				else
				{
					Announce("Shift+Mouse3: aim at a piece/tree/rock to sample (" + (aimReject ?? "no-hover") + ")");
				}

				return;
			}

			if (!session.Sampler.TrySample(prefab, out string filter))
			{
				Announce("Prefab sample failed");
				return;
			}

			// Non-build samples are Mass Delete only — switch so the filter is immediately usable.
			if (filter != null
			    && sampleTarget.Kind != TargetKind.BuildPiece
			    && session.ActiveMode == OperationMode.MassDismantle)
			{
				session.SetMode(OperationMode.MassDelete, "sample-world-object");
			}

			string kind = sampleTarget.Kind.ToString();
			if (filter == null)
			{
				Announce("Prefab filter cleared");
			}
			else if (sampleTarget.Kind != TargetKind.BuildPiece)
			{
				Announce(
					"Prefab filter → " + filter + " [" + kind + "] — Mass Delete (exact match selectable)");
			}
			else
			{
				Announce("Prefab filter → " + filter + " [" + kind + "]");
			}
		}

		private static void TryLeftClickSelect(DismantleSession session, bool ctrl)
		{
			if (!MouseButtons.GetDown(0))
			{
				return;
			}

			// Ctrl+click is handled in DragBoxSelector (paint vs discrete unselect).
			if (ctrl)
			{
				return;
			}

			TargetIdentity target = session.HoverTarget;
			if (target == null
			    && TargetResolver.TryResolveAim(Player.m_localPlayer, out TargetIdentity aim, out _))
			{
				target = aim;
				session.HoverTarget = aim;
			}

			if (target == null)
			{
				session.LastRejectReason = "click-no-hover " + (session.LastRejectReason ?? "");
				DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim click ignored: no hover target");
				return;
			}

			if (!Eligibility.IsEligible(
				    target,
				    session.ActiveMode,
				    session.Sampler.ActiveFilter,
				    session.AllowEnvWithFilter,
				    session.ExtraDeny(),
				    session.ExtraAllow(),
				    out EligibilityRejectReason reason))
			{
				session.LastRejectReason = reason + " mode=" + session.ActiveMode
				                           + " prefab=" + target.PrefabName + " kind=" + target.Kind;
				DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim Reject select: " + session.LastRejectReason);
				return;
			}

			int before = session.Queue.Count;
			bool added = session.Queue.TryAdd(target, session.MaximumTargets);
			if (!added)
			{
				if (session.Queue.Contains(target))
				{
					// Already selected — plain click does not unselect.
					return;
				}

				if (before >= session.MaximumTargets)
				{
					session.LastRejectReason = "max-targets=" + session.MaximumTargets;
					DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim Reject select: " + session.LastRejectReason);
				}

				return;
			}

			session.SyncStateAfterSelectionChange();
			Announce("Queued " + target.PrefabName + "  (" + session.Queue.Count + " selected)");
		}

		private static void Announce(string msg)
		{
			DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim " + msg);
			if (Console.instance != null)
			{
				Console.instance.Print("Dismantleheim " + msg);
			}

			Player player = Player.m_localPlayer;
			if ((Object)(object)player != (Object)null)
			{
				player.Message(MessageHud.MessageType.TopLeft, msg);
			}
		}

		private static bool ConsoleIsOpen()
		{
			return Console.IsVisible();
		}

		private static bool ChatIsOpen()
		{
			return Chat.instance != null && Chat.instance.HasFocus();
		}

		private static bool MenuIsOpen()
		{
			return Menu.IsVisible();
		}
	}
}
