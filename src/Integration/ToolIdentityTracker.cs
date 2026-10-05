using Dismantleheim.Core;
using Dismantleheim.Selection;
using HarmonyLib;
using UnityEngine;

namespace Dismantleheim.Integration
{
	/// <summary>
	/// Ownership + IH tool selection. Menu select uses BuildUi; holster/re-equip must
	/// deactivate HUD and re-activate when the hammer comes back with our tool still selected.
	/// </summary>
	internal static class ToolIdentityTracker
	{
		private const int RealToolSwapDebounceFrames = 8;
		private const int EmptyHandDebounceFrames = 12;
		private const int LeftPlaceDebounceFrames = 12;

		private static string _activateWorldSession;
		private static string _activateRightItem;
		private static int _realSwapFrames;
		private static int _emptyHandFrames;
		private static int _leftPlaceFrames;

		internal static void OnActivated()
		{
			_activateWorldSession = TargetResolver.CurrentWorldSessionKey();
			Player player = Player.m_localPlayer;
			_activateRightItem = DescribeRightItem(player);
			_realSwapFrames = 0;
			_emptyHandFrames = 0;
			_leftPlaceFrames = 0;
		}

		internal static void OnDeactivated()
		{
			_activateWorldSession = null;
			_activateRightItem = null;
			_realSwapFrames = 0;
			_emptyHandFrames = 0;
			_leftPlaceFrames = 0;
		}

		internal static void RefreshBaseline(string reason)
		{
			Player player = Player.m_localPlayer;
			string next = DescribeRightItem(player);
			if (next != "empty")
			{
				_activateRightItem = next;
			}

			_realSwapFrames = 0;
			_emptyHandFrames = 0;
			_leftPlaceFrames = 0;
		}

