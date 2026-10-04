using Dismantleheim.Core;
using Dismantleheim.Selection;
using HarmonyLib;
using UnityEngine;

namespace Dismantleheim.Input
{
	/// <summary>
	/// While Dismantleheim is active, prevent vanilla middle-click single-piece remove
	/// so hold-to-confirm owns Mouse3. Player.RemovePiece() takes no args (hover target).
	/// </summary>
	[HarmonyPatch(typeof(Player), "RemovePiece")]
	internal static class PlayerRemovePiecePatch
	{
		private static bool Prefix(Player __instance)
		{
			DismantleSession session = DismantleheimPlugin.Session;
			if (session == null || !session.IsActive)
			{
				return true;
			}

			if ((Object)(object)__instance != (Object)(object)Player.m_localPlayer)
			{
				return true;
			}

			if (session.StateMachine.State == DismantleState.Executing)
			{
				return true;
			}

			DismantleheimPlugin.DebugLog("Suppressed vanilla RemovePiece while Dismantleheim active.");
			return false;
		}
	}
}
