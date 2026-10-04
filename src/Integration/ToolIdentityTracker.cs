using Dismantleheim.Selection;
using HarmonyLib;
using UnityEngine;

namespace Dismantleheim.Integration
{
	/// <summary>
	/// Persistent ownership: exit on unequip, leave place-mode, select another piece/tool,
	/// world/session epoch change, or missing local player.
	/// </summary>
	internal static class ToolIdentityTracker
	{
		private static string _activateWorldSession;
		private static string _activateRightItem;
		private static bool _hadPlaceMode;

		internal static void OnActivated()
		{
			_activateWorldSession = TargetResolver.CurrentWorldSessionKey();
			Player player = Player.m_localPlayer;
			_activateRightItem = DescribeRightItem(player);
			_hadPlaceMode = (Object)(object)player != (Object)null && player.InPlaceMode();
		}

		internal static void OnDeactivated()
		{
			_activateWorldSession = null;
			_activateRightItem = null;
			_hadPlaceMode = false;
		}

		internal static void Tick(DismantleSession session)
		{
			if (session == null || !session.IsActive)
			{
				return;
			}

			Player player = Player.m_localPlayer;
			if ((Object)(object)player == (Object)null)
			{
				session.ResetHard("no-player");
				return;
			}

			string world = TargetResolver.CurrentWorldSessionKey();
			if (_activateWorldSession != null
			    && !string.Equals(world, _activateWorldSession, System.StringComparison.Ordinal))
			{
				session.ResetHard("world-session-changed");
				return;
			}

			string right = DescribeRightItem(player);
			if (_activateRightItem != null
			    && !string.Equals(right, _activateRightItem, System.StringComparison.Ordinal))
			{
				session.Deactivate("held-item-changed");
				return;
			}

			bool place = player.InPlaceMode();
			if (_hadPlaceMode && !place)
			{
				session.Deactivate("left-place-mode");
				return;
			}

			_hadPlaceMode = place || _hadPlaceMode;
		}

		internal static void NotifySelectedPieceChanged()
		{
			DismantleSession session = DismantleheimPlugin.Session;
			if (session == null || !session.IsActive)
			{
				return;
			}

			if (session.IgnorePieceChangeFrames > 0)
			{
				return;
			}

			session.Deactivate("selected-piece-changed");
		}

		private static string DescribeRightItem(Player player)
		{
			if ((Object)(object)player == (Object)null)
			{
				return string.Empty;
			}

			ItemDrop.ItemData tool = player.RightItem;
			if (tool == null || tool.m_shared == null)
			{
				return "empty";
			}

			return (tool.m_shared.m_name ?? "") + "|" + (tool.m_dropPrefab != null ? tool.m_dropPrefab.name : "");
		}
	}

	[HarmonyPatch(typeof(Player), "SetSelectedPiece", typeof(Vector2Int))]
	internal static class PlayerSetSelectedPieceIndexPatch
	{
		private static void Postfix(Player __instance)
		{
			if ((Object)(object)__instance != (Object)(object)Player.m_localPlayer)
			{
				return;
			}

			ToolIdentityTracker.NotifySelectedPieceChanged();
		}
	}

	[HarmonyPatch(typeof(Player), "SetSelectedPiece", typeof(Piece))]
	internal static class PlayerSetSelectedPieceObjPatch
	{
		private static void Postfix(Player __instance)
		{
			if ((Object)(object)__instance != (Object)(object)Player.m_localPlayer)
			{
				return;
			}

			ToolIdentityTracker.NotifySelectedPieceChanged();
		}
	}

	/// <summary>Hard-clear queues on logout / disconnect regardless of ClearQueueOnToolSwitch.</summary>
	[HarmonyPatch(typeof(Game), nameof(Game.Logout), typeof(bool), typeof(bool))]
	internal static class GameLogoutHardResetPatch
	{
		private static void Prefix()
		{
			DismantleSession session = DismantleheimPlugin.Session;
			if (session != null)
			{
				session.ResetHard("logout");
			}
		}
	}
}
