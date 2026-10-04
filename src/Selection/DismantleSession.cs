using System.Collections.Generic;
using Dismantleheim.Core;
using Dismantleheim.Removal;

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

		public bool IsActive => StateMachine.State != DismantleState.Inactive;

		public void Activate(string source)
		{
			if (StateMachine.State == DismantleState.Inactive)
			{
				StateMachine.TryTransition(DismantleTransition.Activate, Queue.Count, out _);
				DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim activate via " + source);
			}
		}

		public void Deactivate(string source)
		{
			ConfirmHold.Cancel();
			ConfirmHold.Rearm();
			if (DismantleheimPlugin.ClearQueueOnToolSwitch != null && DismantleheimPlugin.ClearQueueOnToolSwitch.Value)
			{
				Queue.Clear();
			}

			HoverTarget = null;
			StateMachine.TryTransition(DismantleTransition.Deactivate, Queue.Count, out _);
			DismantleheimPlugin.ModLogger?.LogInfo("Dismantleheim deactivate via " + source + " queue=" + Queue.Count);
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
			if (!StateMachine.TryTransition(DismantleTransition.HoldComplete, Queue.Count, out _))
			{
				return;
			}

			RemovalPlan plan = RemovalPlan.FromQueue(
				Queue.Snapshot(),
				id => id != null && !id.IsStale && !RemovalPlan.LooksLikeWildcard(id.PrefabName),
				out List<TargetIdentity> skipped);

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
				// Real removes already applied in executor; drop successfully removed from queue.
				foreach (RemovalResult r in results)
				{
					if (r.Removed && r.Target != null)
					{
						Queue.Toggle(r.Target); // remove if present
					}
				}
			}

			StateMachine.TryTransition(DismantleTransition.ExecutionDone, Queue.Count, out _);
			ConfirmHold.Cancel();
		}
	}
}
