using System;
using System.Collections.Generic;
using Dismantleheim.Core;
using Dismantleheim.Input;
using Dismantleheim.Integration;
using Dismantleheim.Removal;
using Dismantleheim.UI;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Dismantleheim.Selection
{
	internal sealed class DismantleSession
	{
		public SelectionQueue Queue { get; } = new SelectionQueue();

		public PrefabSampler Sampler { get; } = new PrefabSampler();

		public DismantleStateMachine StateMachine { get; } = new DismantleStateMachine();

		public ConfirmationHold ConfirmHold { get; } = new ConfirmationHold();

		public TargetIdentity HoverTarget { get; set; }

		public string LastRejectReason { get; set; }

		public int IgnorePieceChangeFrames { get; set; }

		public OperationMode ActiveMode { get; private set; } = OperationMode.MassDismantle;

		public bool HasContentsLossWarning { get; private set; }

		private List<TargetIdentity> _confirmSnapshot;

		public bool IsActive => StateMachine.State != DismantleState.Inactive;

		public bool TryActivate(string source, out string reject)
		{
			return TryActivate(ActiveMode, source, out reject);
		}

		public bool TryActivate(OperationMode mode, string source, out string reject)
		{
			reject = null;
			ActiveMode = mode;

			if (StateMachine.State != DismantleState.Inactive)
			{
				return true;
			}

			Player player = Player.m_localPlayer;
			if ((UObject)(object)player == (UObject)null)
			{
				reject = "no-player";
				return false;
			}

			if (!player.InPlaceMode())
			{
				reject = "not-in-place-mode";
				DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim activate rejected: " + reject);
				return false;
			}

			StateMachine.TryTransition(DismantleTransition.Activate, Queue.Count, out _);
			// IH instant Tools storm SetSelectedPiece after the command; keep grace long enough
			// that selecting the tool from the build menu cannot immediately deactivate us.
			IgnorePieceChangeFrames = 180;
			ToolIdentityTracker.OnActivated();
			ContextualInputRouter.ResetLatches();
			DragBoxSelector.Cancel();
			RefreshContentsWarning();

			Player local = Player.m_localPlayer;
			bool place = (UObject)(object)local != (UObject)null && local.InPlaceMode();
			string right = (UObject)(object)local != (UObject)null && local.RightItem != null
				? ((local.RightItem.m_shared != null ? local.RightItem.m_shared.m_name : "?")
				   + "|" + (local.RightItem.m_dropPrefab != null ? local.RightItem.m_dropPrefab.name : "?"))
				: "empty";
			DismantleheimPlugin.ModLogger?.LogInfo(
				"Dismantleheim activate via " + source
				+ " mode=" + OperationModeUtil.ShortName(ActiveMode)
				+ " state=" + StateMachine.State
				+ " place=" + place
				+ " item=" + right
				+ " grace=" + IgnorePieceChangeFrames);

			if ((UObject)(object)local != (UObject)null)
			{
				local.Message(MessageHud.MessageType.TopLeft, OperationModeUtil.Banner(ActiveMode));
			}

			return true;
		}

		public void Activate(string source)
		{
			TryActivate(source, out _);
		}

		/// <summary>Switch mode without executing. Preserves queue when configured; never widens set.</summary>
		public void SetMode(OperationMode mode, string source)
		{
			if (ActiveMode == mode && IsActive)
			{
				return;
			}

			ConfirmHold.Cancel();
			ClearConfirmSnapshot();

			bool preserve = DismantleheimPlugin.PreserveSelectionOnModeSwitch == null
			                || DismantleheimPlugin.PreserveSelectionOnModeSwitch.Value;
			OperationMode previous = ActiveMode;
			ActiveMode = mode;

			if (!IsActive)
			{
				TryActivate(mode, source, out _);
				return;
			}

			if (!preserve)
			{
				Queue.Clear();
			}

			RefreshContentsWarning();
			SyncStateAfterSelectionChange();
			DismantleheimPlugin.ModLogger?.LogInfo(
				"Dismantleheim mode " + OperationModeUtil.ShortName(previous)
				+ " → " + OperationModeUtil.ShortName(mode) + " via " + source
				+ " queue=" + Queue.Count);
		}

		public void Deactivate(string source)
		{
			int q = Queue.Count;
			DismantleState prev = StateMachine.State;
			ConfirmHold.Cancel();
			ConfirmHold.Rearm();
			ClearConfirmSnapshot();
			if (DismantleheimPlugin.ClearQueueOnToolSwitch == null || DismantleheimPlugin.ClearQueueOnToolSwitch.Value)
			{
				Queue.Clear();
			}

			HoverTarget = null;
			HasContentsLossWarning = false;
			StateMachine.ForceInactive();
			ToolIdentityTracker.OnDeactivated();
			ContextualInputRouter.ResetLatches();
			HoverHighlighter.ClearAll();
			DismantleheimPlugin.ModLogger?.LogInfo(
				"Dismantleheim deactivate via " + source
				+ " queue=" + q
				+ " wasState=" + prev
				+ " mode=" + OperationModeUtil.ShortName(ActiveMode));
		}

		public void ResetHard(string source)
		{
			Queue.Clear();
			Sampler.Clear();
			ConfirmHold.Cancel();
			ConfirmHold.Rearm();
			ClearConfirmSnapshot();
			HoverTarget = null;
			HasContentsLossWarning = false;
			StateMachine.ForceInactive();
			ToolIdentityTracker.OnDeactivated();
			ContextualInputRouter.ResetLatches();
			HoverHighlighter.ClearAll();
			DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim hard-reset via " + source);
		}

		public void ToggleActivate(string source)
		{
			if (IsActive)
			{
				Deactivate(source);
			}
			else
			{
				OperationMode def = DismantleheimPlugin.GetDefaultMode();
				TryActivate(def, source, out _);
			}
		}

		public void SyncStateAfterSelectionChange()
		{
			RefreshContentsWarning();
			if (!IsActive)
			{
				return;
			}

			StateMachine.TryTransition(DismantleTransition.SelectionChanged, Queue.Count, out _);
		}

		public void BeginConfirmSnapshot()
		{
			_confirmSnapshot = new List<TargetIdentity>(Queue.Snapshot());
		}

		public void ClearConfirmSnapshot()
		{
			_confirmSnapshot = null;
		}

		public ISet<string> ExtraDeny()
		{
			return Eligibility.ParsePrefabList(
				DismantleheimPlugin.ExtraDenyPrefabs != null ? DismantleheimPlugin.ExtraDenyPrefabs.Value : string.Empty);
		}

		public ISet<string> ExtraAllow()
		{
			return Eligibility.ParsePrefabList(
				DismantleheimPlugin.ExtraAllowPrefabs != null ? DismantleheimPlugin.ExtraAllowPrefabs.Value : string.Empty);
		}

		public bool AllowEnvWithFilter =>
			DismantleheimPlugin.AllowEnvironmentWithFilter == null || DismantleheimPlugin.AllowEnvironmentWithFilter.Value;

		public int MaximumTargets =>
			DismantleheimPlugin.MaximumTargets != null ? DismantleheimPlugin.MaximumTargets.Value : 50;

		public void RefreshContentsWarning()
		{
			HasContentsLossWarning = false;
			if (ActiveMode != OperationMode.MassDelete)
			{
				return;
			}

			if (DismantleheimPlugin.WarnWhenDeletingOccupiedContainers != null
			    && !DismantleheimPlugin.WarnWhenDeletingOccupiedContainers.Value)
			{
				return;
			}

			foreach (TargetIdentity id in Queue.Entries)
			{
				GameObject go = ExactObjectExecutor.ResolveInstance(id);
				if ((UObject)(object)go != (UObject)null
				    && ContainerContentsInspector.HasStoredItems(go, out _))
				{
					HasContentsLossWarning = true;
					return;
				}
			}
		}

		public void CommitDryOrReal()
		{
			try
			{
				if (!StateMachine.TryTransition(DismantleTransition.HoldComplete, Queue.Count, out _))
				{
					return;
				}

				IReadOnlyList<TargetIdentity> source = _confirmSnapshot != null
					? (IReadOnlyList<TargetIdentity>)_confirmSnapshot
					: Queue.Snapshot();

				List<RemovalPlanEntry> plan = PreflightBatchValidator.BuildPlan(
					ActiveMode,
					source,
					AllowEnvWithFilter,
					ExtraDeny(),
					ExtraAllow(),
					out _);

				StateMachine.TryTransition(DismantleTransition.ValidationDone, plan.Count, out _);

				List<RemovalResult> results = ExactObjectExecutor.Execute(ActiveMode, plan);
				ExecutionResultReporter.LogAll(results);
				string summary = ExecutionResultReporter.Summarize(results, ActiveMode);
				DismantleheimPlugin.ModLogger?.LogInfo(summary);
				if (Console.instance != null)
				{
					Console.instance.Print("Dismantleheim " + summary);
				}

				bool dry = DismantleheimPlugin.DryRunOnly != null && DismantleheimPlugin.DryRunOnly.Value;
				if (!dry)
				{
					foreach (RemovalResult r in results)
					{
						if (r.Removed && r.Target != null && Queue.Contains(r.Target))
						{
							Queue.Toggle(r.Target);
						}
					}
				}

				RefreshContentsWarning();
			}
			catch (Exception ex)
			{
				DismantleheimPlugin.ModLogger?.LogError("Commit failed: " + ex);
			}
			finally
			{
				ClearConfirmSnapshot();
				ConfirmHold.Cancel();
				StateMachine.TryTransition(DismantleTransition.ExecutionDone, Queue.Count, out _);
				if (StateMachine.State == DismantleState.Executing || StateMachine.State == DismantleState.Validating)
				{
					StateMachine.ForceInactive();
					TryActivate(ActiveMode, "post-commit", out _);
					SyncStateAfterSelectionChange();
				}
				else if (IsActive)
				{
					// Commit often flickers held-item / place-mode; rebind instead of tearing down.
					IgnorePieceChangeFrames = System.Math.Max(IgnorePieceChangeFrames, 45);
					ToolIdentityTracker.RefreshBaseline("post-commit");
					SyncStateAfterSelectionChange();
				}

				DismantleheimPlugin.ModLogger?.LogInfo(
					"Dismantleheim post-commit state=" + StateMachine.State
					+ " active=" + IsActive
					+ " queue=" + Queue.Count
					+ " grace=" + IgnorePieceChangeFrames);
			}
		}
	}
}
