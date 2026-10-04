using System;
using System.Reflection;
using HarmonyLib;

namespace Dismantleheim.Removal
{
	/// <summary>
	/// Player.CheckCanRemovePiece is private; invoke via Harmony AccessTools.
	/// </summary>
	internal static class PlayerRemoveAccess
	{
		private static readonly MethodInfo CheckCanRemovePieceMethod =
			AccessTools.Method(typeof(Player), "CheckCanRemovePiece", new[] { typeof(Piece) });

		internal static bool CanRemovePiece(Player player, Piece piece)
		{
			if ((UnityEngine.Object)(object)player == (UnityEngine.Object)null
			    || (UnityEngine.Object)(object)piece == (UnityEngine.Object)null)
			{
				return false;
			}

			if (CheckCanRemovePieceMethod == null)
			{
				DismantleheimPlugin.ModLogger?.LogWarning("CheckCanRemovePiece not found; denying remove.");
				return false;
			}

			try
			{
				object result = CheckCanRemovePieceMethod.Invoke(player, new object[] { piece });
				return result is bool b && b;
			}
			catch (Exception ex)
			{
				DismantleheimPlugin.ModLogger?.LogWarning("CheckCanRemovePiece invoke failed: " + ex.Message);
				return false;
			}
		}
	}
}