		internal static void Tick(DismantleSession session)
		{
			if (session == null)
			{
				return;
			}

			Player player = Player.m_localPlayer;
			if ((Object)(object)player == (Object)null)
			{
				if (session.IsActive)
				{
					session.ResetHard("no-player");
				}

				return;
			}

			if (!session.IsActive)
			{
				TryReactivateFromEquippedTool(player);
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
			bool place = player.InPlaceMode();

			if (session.IgnorePieceChangeFrames > 0)
			{
				if (right != "empty"
				    && !string.Equals(right, _activateRightItem, System.StringComparison.Ordinal))
				{
					_activateRightItem = right;
				}

				_realSwapFrames = 0;
				_emptyHandFrames = 0;
				_leftPlaceFrames = 0;
				return;
			}

			if (!place)
			{
				_leftPlaceFrames++;
				_emptyHandFrames = 0;
				_realSwapFrames = 0;
				if (_leftPlaceFrames >= LeftPlaceDebounceFrames)
				{
					session.Deactivate("left-place-mode");
				}

				return;
			}

			_leftPlaceFrames = 0;

			if (right == "empty")
			{
				_emptyHandFrames++;
				_realSwapFrames = 0;
				if (_emptyHandFrames >= EmptyHandDebounceFrames)
				{
					session.Deactivate("held-item-empty");
				}

				return;
			}

			_emptyHandFrames = 0;

			if (_activateRightItem == null || _activateRightItem == "empty")
			{
				_activateRightItem = right;
				_realSwapFrames = 0;
				return;
			}

			if (string.Equals(right, _activateRightItem, System.StringComparison.Ordinal))
			{
				_realSwapFrames = 0;
				return;
			}

			if (IsHammerLike(right))
			{
				_activateRightItem = right;
				_realSwapFrames = 0;
				return;
			}

			_realSwapFrames++;
			if (_realSwapFrames >= RealToolSwapDebounceFrames)
			{
				session.Deactivate("tool-swapped");
			}
		}

		private static void TryReactivateFromEquippedTool(Player player)
		{
			if (!player.InPlaceMode())
			{
				return;
			}

			string right = DescribeRightItem(player);
			if (!IsHammerLike(right))
			{
				return;
			}

			// IH patches GetSelectedPiece to return null for Instant tools — read slot directly.
			Piece tablePiece = null;
			try
			{
				PieceTable pt = GetBuildPieceTable(player);
				if ((Object)(object)pt != (Object)null)
				{
					tablePiece = pt.GetPiece(pt.GetSelectedCategory(), pt.GetSelectedIndex());
					if ((Object)(object)tablePiece == (Object)null)
					{
						tablePiece = pt.GetSelectedPiece();
					}
				}
			}
			catch
			{
				return;
			}

			TryActivateFromPiece(tablePiece, "re-equip-selected-tool");
		}

		internal static void NotifySelectedPieceChanged()
		{
			if (!DismantleheimPlugin.IsModEnabled() || !DismantleheimPlugin.PatchesReady)
			{
				return;
			}

			DismantleSession session = DismantleheimPlugin.Session;
			if (session == null)
			{
				return;
			}

			Player player = Player.m_localPlayer;
			if ((Object)(object)player == (Object)null)
			{
				return;
			}

			Piece piece = player.GetSelectedPiece();
			string label = DescribeSelectedPiece(piece);
			Piece tablePiece = null;
			string tableLabel = null;
			try
			{
				PieceTable pt = GetBuildPieceTable(player);
				if ((Object)(object)pt != (Object)null)
				{
					tablePiece = pt.GetPiece(pt.GetSelectedCategory(), pt.GetSelectedIndex());
					if ((Object)(object)tablePiece == (Object)null)
					{
						tablePiece = pt.GetSelectedPiece();
					}

					tableLabel = DescribeSelectedPiece(tablePiece);
				}
			}
			catch
			{
				// ignored
			}

			if (IhToolNames.TryMatch(label, out OperationMode modeGet))
			{
				session.SetMode(modeGet, "ih-tool-selected");
				return;
			}

			if (IhToolNames.TryMatch(tableLabel, out OperationMode modeTable))
			{
				session.SetMode(modeTable, "ih-tool-selected-table");
				return;
			}

			if (!session.IsActive || session.IgnorePieceChangeFrames > 0)
			{
				return;
			}

			bool pieceMenu = false;
			try
			{
				pieceMenu = Hud.IsPieceSelectionVisible();
			}
			catch
			{
				// ignored
			}

			if (!pieceMenu)
			{
				return;
			}

			session.Deactivate("selected-piece-changed");
		}

		private static string DescribeSelectedPiece(Piece piece)
		{
			if ((Object)(object)piece == (Object)null)
			{
				return null;
			}

			string name = piece.m_name ?? string.Empty;
			string go = piece.gameObject != null ? piece.gameObject.name ?? string.Empty : string.Empty;
			string desc = piece.m_description ?? string.Empty;
			return name + "|" + go + "|" + desc;
		}

		private static bool IsHammerLike(string described)
		{
			if (string.IsNullOrEmpty(described) || described == "empty")
			{
				return false;
			}

			return described.IndexOf("hammer", System.StringComparison.OrdinalIgnoreCase) >= 0;
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

		internal static PieceTable GetBuildPieceTable(Player player)
		{
			if ((Object)(object)player == (Object)null)
			{
				return null;
			}

			try
			{
				return Traverse.Create(player).Field("m_buildPieces").GetValue<PieceTable>();
			}
			catch
			{
				return null;
			}
		}

		internal static bool TryActivateFromPiece(Piece piece, string via)
		{
			if (!DismantleheimPlugin.IsModEnabled() || !DismantleheimPlugin.PatchesReady)
			{
				return false;
			}

			DismantleSession session = DismantleheimPlugin.Session;
			if (session == null)
			{
				return false;
			}

			string label = DescribeSelectedPiece(piece);
			if (!IhToolNames.TryMatch(label, out OperationMode mode)
			    && !IhToolNames.TryMatch(piece != null ? piece.m_name : null, out mode))
			{
				return false;
			}

			session.SetMode(mode, via);
			return true;
		}

		internal static void HandleBuildUiPiece(Piece piece, string via)
		{
			string label = DescribeSelectedPiece(piece);
			string name = piece != null ? piece.m_name : null;
			bool match = IhToolNames.TryMatch(label, out _)
			             || IhToolNames.TryMatch(name, out _);
			bool commit = via == "buildui-on-select-piece" || via == "buildui-piece-click";

			if (match)
			{
				TryActivateFromPiece(piece, via);
				return;
			}

			if (!commit || string.IsNullOrEmpty(name))
			{
				return;
			}

			DismantleSession session = DismantleheimPlugin.Session;
			if (session == null || !session.IsActive)
			{
				return;
			}

			session.Deactivate("other-tool-selected");
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

	[HarmonyPatch(typeof(PieceTable), nameof(PieceTable.SetSelected))]
	internal static class PieceTableSetSelectedPatch
	{
		private static void Postfix(PieceTable __instance)
		{
			Player player = Player.m_localPlayer;
			if ((Object)(object)player == (Object)null
			    || (Object)(object)ToolIdentityTracker.GetBuildPieceTable(player) != (Object)(object)__instance)
			{
				return;
			}

			Piece piece = null;
			try
			{
				piece = __instance.GetPiece(__instance.GetSelectedCategory(), __instance.GetSelectedIndex());
				if ((Object)(object)piece == (Object)null)
				{
					piece = __instance.GetSelectedPiece();
				}
			}
			catch
			{
				return;
			}

			ToolIdentityTracker.TryActivateFromPiece(piece, "piece-table-set-selected");
		}
	}

	/// <summary>
	/// Modern Valheim BuildUi: Infinity Hammer DeepNorth SelectTool runs Instant commands here
	/// and returns false — Player.SetSelectedPiece never runs.
	/// </summary>
	[HarmonyPatch(typeof(BuildUi), nameof(BuildUi.OnSelectPiece))]
	[HarmonyPriority(Priority.First)]
	internal static class BuildUiOnSelectPiecePatch
	{
		private static void Prefix(Piece __0)
		{
			ToolIdentityTracker.HandleBuildUiPiece(__0, "buildui-on-select-piece");
		}
	}

	[HarmonyPatch(typeof(BuildUi), nameof(BuildUi.OnNavigatedToPieceButton))]
	[HarmonyPriority(Priority.First)]
	internal static class BuildUiOnNavigatedPatch
	{
		private static void Postfix(BuildUiPieceButton __0)
		{
			Piece piece = __0 != null ? __0.Piece : null;
			ToolIdentityTracker.HandleBuildUiPiece(piece, "buildui-on-navigated");
		}
	}

	[HarmonyPatch(typeof(BuildUiPieceButton), "OnClickButton")]
	[HarmonyPriority(Priority.First)]
	internal static class BuildUiPieceButtonClickPatch
	{
		private static void Prefix(BuildUiPieceButton __instance)
		{
			Piece piece = __instance != null ? __instance.Piece : null;
			ToolIdentityTracker.HandleBuildUiPiece(piece, "buildui-piece-click");
		}
	}

	[HarmonyPatch(typeof(Terminal), nameof(Terminal.TryRunCommand), typeof(string), typeof(bool), typeof(bool))]
	internal static class TerminalIhToolCommandPatch
	{
		private static void Prefix(string text)
		{
			if (string.IsNullOrEmpty(text)
			    || !text.StartsWith("tool ", System.StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			string toolName = text.Substring(5).Trim();
			if (!IhToolNames.TryMatch(toolName, out OperationMode mode))
			{
				return;
			}

			DismantleSession session = DismantleheimPlugin.Session;
			if (session != null && DismantleheimPlugin.IsModEnabled() && DismantleheimPlugin.PatchesReady)
			{
				session.SetMode(mode, "ih-tool-command");
			}
		}
	}

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
