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

		private List<TargetIdentity> _confirmSnapshot;

		public bool IsActive => StateMachine.State != DismantleState.Inactive;

		public bool TryActivate(string source, out string reject)
		{
			reject = null;
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

			// Ownership requires hammer place-mode so attack suppression cannot stick on a non-tool.
			if (!player.InPlaceMode())
			{
				reject = "not-in-place-mode";
				DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim activate rejected: " + reject);
				return false;
			}

			StateMachine.TryTransition(DismantleTransition.Activate, Queue.Count, out _);
			IgnorePieceChangeFrames = 3;
			ToolIdentityTracker.OnActivated();
			ContextualInputRouter.ResetLatches();
			DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim activate via " + source);
			return true;
		}

		public void Activate(string source)
		{
			TryActivate(source, out _);
		}

		public void Deactivate(string source)
		{
			ConfirmHold.Cancel();
			ConfirmHold.Rearm();
			ClearConfirmSnapshot();
			if (DismantleheimPlugin.ClearQueueOnToolSwitch == null || DismantleheimPlugin.ClearQueueOnToolSwitch.Value)
			{
				Queue.Clear();
			}

			HoverTarget = null;
			StateMachine.ForceInactive();
			ToolIdentityTracker.OnDeactivated();
			ContextualInputRouter.ResetLatches();
			HoverHighlighter.ClearAll();
			DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim deactivate via " + source + " queue=" + Queue.Count);
		}

		/// <summary>Always clears queue/filter — used for disconnect/world epoch, not ordinary tool switch.</summary>
		public void ResetHard(string source)
		{
			Queue.Clear();
			Sampler.Clear();
			ConfirmHold.Cancel();
			ConfirmHold.Rearm();
			ClearConfirmSnapshot();
			HoverTarget = null;
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
				Activate(source);
			}
		}

		public void SyncStateAfterSelectionChange()
		{
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

				var revalidated = new List<TargetIdentity>();
				var skipped = new List<TargetIdentity>();
				foreach (TargetIdentity queued in source)
				{
					if (!TargetResolver.TryRevalidate(queued, out TargetIdentity live, out string detail))
					{
						skipped.Add(queued);
						DismantleheimPlugin.DebugLog("Revalidate skip " + (queued != null ? queued.PrefabName : "?") + " " + detail);
						continue;
					}

					// Commit must not re-apply the current candidate filter to a mixed queue.
					if (!Eligibility.IsEligibleForCommit(
						    live,
						    AllowEnvWithFilter,
						    ExtraDeny(),
						    ExtraAllow(),
						    out EligibilityRejectReason reason))
					{
						skipped.Add(live);
						DismantleheimPlugin.DebugLog("Commit safety skip " + live.PrefabName + " " + reason);
						continue;
					}

					revalidated.Add(live);
				}

				RemovalPlan plan = RemovalPlan.FromQueue(revalidated, id => id != null && !id.IsStale, out List<TargetIdentity> planSkipped);
				skipped.AddRange(planSkipped);

				StateMachine.TryTransition(DismantleTransition.ValidationDone, plan.Count, out _);

				List<RemovalResult> results = ExactObjectExecutor.Execute(plan, skipped);
				foreach (RemovalResult r in results)
				{
					string line = (r.Removed ? "REMOVED " : "SKIP ")
					              + (r.Target != null ? r.Target.PrefabName + "#" + r.Target.NetworkId : "?")
					              + " " + r.SkipReason + " " + r.Message;
					DismantleheimPlugin.ModLogger?.LogInfo(line);
				}

				bool dry = DismantleheimPlugin.DryRunOnly == null || DismantleheimPlugin.DryRunOnly.Value;
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
					TryActivate("post-commit", out _);
					SyncStateAfterSelectionChange();
				}
			}
		}
	}
}
