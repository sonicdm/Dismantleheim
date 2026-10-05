using System.Collections.Generic;
using System.Text;
using Dismantleheim.Core;

namespace Dismantleheim.Removal
{
	internal static class ExecutionResultReporter
	{
		public static string Summarize(IReadOnlyList<RemovalResult> results, OperationMode mode)
		{
			int removed = 0;
			int planned = 0;
			int skipped = 0;
			int failed = 0;
			int pending = 0;
			if (results != null)
			{
				foreach (RemovalResult r in results)
				{
					if (r == null)
					{
						continue;
					}

					if (r.Removed)
					{
						removed++;
					}
					else if (r.SkipReason == RemovalSkipReason.PendingNetwork)
					{
						pending++;
					}
					else if (r.SkipReason == RemovalSkipReason.DryRun)
					{
						planned++;
					}
					else if (r.SkipReason == RemovalSkipReason.None)
					{
						failed++;
					}
					else
					{
						skipped++;
					}
				}
			}

			var sb = new StringBuilder();
			sb.Append(OperationModeUtil.ShortName(mode));
			if (planned > 0)
			{
				sb.Append(": DRY-RUN would-remove=").Append(planned);
				sb.Append(" skipped=").Append(skipped);
				sb.Append(" failed=").Append(failed);
			}
			else
			{
				sb.Append(": removed=").Append(removed);
				sb.Append(" pending=").Append(pending);
				sb.Append(" skipped=").Append(skipped);
				sb.Append(" failed=").Append(failed);
			}

			return sb.ToString();
		}

		public static void LogAll(IReadOnlyList<RemovalResult> results)
		{
			if (results == null)
			{
				return;
			}

			foreach (RemovalResult r in results)
			{
				if (r == null)
				{
					continue;
				}

				string tag = r.Removed
					? "REMOVED "
					: r.SkipReason == RemovalSkipReason.PendingNetwork
						? "PENDING "
						: r.SkipReason == RemovalSkipReason.DryRun
							? "PLAN "
							: "SKIP ";
				string line = tag
				              + r.Mode + " "
				              + (r.Target != null ? r.Target.PrefabName + "#" + r.Target.NetworkId : "?")
				              + " drops=" + r.DropPolicy
				              + " " + r.SkipReason + " " + r.Message;
				DismantleheimPlugin.ModLogger?.LogInfo(line);
			}
		}
	}
}
