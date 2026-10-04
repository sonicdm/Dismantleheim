namespace Dismantleheim.Core
{
	/// <summary>
	/// Hold-to-confirm progress using an injected clock (unscaled seconds).
	/// Completes once; requires release (Rearm) before another completion.
	/// </summary>
	public sealed class ConfirmationHold
	{
		private float _startTime = -1f;
		private bool _armed = true;
		private bool _holding;

		public bool IsHolding => _holding;

		public float Progress { get; private set; }

		public bool CompletedThisFrame { get; private set; }

		public void Begin(float nowSeconds)
		{
			CompletedThisFrame = false;
			if (!_armed)
			{
				return;
			}

			_holding = true;
			_startTime = nowSeconds;
			Progress = 0f;
		}

		public void Update(float nowSeconds, float holdSeconds)
		{
			CompletedThisFrame = false;
			if (!_holding || !_armed)
			{
				return;
			}

			float duration = holdSeconds <= 0.01f ? 0.01f : holdSeconds;
			float elapsed = nowSeconds - _startTime;
			if (elapsed < 0f)
			{
				elapsed = 0f;
			}

			Progress = elapsed / duration;
			if (Progress >= 1f)
			{
				Progress = 1f;
				CompletedThisFrame = true;
				_holding = false;
				_armed = false;
			}
		}

		public void Cancel()
		{
			_holding = false;
			_startTime = -1f;
			Progress = 0f;
			CompletedThisFrame = false;
		}

		/// <summary>Call on key/button up so a new hold can complete once.</summary>
		public void Rearm()
		{
			_armed = true;
			if (!_holding)
			{
				Progress = 0f;
			}
		}
	}
}
