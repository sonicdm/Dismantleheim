using Dismantleheim.Selection;
using HarmonyLib;
using UnityEngine;

namespace Dismantleheim.Input
{
	internal static class Ownership
	{
		internal static bool BlocksVanillaActions()
		{
			if (!DismantleheimPlugin.IsModEnabled())
			{
				return false;
			}

			DismantleSession session = DismantleheimPlugin.Session;
			return session != null && session.IsActive;
		}
	}

	/// <summary>Block placement while Dismantleheim owns input.</summary>
	[HarmonyPatch(typeof(Player), "TryPlacePiece")]
	internal static class TryPlacePiecePatch
	{
		private static bool Prefix(Player __instance, ref bool __result)
		{
			if (!Ownership.BlocksVanillaActions())
			{
				return true;
			}

			if ((Object)(object)__instance != (Object)(object)Player.m_localPlayer)
			{
				return true;
			}

			__result = false;
			return false;
		}
	}

	[HarmonyPatch(typeof(Player), "PlacePiece")]
	internal static class PlacePiecePatch
	{
		private static bool Prefix(Player __instance)
		{
			if (!Ownership.BlocksVanillaActions())
			{
				return true;
			}

			if ((Object)(object)__instance != (Object)(object)Player.m_localPlayer)
			{
				return true;
			}

			return false;
		}
	}

	/// <summary>Block vanilla Mouse3 remove while active (executor uses WearNTear after CheckCanRemovePiece).</summary>
	[HarmonyPatch(typeof(Player), "RemovePiece")]
	internal static class PlayerRemovePiecePatch
	{
		private static bool Prefix(Player __instance)
		{
			if (!Ownership.BlocksVanillaActions())
			{
				return true;
			}

			if ((Object)(object)__instance != (Object)(object)Player.m_localPlayer)
			{
				return true;
			}

			DismantleheimPlugin.DebugLog("Suppressed vanilla RemovePiece while Dismantleheim active.");
			return false;
		}
	}

	/// <summary>
	/// Block vanilla Shift+Mouse3 pipette (CopyPiece). Private method — targeted via AccessTools
	/// so ScriptEngine reloads still bind correctly.
	/// </summary>
	[HarmonyPatch]
	internal static class PlayerCopyPiecePatch
	{
		private static System.Reflection.MethodBase TargetMethod()
		{
			return AccessTools.Method(typeof(Player), "CopyPiece");
		}

		private static bool Prefix(Player __instance, ref bool __result)
		{
			if (!Ownership.BlocksVanillaActions())
			{
				return true;
			}

			if ((Object)(object)__instance != (Object)(object)Player.m_localPlayer)
			{
				return true;
			}

			__result = false;
			return false;
		}
	}

	/// <summary>
	/// Hard-stop the pipette "missing requirement" toast while we own Shift+M3 for sampling.
	/// </summary>
	[HarmonyPatch(typeof(Character), nameof(Character.Message))]
	internal static class CharacterMessageMissingRequirementPatch
	{
		private static bool Prefix(
			Character __instance,
			MessageHud.MessageType type,
			string msg,
			int amount,
			Sprite icon,
			bool log)
		{
			if (string.IsNullOrEmpty(msg) || !Ownership.BlocksVanillaActions())
			{
				return true;
			}

			if ((Object)(object)__instance != (Object)(object)Player.m_localPlayer)
			{
				return true;
			}

			if (msg.IndexOf("missingrequirement", System.StringComparison.OrdinalIgnoreCase) >= 0
			    || msg == "$msg_missingrequirement")
			{
				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(Player), "PlayerAttackInput")]
	internal static class PlayerAttackInputPatch
	{
		private static bool Prefix(Player __instance)
		{
			if (!Ownership.BlocksVanillaActions())
			{
				return true;
			}

			if ((Object)(object)__instance != (Object)(object)Player.m_localPlayer)
			{
				return true;
			}

			return false;
		}
	}
}
