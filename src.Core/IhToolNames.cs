using System;

namespace Dismantleheim.Core
{
	/// <summary>Match Infinity Hammer tool Piece labels to Dismantleheim modes.</summary>
	public static class IhToolNames
	{
		public static bool TryMatch(string label, out OperationMode mode)
		{
			mode = OperationMode.MassDismantle;
			if (string.IsNullOrEmpty(label))
			{
				return false;
			}

			if (label.IndexOf("Mass Delete", StringComparison.OrdinalIgnoreCase) >= 0
			    || label.IndexOf("mode delete", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				mode = OperationMode.MassDelete;
				return true;
			}

			if (label.IndexOf("Mass Dismantle", StringComparison.OrdinalIgnoreCase) >= 0
			    || label.IndexOf("mode dismantle", StringComparison.OrdinalIgnoreCase) >= 0
			    || (label.IndexOf("Dismantleheim", StringComparison.OrdinalIgnoreCase) >= 0
			        && label.IndexOf("Delete", StringComparison.OrdinalIgnoreCase) < 0))
			{
				mode = OperationMode.MassDismantle;
				return true;
			}

			return false;
		}
	}
}
