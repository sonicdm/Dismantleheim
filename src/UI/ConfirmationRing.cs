using Dismantleheim.Core;
using Dismantleheim.Selection;
using UnityEngine;

namespace Dismantleheim.UI
{
	internal static class ConfirmationRing
	{
		private static GUIStyle _bannerStyle;
		private static GUIStyle _subStyle;
		private static Texture2D _panelTex;

		public static void Tick(DismantleSession session)
		{
		}

		public static void DrawGui(DismantleSession session)
		{
			if (session == null || !session.IsActive)
			{
				return;
			}

			EnsureStyles();

			bool showMode = DismantleheimPlugin.ShowActiveMode == null || DismantleheimPlugin.ShowActiveMode.Value;
			bool showCount = DismantleheimPlugin.ShowSelectedCount == null || DismantleheimPlugin.ShowSelectedCount.Value;

			// Fixed top-center HUD so the mode is visible even while the IH/build piece table
			// is still open after selecting the tool (cursor-follow text sits under that panel).
			if (showMode || showCount)
			{
				string banner = showMode ? OperationModeUtil.Banner(session.ActiveMode) : string.Empty;
				string sub = string.Empty;
				if (showCount)
				{
					sub = session.Queue.Count + " selected";
					if (session.Sampler.HasFilter)
					{
						sub += "  [" + session.Sampler.ActiveFilter + "]";
					}

					bool dry = DismantleheimPlugin.DryRunOnly != null && DismantleheimPlugin.DryRunOnly.Value;
					if (dry)
					{
						sub += "  (dry-run)";
					}
				}

				float width = 520f;
				float height = showMode && showCount ? 54f : 32f;
				if (session.HasContentsLossWarning)
				{
					height += 22f;
				}

				Rect panel = new Rect((Screen.width - width) * 0.5f, 18f, width, height);
				Color prev = GUI.color;
				GUI.color = new Color(0f, 0f, 0f, 0.72f);
				GUI.DrawTexture(panel, _panelTex);
				GUI.color = prev;

				float y = panel.y + 6f;
				if (showMode)
				{
					_bannerStyle.normal.textColor = session.ActiveMode == OperationMode.MassDelete
						? new Color(1f, 0.45f, 0.35f, 1f)
						: new Color(0.85f, 0.95f, 0.55f, 1f);
					GUI.Label(new Rect(panel.x, y, panel.width, 26f), banner, _bannerStyle);
					y += 24f;
				}

				if (showCount)
				{
					_subStyle.normal.textColor = new Color(1f, 0.95f, 0.4f, 1f);
					GUI.Label(new Rect(panel.x, y, panel.width, 22f), sub, _subStyle);
					y += 20f;
				}

				if (session.HasContentsLossWarning)
				{
					_subStyle.normal.textColor = new Color(1f, 0.35f, 0.2f, 1f);
					GUI.Label(
						new Rect(panel.x, y, panel.width, 22f),
						"WARNING: queued containers hold items",
						_subStyle);
				}
			}

			bool showRing = DismantleheimPlugin.ShowRing == null || DismantleheimPlugin.ShowRing.Value;
			if (!showRing)
			{
				return;
			}

			if (!session.ConfirmHold.IsHolding && session.ConfirmHold.Progress <= 0f)
			{
				return;
			}

			float progress = session.ConfirmHold.Progress;
			Vector2 center = UnityEngine.Input.mousePosition;
			center.y = Screen.height - center.y;
			DrawArc(center, 22f, progress, session.ActiveMode);
		}

		private static void EnsureStyles()
		{
			if (_panelTex == null)
			{
				_panelTex = Texture2D.whiteTexture;
			}

			if (_bannerStyle == null)
			{
				_bannerStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.UpperCenter,
					fontSize = 18,
					fontStyle = FontStyle.Bold,
					wordWrap = false
				};
			}

			if (_subStyle == null)
			{
				_subStyle = new GUIStyle(GUI.skin.label)
				{
					alignment = TextAnchor.UpperCenter,
					fontSize = 14,
					fontStyle = FontStyle.Bold,
					wordWrap = false
				};
			}
		}

		private static void DrawArc(Vector2 center, float radius, float progress, OperationMode mode)
		{
			progress = Mathf.Clamp01(progress);
			const int segments = 48;
			Texture2D tex = Texture2D.whiteTexture;
			Color track = new Color(0f, 0f, 0f, 0.55f);
			Color fill = mode == OperationMode.MassDelete
				? new Color(1f, 0.4f, 0.25f, 0.95f)
				: new Color(1f, 0.75f, 0.2f, 0.95f);

			for (int i = 0; i < segments; i++)
			{
				float a0 = (i / (float)segments) * Mathf.PI * 2f - Mathf.PI * 0.5f;
				float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f - Mathf.PI * 0.5f;
				DrawSegment(center, radius, a0, a1, track, tex);
			}

			int filled = Mathf.CeilToInt(segments * progress);
			for (int i = 0; i < filled; i++)
			{
				float a0 = (i / (float)segments) * Mathf.PI * 2f - Mathf.PI * 0.5f;
				float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f - Mathf.PI * 0.5f;
				DrawSegment(center, radius, a0, a1, fill, tex);
			}
		}

		private static void DrawSegment(Vector2 center, float radius, float a0, float a1, Color color, Texture2D tex)
		{
			Vector2 p0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
			Vector2 p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
			DrawLine(p0, p1, color, tex, 3f);
		}

		private static void DrawLine(Vector2 a, Vector2 b, Color color, Texture2D tex, float width)
		{
			Vector2 d = b - a;
			float len = d.magnitude;
			if (len < 0.01f)
			{
				return;
			}

			float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
			GUI.color = color;
			Matrix4x4 matrix = GUI.matrix;
			GUIUtility.RotateAroundPivot(angle, a);
			GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, len, width), tex);
			GUI.matrix = matrix;
			GUI.color = Color.white;
		}
	}
}
