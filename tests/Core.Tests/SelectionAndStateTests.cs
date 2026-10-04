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
		public void Queue_FilterChange_DoesNotDropEntries()
		{
			var q = new SelectionQueue();
			q.Toggle(Id(1, "woodwall"));
			q.Toggle(Id(2, "stonwall"));
			var sampler = new PrefabSampler();
			sampler.TrySample("woodwall", out _);
			Assert.Equal(2, q.Count);
		}

		[Fact]
		public void PrefabSampler_SampleClearSwitch()
		{
			var s = new PrefabSampler();
			Assert.True(s.TrySample("A", out string f1));
			Assert.Equal("A", f1);
			Assert.True(s.TrySample("A", out string f2));
			Assert.Null(f2);
			Assert.True(s.TrySample("B", out string f3));
			Assert.Equal("B", f3);
			Assert.False(s.TrySample("", out _));
		}

		[Fact]
		public void Eligibility_Unfiltered_RejectsEnvironment()
		{
			var env = Id(1, "OakTree", piece: false, removable: false, env: true);
			Assert.False(Eligibility.IsEligible(env, null, true, null, null, out var reason));
			Assert.Equal(EligibilityRejectReason.EnvironmentWithoutFilter, reason);
		}

		[Fact]
		public void Eligibility_Filtered_AllowsMatchingEnvironment()
		{
			var env = Id(1, "OakTree", piece: false, removable: false, env: true);
			Assert.True(Eligibility.IsEligible(env, "OakTree", true, null, null, out _));
		}

		[Fact]
		public void Eligibility_BuildingPiece_Allowed()
		{
			var p = Id(1, "woodwall");
			Assert.True(Eligibility.IsEligible(p, null, true, null, null, out _));
		}

		[Fact]
		public void Eligibility_Unsupported_RejectedEvenWithExtraAllow()
		{
			var bad = Id(1, "MysteryThing", piece: false, removable: false, env: false);
			Assert.Equal(TargetKind.Unsupported, bad.Kind);
			var allow = new HashSet<string> { "MysteryThing" };
			Assert.False(Eligibility.IsEligible(bad, null, true, null, allow, out var reason));
			Assert.Equal(EligibilityRejectReason.UnsupportedType, reason);
		}

		[Fact]
		public void Eligibility_ExtraAllow_DoesNotBypassEnvironmentWithoutFilter()
		{
			var env = Id(1, "OakTree", piece: false, removable: false, env: true);
			var allow = new HashSet<string> { "OakTree" };
			Assert.False(Eligibility.IsEligible(env, null, true, null, allow, out var reason));
			Assert.Equal(EligibilityRejectReason.EnvironmentWithoutFilter, reason);
		}

		[Fact]
		public void Eligibility_ExtraAllow_OverridesExtraDeny_ForValidPiece()
		{
			var p = Id(1, "woodwall");
			var deny = new HashSet<string> { "woodwall" };
			var allow = new HashSet<string> { "woodwall" };
			Assert.True(Eligibility.IsEligible(p, null, true, deny, allow, out _));
		}

		[Fact]
		public void Eligibility_EnvWithoutPreview_Unsupported()
		{
			var env = Id(1, "MineRock", piece: false, removable: false, env: true, preview: false);
			Assert.Equal(TargetKind.Unsupported, env.Kind);
			Assert.False(Eligibility.IsEligible(env, "MineRock", true, null, null, out var reason));
			Assert.Equal(EligibilityRejectReason.UnsupportedType, reason);
		}

		[Fact]
		public void Eligibility_Commit_IgnoresLaterCandidateFilter()
		{
			var wood = Id(1, "woodwall");
			var stone = Id(2, "stonewall");
			Assert.True(Eligibility.IsEligible(wood, null, true, null, null, out _));
			Assert.True(Eligibility.IsEligible(stone, "stonewall", true, null, null, out _));

			// After filter switches to stonewall, candidate selection would reject wood —
			// but commit must keep previously queued wood.
			Assert.False(Eligibility.IsEligible(wood, "stonewall", true, null, null, out _));
			Assert.True(Eligibility.IsEligibleForCommit(wood, true, null, null, out _));
			Assert.True(Eligibility.IsEligibleForCommit(stone, true, null, null, out _));
		}

		[Fact]
		public void Eligibility_Commit_KeepsQueuedEnvironmentAfterFilterClear()
		{
			var env = Id(1, "OakTree", piece: false, removable: false, env: true);
			Assert.True(Eligibility.IsEligible(env, "OakTree", true, null, null, out _));
			Assert.False(Eligibility.IsEligible(env, null, true, null, null, out _));
			Assert.True(Eligibility.IsEligibleForCommit(env, true, null, null, out _));
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
			Assert.Equal(DismantleState.Selected, sm.State);
			sm.TryTransition(DismantleTransition.BeginHold, 2, out _);
			sm.TryTransition(DismantleTransition.CancelHold, 2, out _);
			Assert.Equal(DismantleState.Selected, sm.State);
			Assert.False(sm.LastTransitionRequestedExecute);
		}

		[Fact]
		public void StateMachine_ToolSwitch_NoExecute()
		{
			var sm = new DismantleStateMachine();
			sm.TryTransition(DismantleTransition.Activate, 1, out _);
			sm.TryTransition(DismantleTransition.BeginHold, 1, out _);
			sm.TryTransition(DismantleTransition.ToolSwitch, 1, out _);
			Assert.Equal(DismantleState.Inactive, sm.State);
			Assert.False(sm.LastTransitionRequestedExecute);
		}

		[Fact]
		public void ConfirmationHold_EarlyRelease()
		{
			var h = new ConfirmationHold();
			h.Begin(0f);
			h.Update(0.3f, 1f);
			Assert.True(h.Progress < 1f);
			Assert.False(h.CompletedThisFrame);
			h.Cancel();
			Assert.Equal(0f, h.Progress);
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
		public void TargetIdentity_Equality_ByZdoAndSession()
		{
			var a = Id(10, "a");
			var b = new TargetIdentity(1, 10, "world", "other", true, true, false, TargetKind.BuildPiece, true);
			Assert.True(a.Equals(b));
		}
	}
}
