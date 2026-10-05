namespace Dismantleheim.Core
{
	public enum OperationMode
	{
		MassDismantle = 0,
		MassDelete = 1
	}

	public enum DropPolicy
	{
		None = 0,
		RefundsViaNative = 1,
		NoDrops = 2
	}

	public static class OperationModeUtil
	{
		public static string Banner(OperationMode mode)
		{
			return mode == OperationMode.MassDelete
				? "MASS DELETE — No Drops"
				: "MASS DISMANTLE — Returns Materials";
		}

		public static string ShortName(OperationMode mode)
		{
			return mode == OperationMode.MassDelete ? "delete" : "dismantle";
		}

		public static bool TryParse(string text, out OperationMode mode)
		{
			mode = OperationMode.MassDismantle;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}

			switch (text.Trim().ToLowerInvariant())
			{
				case "dismantle":
				case "massdismantle":
				case "mass-dismantle":
					mode = OperationMode.MassDismantle;
					return true;
				case "delete":
				case "massdelete":
				case "mass-delete":
					mode = OperationMode.MassDelete;
					return true;
				default:
					return false;
			}
		}
	}
}
