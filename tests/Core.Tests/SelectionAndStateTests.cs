using System.Collections.Generic;
using Dismantleheim.Core;
using Xunit;

namespace Dismantleheim.Tests.Core
{
	public class SelectionAndStateTests
	{
		private static TargetIdentity Id(
			uint n,
			string prefab,
			bool piece = true,
			bool removable = true,
			bool env = false,
			bool stale = false,
			bool preview = true)
		{
			TargetKind kind = Eligibility.Classify(piece, removable, env, preview);
			return new TargetIdentity(1, n, "world", prefab, piece, removable, env, kind, preview, stale);
		}

		[Fact]
		public void Queue_Toggle_AddRemove()
		{
			var q = new SelectionQueue();
			var a = Id(1, "woodwall");
			Assert.True(q.Toggle(a));
			Assert.Equal(1, q.Count);
			Assert.False(q.Toggle(a));
			Assert.Equal(0, q.Count);
		}

		[Fact]
		public void Queue_TryAdd_DoesNotUnselect_TryRemove_Does()
		{
			var q = new SelectionQueue();
			var a = Id(1, "woodwall");
			Assert.True(q.TryAdd(a));
			Assert.False(q.TryAdd(a));
			Assert.Equal(1, q.Count);
			Assert.True(q.TryRemove(a));
			Assert.Equal(0, q.Count);
			Assert.False(q.TryRemove(a));
		}

		[Fact]
		public void Queue_RespectsMaxCount()
		{
			var q = new SelectionQueue();
			Assert.True(q.Toggle(Id(1, "a"), 1));
			Assert.False(q.Toggle(Id(2, "b"), 1));
			Assert.Equal(1, q.Count);
		}

		[Fact]
		public void PrefabSampler_SampleClearSwitch()
		{
			var s = new PrefabSampler();
			Assert.True(s.TrySample("A", out string f1));
			Assert.Equal("A", f1);
			Assert.True(s.TrySample("A", out string f2));
			Assert.Null(f2);
		}

		[Fact]
		public void Eligibility_MassDismantle_RejectsEnvironmentEvenWithFilter()
		{
			var env = Id(1, "OakTree", piece: false, removable: false, env: true);
			Assert.False(Eligibility.IsEligible(
				env, OperationMode.MassDismantle, "OakTree", true, null, null, out var reason));
			Assert.Equal(EligibilityRejectReason.EnvironmentWithoutFilter, reason);
		}

		[Fact]
		public void Eligibility_MassDelete_Filtered_AllowsEnvironment()
		{
			var env = Id(1, "OakTree", piece: false, removable: false, env: true);
			Assert.True(Eligibility.IsEligible(
				env, OperationMode.MassDelete, "OakTree", true, null, null, out _));
		}

		[Fact]
		public void Eligibility_MassDelete_Unfiltered_RejectsEnvironment()
		{
			var env = Id(1, "OakTree", piece: false, removable: false, env: true);
			Assert.False(Eligibility.IsEligible(
				env, OperationMode.MassDelete, null, true, null, null, out _));
		}

		[Fact]
		public void Eligibility_BuildingPiece_Allowed_BothModes()
		{
			var p = Id(1, "woodwall");
			Assert.True(Eligibility.IsEligible(p, OperationMode.MassDismantle, null, true, null, null, out _));
			Assert.True(Eligibility.IsEligible(p, OperationMode.MassDelete, null, true, null, null, out _));
		}

		[Fact]
		public void Eligibility_Commit_IgnoresLaterCandidateFilter()
		{
			var wood = Id(1, "woodwall");
			Assert.False(Eligibility.IsEligible(wood, OperationMode.MassDismantle, "stonewall", true, null, null, out _));
			Assert.True(Eligibility.IsEligibleForCommit(wood, OperationMode.MassDismantle, true, null, null, out _));
		}

		[Fact]
		public void Eligibility_EnvWithoutPreview_StillEnvironment_MassDeleteFiltered()
		{
			var env = Id(1, "Birch2", piece: false, removable: false, env: true, preview: false);
			Assert.Equal(TargetKind.Environment, env.Kind);
			Assert.True(Eligibility.IsEligible(
				env, OperationMode.MassDelete, "Birch2", true, null, null, out _));
		}

		[Fact]
		public void Eligibility_PropPrefab_MassDeleteFiltered_Allowed()
		{
			var prop = Id(1, "prop_ashwood_bed", piece: false, removable: false, env: true, preview: false);
			Assert.Equal(TargetKind.Environment, prop.Kind);
			Assert.True(Eligibility.IsEligible(
				prop, OperationMode.MassDelete, "prop_ashwood_bed", true, null, null, out _));
			Assert.False(Eligibility.IsEligible(
				prop, OperationMode.MassDismantle, "prop_ashwood_bed", true, null, null, out _));
		}

