using System.Collections.Generic;
using Dismantleheim.Core;
using Dismantleheim.Selection;
using UnityEngine;

namespace Dismantleheim.UI
{
	internal static class HoverHighlighter
	{
		private static readonly HashSet<long> QueuedKeys = new HashSet<long>();

		public static void Tick(DismantleSession session)
		{
			if (session == null || !session.IsActive)
			{
				ClearAll();
				return;
			}

			QueuedKeys.Clear();
			foreach (TargetIdentity id in session.Queue.Entries)
			{
				if (id == null)
				{
					continue;
				}

				GameObject go = Removal.ExactObjectExecutor.ResolveInstance(id);
				if ((Object)(object)go == (Object)null)
				{
					continue;
				}

				WearNTear wear = go.GetComponent<WearNTear>();
				if ((Object)(object)wear != (Object)null)
				{
					wear.Highlight();
					QueuedKeys.Add(id.UserId ^ id.ObjectId);
				}
			}

			TargetIdentity hover = session.HoverTarget;
			if (hover == null)
			{
				return;
			}

			bool eligible = Eligibility.IsEligible(
				hover,
				session.Sampler.ActiveFilter,
				session.AllowEnvWithFilter,
				session.ExtraDeny(),
				session.ExtraAllow(),
				out _);
			if (!eligible)
			{
				return;
			}

			GameObject hoverGo = Removal.ExactObjectExecutor.ResolveInstance(hover);
			if ((Object)(object)hoverGo == (Object)null)
			{
				return;
			}

			WearNTear hoverWear = hoverGo.GetComponent<WearNTear>();
			if ((Object)(object)hoverWear != (Object)null)
			{
				hoverWear.Highlight();
			}
		}

		public static void ClearAll()
		{
			QueuedKeys.Clear();
		}
	}
}
