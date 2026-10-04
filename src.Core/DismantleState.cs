namespace Dismantleheim.Core
{
	public enum DismantleState
	{
		Inactive,
		ActiveIdle,
		Selected,
		HoldingConfirm,
		Validating,
		Executing
	}

	public enum DismantleTransition
	{
		Activate,
		Deactivate,
		SelectionChanged,
		BeginHold,
		CancelHold,
		HoldComplete,
		ValidationDone,
		ExecutionDone,
		ToolSwitch
	}

	public sealed class DismantleStateMachine
	{
		public DismantleState State { get; private set; } = DismantleState.Inactive;

		public bool LastTransitionRequestedExecute { get; private set; }

		public bool TryTransition(DismantleTransition transition, int queueCount, out DismantleState newState)
		{
			LastTransitionRequestedExecute = false;
			DismantleState current = State;
			newState = current;

			if (transition == DismantleTransition.ToolSwitch || transition == DismantleTransition.Deactivate)
			{
				State = DismantleState.Inactive;
				newState = State;
				return true;
			}

			switch (current)
			{
				case DismantleState.Inactive:
					if (transition == DismantleTransition.Activate)
					{
						State = DismantleState.ActiveIdle;
						newState = State;
						return true;
					}
					break;

				case DismantleState.ActiveIdle:
					if (transition == DismantleTransition.SelectionChanged)
					{
						State = queueCount > 0 ? DismantleState.Selected : DismantleState.ActiveIdle;
						newState = State;
						return true;
					}
					if (transition == DismantleTransition.BeginHold)
					{
						if (queueCount <= 0)
						{
							return false;
						}
						State = DismantleState.HoldingConfirm;
						newState = State;
						return true;
					}
					break;

				case DismantleState.Selected:
					if (transition == DismantleTransition.SelectionChanged)
					{
						State = queueCount > 0 ? DismantleState.Selected : DismantleState.ActiveIdle;
						newState = State;
						return true;
					}
					if (transition == DismantleTransition.BeginHold)
					{
						if (queueCount <= 0)
						{
							return false;
						}
						State = DismantleState.HoldingConfirm;
						newState = State;
						return true;
					}
					break;

				case DismantleState.HoldingConfirm:
					if (transition == DismantleTransition.CancelHold)
					{
						State = queueCount > 0 ? DismantleState.Selected : DismantleState.ActiveIdle;
						newState = State;
						return true;
					}
					if (transition == DismantleTransition.HoldComplete)
					{
						if (queueCount <= 0)
						{
							State = DismantleState.ActiveIdle;
							newState = State;
							return true;
						}
						State = DismantleState.Validating;
						newState = State;
						return true;
					}
					break;

				case DismantleState.Validating:
					if (transition == DismantleTransition.ValidationDone)
					{
						State = DismantleState.Executing;
						LastTransitionRequestedExecute = true;
						newState = State;
						return true;
					}
					break;

				case DismantleState.Executing:
					if (transition == DismantleTransition.ExecutionDone)
					{
						State = queueCount > 0 ? DismantleState.Selected : DismantleState.ActiveIdle;
						newState = State;
						return true;
					}
					break;
			}

			return false;
		}

		public void ForceInactive()
		{
			State = DismantleState.Inactive;
			LastTransitionRequestedExecute = false;
		}
	}
}
