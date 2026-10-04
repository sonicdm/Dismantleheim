using Dismantleheim.Selection;
using UnityEngine;

namespace Dismantleheim.Integration
{
	/// <summary>
	/// Tracks tool ownership; exits when the local player disappears.
	/// Piece-menu exit is handled by right-click / ActivateKey / deactivate command (H05).
	/// </summary>
	internal static class ToolIdentityTracker
	{
		internal static void Tick(DismantleSession session)
		{
			if (session == null || !session.IsActive)
			{
				return;
			}

			Player player = Player.m_localPlayer;
			if ((Object)(object)player == (Object)null)
			{
				session.Deactivate("no-player");
			}
		}
	}
}
