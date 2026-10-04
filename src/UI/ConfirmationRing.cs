using Dismantleheim.Selection;
using UnityEngine;

namespace Dismantleheim.UI
{
	internal static class ConfirmationRing
	{
		public static void Tick(DismantleSession session)
		{
		}

		public static void DrawGui(DismantleSession session)
		{
			if (session == null || !session.IsActive)
			{
				return;
			}

			if (DismantleheimPlugin.ShowSelectedCount != null && DismantleheimPlugin.ShowSelectedCount.Value)
			{
				Vector2 mouse = UnityEngine.Input.mousePosition;
				float y = Screen.height - mouse.y;
				string label = "Dismantleheim: " + session.Queue.Count;
				if (session.Sampler.HasFilter)
				{
					label += " [" + session.Sampler.ActiveFilter + "]";
				}

				GUI.color = Color.white;
				GUI.Label(new Rect(mouse.x + 18f, y + 18f, 320f, 24f), label);
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
			DrawArc(center, 22f, progress);
		}

		private static void DrawArc(Vector2 center, float radius, float progress)
		{
			progress = Mathf.Clamp01(progress);
			const int segments = 48;
			Texture2D tex = Texture2D.whiteTexture;
			Color track = new Color(0f, 0f, 0f, 0.55f);
			Color fill = new Color(1f, 0.75f, 0.2f, 0.95f);

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
