using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;

namespace Dismantleheim.Integration
{
	internal static class InfinityHammerDetector
	{
		// Common Guids used by JereKuusela Infinity Hammer releases.
		private static readonly string[] CandidateGuids =
		{
			"com.github.jerekuusela.InfinityHammer",
			"JereKuusela.InfinityHammer",
			"infinity_hammer"
		};

		public static bool IsInstalled(out string guid, out string version)
		{
			guid = null;
			version = null;
			try
			{
				foreach (var kv in Chainloader.PluginInfos)
				{
					PluginInfo info = kv.Value;
					if (info == null || info.Metadata == null)
					{
						continue;
					}

					string id = info.Metadata.GUID ?? string.Empty;
					string name = info.Metadata.Name ?? string.Empty;
					if (IsInfinityHammer(id, name))
					{
						guid = id;
						version = info.Metadata.Version != null ? info.Metadata.Version.ToString() : "";
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				DismantleheimPlugin.ModLogger?.LogWarning("InfinityHammer detect failed: " + ex.Message);
			}

			return false;
		}

		private static bool IsInfinityHammer(string guid, string name)
		{
			if (string.IsNullOrEmpty(guid) && string.IsNullOrEmpty(name))
			{
				return false;
			}

			foreach (string c in CandidateGuids)
			{
				if (string.Equals(guid, c, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			// Exact product name only — avoid loose substring matches on unrelated mods.
			return string.Equals(name, "Infinity Hammer", StringComparison.OrdinalIgnoreCase);
		}

		public static string FindConfigDirectory()
		{
			try
			{
				return Paths.ConfigPath;
			}
			catch
			{
				return null;
			}
		}
	}
}