		[Fact]
		public void ToolMatch_MassDismantleAndDelete_Labels()
		{
			Assert.True(IhToolNames.TryMatch("Dismantleheim - Mass Dismantle|foo|bar", out var d));
			Assert.Equal(OperationMode.MassDismantle, d);
			Assert.True(IhToolNames.TryMatch("Dismantleheim - Mass Delete|x|y", out var del));
			Assert.Equal(OperationMode.MassDelete, del);
			Assert.False(IhToolNames.TryMatch("wood_wall|wood_wall|", out _));
		}

		[Fact]
		public void Eligibility_MassDelete_FilterMatch_AllowsUnsupportedKind()
		{
			// Exact sample authorizes the prefab even when resolver could not classify components.
			var weird = Id(1, "Rock_3", piece: false, removable: false, env: false, preview: false);
			Assert.Equal(TargetKind.Unsupported, weird.Kind);
			Assert.True(Eligibility.IsEligible(
				weird, OperationMode.MassDelete, "Rock_3", true, null, null, out _));
			Assert.False(Eligibility.IsEligible(
				weird, OperationMode.MassDelete, null, true, null, null, out _));
		}

		[Fact]
		public void Policies_DivergeOnSameEnvironmentTarget()
		{
			var env = Id(1, "OakTree", piece: false, removable: false, env: true);
			var dismantle = new MassDismantlePolicy();
			var delete = new MassDeletePolicy();
			var dEntry = dismantle.PlanEntry(env, true, null, null, false);
			var delEntry = delete.PlanEntry(env, true, null, null, false);
			Assert.Equal(RemovalSkipReason.ModeIneligible, dEntry.SkipReason);
			Assert.Equal(RemovalSkipReason.None, delEntry.SkipReason);
			Assert.Equal(DropPolicy.NoDrops, delEntry.DropPolicy);
			Assert.Equal(DropPolicy.RefundsViaNative, dismantle.PredictedDropPolicy);
		}

		[Fact]
		public void Policies_SameBuildPiece_DifferentDropPolicy()
		{
			var wall = Id(1, "woodwall");
			var d = new MassDismantlePolicy().PlanEntry(wall, true, null, null, false);
			var del = new MassDeletePolicy().PlanEntry(wall, true, null, null, true);
			Assert.Equal(RemovalSkipReason.None, d.SkipReason);
			Assert.Equal(RemovalSkipReason.None, del.SkipReason);
			Assert.Equal(DropPolicy.RefundsViaNative, d.DropPolicy);
			Assert.Equal(DropPolicy.NoDrops, del.DropPolicy);
			Assert.True(del.ContentsLossWarning);
		}

		[Fact]
		public void StateMachine_EmptyQueue_CannotHold()
		{
			var sm = new DismantleStateMachine();
			sm.TryTransition(DismantleTransition.Activate, 0, out _);
			Assert.False(sm.TryTransition(DismantleTransition.BeginHold, 0, out _));
		}

		[Fact]
		public void StateMachine_HoldCancel_PreservesSelected()
		{
			var sm = new DismantleStateMachine();
			sm.TryTransition(DismantleTransition.Activate, 0, out _);
			sm.TryTransition(DismantleTransition.SelectionChanged, 2, out _);
			sm.TryTransition(DismantleTransition.BeginHold, 2, out _);
			sm.TryTransition(DismantleTransition.CancelHold, 2, out _);
			Assert.Equal(DismantleState.Selected, sm.State);
			Assert.False(sm.LastTransitionRequestedExecute);
		}

		[Fact]
		public void ConfirmationHold_CompletesOnce_NeedsRearm()
		{
			var h = new ConfirmationHold();
			h.Begin(0f);
			h.Update(1f, 1f);
			Assert.True(h.CompletedThisFrame);
			h.Begin(2f);
			h.Update(3f, 1f);
			Assert.False(h.CompletedThisFrame);
			h.Rearm();
			h.Begin(4f);
			h.Update(5f, 1f);
			Assert.True(h.CompletedThisFrame);
		}

		[Fact]
		public void RemovalPlan_RejectsWildcard_SkipsInvalid()
		{
			var queued = new List<TargetIdentity>
			{
				Id(1, "woodwall"),
				Id(2, "id=* area"),
				Id(3, "gone", stale: true)
			};
			var plan = RemovalPlan.FromQueue(queued, id => !id.IsStale, out var skipped);
			Assert.Equal(1, plan.Count);
			Assert.Equal(2, skipped.Count);
		}

		[Fact]
		public void OperationMode_Parse()
		{
			Assert.True(OperationModeUtil.TryParse("delete", out var m));
			Assert.Equal(OperationMode.MassDelete, m);
			Assert.Contains("No Drops", OperationModeUtil.Banner(OperationMode.MassDelete));
			Assert.Contains("Returns Materials", OperationModeUtil.Banner(OperationMode.MassDismantle));
		}
	}
}
