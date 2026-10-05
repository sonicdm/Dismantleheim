using System;
using System.IO;
using BepInEx.Logging;
using Dismantleheim.Core;
using Dismantleheim.Integration;

namespace Dismantleheim.Install
{
	internal static class YamlToolInstallerBridge
	{
		/// <returns>True when install ran without throwing (file may already be current).</returns>
		public static bool TryInstall(ManualLogSource log, out string destPath, out bool wroteBytes)
		{
			destPath = null;
			wroteBytes = false;
			try
			{
				string configDir = InfinityHammerDetector.FindConfigDirectory();
				if (string.IsNullOrEmpty(configDir) || !Directory.Exists(configDir))
				{
					log?.LogWarning("Dismantleheim: BepInEx config path missing; skipped Infinity Hammer tool install.");
					return false;
				}

				// IH loads config/infinity_tools.yaml plus config/tools/infinity_tools*.yaml.
				// Root-level infinity_tools_dismantleheim.yaml is ignored — always use tools/.
				string toolsDir = Path.Combine(configDir, "tools");
				if (!Directory.Exists(toolsDir))
				{
					Directory.CreateDirectory(toolsDir);
				}

				wroteBytes = YamlToolInstaller.WriteOwnedFile(configDir, preferToolsSubfolder: true);
				destPath = Path.Combine(toolsDir, YamlToolInstaller.OwnedFileName);

				// Force rewrite if an old stock marker is still on disk (preserve logic can stick).
				if (File.Exists(destPath))
				{
					string onDisk = File.ReadAllText(destPath);
					if (onDisk.IndexOf(YamlToolInstaller.Marker, System.StringComparison.OrdinalIgnoreCase) < 0
					    && onDisk.IndexOf("dismantleheim-tool", System.StringComparison.OrdinalIgnoreCase) >= 0)
					{
						string stock = YamlToolInstaller.BuildOwnedDocument();
						File.WriteAllText(destPath, stock);
						wroteBytes = true;
						log?.LogInfo("Dismantleheim: forced IH tools YAML upgrade → " + YamlToolInstaller.Marker);
					}
				}
				return true;
			}
			catch (Exception ex)
			{
				log?.LogWarning("Dismantleheim: Infinity Hammer YAML install failed: " + ex.Message);
				return false;
			}
		}
	}
}
