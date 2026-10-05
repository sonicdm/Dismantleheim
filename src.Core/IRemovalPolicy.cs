using System.Collections.Generic;

namespace Dismantleheim.Core
{
	/// <summary>
	/// Pure policy surface for mode eligibility / drop prediction (no Unity).
	/// Runtime adapters implement live execute separately.
	/// </summary>
	public interface IRemovalPolicy
	{
		OperationMode Mode { get; }

		DropPolicy PredictedDropPolicy { get; }

		bool AllowsEnvironment { get; }

		/// <summary>
		/// Mode-level plan line for a revalidated target (dry-run prediction).
		/// </summary>
		RemovalPlanEntry PlanEntry(
			TargetIdentity target,
			bool allowEnvironmentWithFilter,
			ISet<string> extraDeny,
			ISet<string> extraAllow,
			bool contentsLossWarning);
	}

	public sealed class MassDismantlePolicy : IRemovalPolicy
	{
		public OperationMode Mode => OperationMode.MassDismantle;

		public DropPolicy PredictedDropPolicy => DropPolicy.RefundsViaNative;

		public bool AllowsEnvironment => false;

		public RemovalPlanEntry PlanEntry(
			TargetIdentity target,
			bool allowEnvironmentWithFilter,
			ISet<string> extraDeny,
			ISet<string> extraAllow,
			bool contentsLossWarning)
		{
			if (!Eligibility.IsEligibleForCommit(
				    target,
				    Mode,
				    allowEnvironmentWithFilter,
				    extraDeny,
				    extraAllow,
				    out EligibilityRejectReason reason))
			{
				return new RemovalPlanEntry(
					target,
					Mode,
					DropPolicy.None,
					RemovalSkipReason.ModeIneligible,
					reason.ToString(),
					false);
			}

			return new RemovalPlanEntry(
				target,
				Mode,
				DropPolicy.RefundsViaNative,
				RemovalSkipReason.None,
				"native refund path",
				false);
		}
	}

	public sealed class MassDeletePolicy : IRemovalPolicy
	{
		public OperationMode Mode => OperationMode.MassDelete;

		public DropPolicy PredictedDropPolicy => DropPolicy.NoDrops;

		public bool AllowsEnvironment => true;

		public RemovalPlanEntry PlanEntry(
			TargetIdentity target,
			bool allowEnvironmentWithFilter,
			ISet<string> extraDeny,
			ISet<string> extraAllow,
			bool contentsLossWarning)
		{
			if (!Eligibility.IsEligibleForCommit(
				    target,
				    Mode,
				    allowEnvironmentWithFilter,
				    extraDeny,
				    extraAllow,
				    out EligibilityRejectReason reason))
			{
				return new RemovalPlanEntry(
					target,
					Mode,
					DropPolicy.None,
					RemovalSkipReason.ModeIneligible,
					reason.ToString(),
					false);
			}

			return new RemovalPlanEntry(
				target,
				Mode,
				DropPolicy.NoDrops,
				RemovalSkipReason.None,
				contentsLossWarning ? "no drops; contents-loss warning" : "no drops",
				contentsLossWarning);
		}
	}

	public static class RemovalPolicyFactory
	{
		public static IRemovalPolicy For(OperationMode mode)
		{
			return mode == OperationMode.MassDelete
				? (IRemovalPolicy)new MassDeletePolicy()
				: new MassDismantlePolicy();
		}
	}
}
