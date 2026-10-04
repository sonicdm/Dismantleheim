using System;
using System.IO;
using BepInEx.Logging;
using Dismantleheim.Core;
using Dismantleheim.Integration;

namespace Dismantleheim.Install
{
	internal static class YamlToolInstallerBridge
	{
		public static void TryInstall(ManualLogSource log)
		{
			try
			{
				string configDir = InfinityHammerDetector.FindConfigDirectory();
				if (string.IsNullOrEmpty(configDir) || !Directory.Exists(configDir))
				{
					log?.LogWarning("Dismantleheim: BepInEx config path missing; skipped Infinity Hammer tool install.");
					return;
				}

				string path = Path.Combine(configDir, "infinity_tools.yaml");
				YamlToolInstaller.UpsertToFile(path, createDirectory: false);
				log?.LogInfo("Dismantleheim: upserted tool entry in " + path);
			}
			catch (Exception ex)
			{
				log?.LogWarning("Dismantleheim: Infinity Hammer YAML install failed: " + ex.Message);
			}
		}
	}
}
