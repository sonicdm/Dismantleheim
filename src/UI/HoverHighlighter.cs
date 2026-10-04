using Dismantleheim.Core;
using Dismantleheim.Selection;
using UnityEngine;

namespace Dismantleheim.UI
{
	internal static class HoverHighlighter
	{
		public static void Tick(DismantleSession session)
		{
			if (session == null || !session.IsActive)
			{
				return;
			}

			// Eligibility is evaluated for the hover target; vanilla hover outline remains.
			// Queued count is shown by ConfirmationRing GUI.
			TargetIdentity hover = session.HoverTarget;
			if (hover == null)
			{
				return;
			}

			Eligibility.IsEligible(
				hover,
				session.Sampler.ActiveFilter,
				session.AllowEnvWithFilter,
				session.ExtraDeny(),
				session.ExtraAllow(),
				out _);
		}
	}
}
