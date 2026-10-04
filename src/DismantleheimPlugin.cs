using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Dismantleheim.Install;
using Dismantleheim.Integration;
using Dismantleheim.Input;
using Dismantleheim.Selection;
using Dismantleheim.UI;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Dismantleheim
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	[BepInDependency(Jotunn.Main.ModGuid)]
	[NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
	public sealed class DismantleheimPlugin : BaseUnityPlugin
	{
		public const string PluginGuid = "com.sonicdm.valheim.dismantleheim";

		public const string PluginName = "Dismantleheim";

		public const string PluginVersion = "0.1.0";

		internal const string ActivateButtonName = "Dismantleheim_Activate";

		internal static ManualLogSource ModLogger;

		internal static ConfigEntry<bool> Enabled;

		internal static ConfigEntry<bool> DebugLogging;

		internal static ConfigEntry<KeyCode> ActivateKey;

		internal static ConfigEntry<float> ConfirmHoldSeconds;

		internal static ConfigEntry<bool> ShowRing;

		internal static ConfigEntry<bool> ShowSelectedCount;

		internal static ConfigEntry<bool> AllowEnvironmentWithFilter;

		internal static ConfigEntry<bool> ClearQueueOnToolSwitch;

		internal static ConfigEntry<string> ExtraDenyPrefabs;

		internal static ConfigEntry<string> ExtraAllowPrefabs;

		internal static ConfigEntry<bool> InstallInfinityHammerTool;

		internal static ConfigEntry<bool> PreferInfinityHammerOnly;

		internal static ConfigEntry<bool> DryRunOnly;

		internal static bool DebugEnabled => DebugLogging != null && DebugLogging.Value;

		internal static DismantleSession Session { get; private set; }

		/// <summary>False when Harmony PatchAll failed — activation stays closed.</summary>
		internal static bool PatchesReady { get; private set; }

		private Harmony _harmony;

		private ButtonConfig _activateButton;

		private bool _clientSystemsStarted;

		private void Awake()
		{
			ModLogger = Logger;
			BindConfig();
			Session = new DismantleSession();

			RegisterCommands();

			if (GUIManager.IsHeadless())
			{
				Logger.LogInfo(PluginName + " " + PluginVersion + " skipped client systems on headless server.");
				return;
			}

			_activateButton = new ButtonConfig
			{
				Name = ActivateButtonName,
				Key = ActivateKey != null ? ActivateKey.Value : KeyCode.Delete,
				ActiveInGUI = false,
				ActiveInCustomGUI = false
			};
			InputManager.Instance.AddButton(PluginGuid, _activateButton);
			if (ActivateKey != null)
			{
				ActivateKey.SettingChanged += OnActivateKeyChanged;
			}

			if (Enabled != null)
			{
				Enabled.SettingChanged += OnEnabledChanged;
			}

			bool ihPresent = InfinityHammerDetector.IsInstalled(out string ihGuid, out string ihVersion);
			if (ihPresent)
			{
				Logger.LogInfo("Infinity Hammer detected (" + ihGuid + " " + ihVersion + ").");
				if (InstallInfinityHammerTool != null && InstallInfinityHammerTool.Value)
				{
					YamlToolInstallerBridge.TryInstall(Logger);
				}
			}
			else
			{
				Logger.LogInfo(
					"Infinity Hammer not detected. Tools-menu entry unavailable; use ActivateKey (default Delete) or 'dismantleheim activate'.");
			}

			_harmony = new Harmony(PluginGuid);
			try
			{
				_harmony.PatchAll(typeof(DismantleheimPlugin).Assembly);
				PatchesReady = true;
				_clientSystemsStarted = true;
				Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
			}
			catch (Exception ex)
			{
				PatchesReady = false;
				Logger.LogError(PluginName + " failed to patch (fail-closed): " + ex);
				try
				{
					_harmony.UnpatchSelf();
				}
				catch
				{
					// ignored
				}
			}
		}

		private void OnDestroy()
		{
			if (ActivateKey != null)
			{
				ActivateKey.SettingChanged -= OnActivateKeyChanged;
			}

			if (Enabled != null)
			{
				Enabled.SettingChanged -= OnEnabledChanged;
			}

			if (Session != null && Session.IsActive)
			{
				Session.ResetHard("unload");
			}

			HoverHighlighter.ClearAll();
			ContextualInputRouter.ResetLatches();

			if (_harmony != null)
			{
				try
				{
					_harmony.UnpatchSelf();
				}
				catch (Exception ex)
				{
					Logger.LogWarning("Unpatch failed: " + ex.Message);
				}

				_harmony = null;
			}

			PatchesReady = false;
		}

		private void OnActivateKeyChanged(object sender, EventArgs e)
		{
			if (_activateButton != null && ActivateKey != null)
			{
				_activateButton.Key = ActivateKey.Value;
			}
		}

		private void OnEnabledChanged(object sender, EventArgs e)
		{
			if (!IsModEnabled() && Session != null && Session.IsActive)
			{
				Session.ResetHard("disabled");
			}
		}

		private void BindConfig()
		{
			Enabled = Config.Bind("General", "Enabled", true, "Enable Dismantleheim.");
			DebugLogging = Config.Bind(
				"General",
				"DebugLogging",
				false,
				"Write diagnostic lines to BepInEx LogOutput.log.");

			ActivateKey = Config.Bind(
				"Input",
				"ActivateKey",
				KeyCode.Delete,
				"Toggle Dismantleheim mode on/off. Default: Delete. Live-rebinding updates the Jötunn button.");

			ConfirmHoldSeconds = Config.Bind(
				"Confirm",
				"HoldSeconds",
				1.0f,
				new ConfigDescription(
					"Seconds to hold Mouse3 to confirm dismantle.",
					new AcceptableValueRange<float>(0.2f, 5f)));
			ShowRing = Config.Bind("Confirm", "ShowRing", true, "Draw the confirmation progress ring around the cursor.");
			ShowSelectedCount = Config.Bind("Confirm", "ShowSelectedCount", true, "Show selected object count near the reticle.");

			AllowEnvironmentWithFilter = Config.Bind(
				"Selection",
				"AllowEnvironmentWithFilter",
				true,
				"When a prefab filter is sampled, allow rocks/trees matching that prefab into the queue.");
			ClearQueueOnToolSwitch = Config.Bind(
				"Selection",
				"ClearQueueOnToolSwitch",
				true,
				"Clear the pending selection when leaving Dismantleheim mode.");
			ExtraDenyPrefabs = Config.Bind(
				"Selection",
				"ExtraDenyPrefabs",
				"",
				"Comma-separated prefab names never selectable.");
			ExtraAllowPrefabs = Config.Bind(
				"Selection",
				"ExtraAllowPrefabs",
				"",
				"Comma-separated prefab names that ignore ExtraDeny only. Does not bypass type/removability/environment rules.");

			InstallInfinityHammerTool = Config.Bind(
				"Integration",
				"InstallInfinityHammerTool",
				true,
				"Write owned infinity_tools_dismantleheim.yaml when Infinity Hammer is installed.");
			PreferInfinityHammerOnly = Config.Bind(
				"Integration",
				"PreferInfinityHammerOnly",
				true,
				"Architecture: never spawn an extra inventory Hammer; Tools YAML + ActivateKey/console only.");

			DryRunOnly = Config.Bind(
				"Removal",
				"DryRunOnly",
				true,
				"When true (v0.1 default), confirmation only logs the exact removal plan — no world deletes.");
		}

		private void Update()
		{
			if (!_clientSystemsStarted || !PatchesReady || !IsModEnabled() || Session == null)
			{
				return;
			}

			if (GUIManager.IsHeadless())
			{
				return;
			}

			ContextualInputRouter.Tick(Session);
			HoverHighlighter.Tick(Session);
			ConfirmationRing.Tick(Session);
		}

		private void OnGUI()
		{
			if (!_clientSystemsStarted || !PatchesReady || !IsModEnabled() || Session == null || !Session.IsActive)
			{
				return;
			}

			ConfirmationRing.DrawGui(Session);
		}

		internal static bool IsModEnabled()
		{
			return Enabled == null || Enabled.Value;
		}

		internal static void DebugLog(string message)
		{
			if (DebugEnabled && ModLogger != null)
			{
				ModLogger.LogDebug(message);
			}
		}

		internal static float GetHoldSeconds()
		{
			if (ConfirmHoldSeconds == null)
			{
				return 1f;
			}

			float v = ConfirmHoldSeconds.Value;
			if (v < 0.2f)
			{
				return 0.2f;
			}

			if (v > 5f)
			{
				return 5f;
			}

			return v;
		}

		private static bool _commandsRegistered;

		private void RegisterCommands()
		{
			if (_commandsRegistered)
			{
				return;
			}

			_commandsRegistered = true;
			new Terminal.ConsoleCommand(
				"dismantleheim",
				"Dismantleheim: activate|deactivate|status|clear|why",
				args =>
				{
					DismantleSession session = Session;
					if (session == null)
					{
						PrintCmd("Dismantleheim session unavailable.");
						return;
					}

					if (!PatchesReady && !GUIManager.IsHeadless())
					{
						PrintCmd("Dismantleheim patches failed; activation closed.");
						return;
					}

					if (!IsModEnabled())
					{
						PrintCmd("Dismantleheim disabled in config.");
						return;
					}

					string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
					switch (sub)
					{
						case "activate":
							session.Activate("command");
							PrintCmd("Dismantleheim activated.");
							break;
						case "deactivate":
						case "off":
							session.Deactivate("command");
							PrintCmd("Dismantleheim deactivated.");
							break;
						case "clear":
							session.Queue.Clear();
							session.SyncStateAfterSelectionChange();
							PrintCmd("Dismantleheim selection cleared.");
							break;
						case "why":
							PrintCmd(session.LastRejectReason ?? "(no recent reject)");
							break;
						default:
							PrintCmd(
								"state=" + session.StateMachine.State
								+ " queue=" + session.Queue.Count
								+ " filter=" + (session.Sampler.ActiveFilter ?? "(none)")
								+ " dryRun=" + (DryRunOnly != null && DryRunOnly.Value)
								+ " patches=" + PatchesReady);
							break;
					}
				});
		}

		private static void PrintCmd(string msg)
		{
			if (Console.instance != null)
			{
				Console.instance.Print(msg);
			}

			ModLogger?.LogInfo(msg);
		}
	}
}
