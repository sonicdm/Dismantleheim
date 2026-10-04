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

				bool preferTools = Directory.Exists(Path.Combine(configDir, "tools"));
				YamlToolInstaller.WriteOwnedFile(configDir, preferTools);
				string dest = preferTools
					? Path.Combine(configDir, "tools", YamlToolInstaller.OwnedFileName)
					: Path.Combine(configDir, YamlToolInstaller.OwnedFileName);
				log?.LogInfo("Dismantleheim: wrote owned IH tools file " + dest);
			}
			catch (Exception ex)
			{
				log?.LogWarning("Dismantleheim: Infinity Hammer YAML install failed: " + ex.Message);
			}
		}
	}
}
