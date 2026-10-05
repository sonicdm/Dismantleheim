using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Dismantleheim.Core;
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
	// Soft dep so Infinity Hammer Awake runs first when present (GUID from current IH releases).
	[BepInDependency("infinity_hammer", BepInDependency.DependencyFlags.SoftDependency)]
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

		internal static ConfigEntry<string> DefaultMode;

		internal static ConfigEntry<bool> ShowActiveMode;

		internal static ConfigEntry<bool> PreserveSelectionOnModeSwitch;

		internal static ConfigEntry<int> MaximumTargets;

		internal static ConfigEntry<float> DragSelectRadius;

		internal static ConfigEntry<bool> WarnWhenDeletingOccupiedContainers;

		internal static ConfigEntry<bool> RefuseUnknownDeleteTargets;

		internal static bool DebugEnabled => DebugLogging != null && DebugLogging.Value;

		internal static DismantleSession Session { get; private set; }

		/// <summary>False when Harmony PatchAll failed — activation stays closed.</summary>
		internal static bool PatchesReady { get; private set; }

		private Harmony _harmony;

		private ButtonConfig _activateButton;

		private bool _clientSystemsStarted;

		private bool _ihToolsInstallDone;

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

			RegisterActivateButton();
			if (ActivateKey != null)
			{
				ActivateKey.SettingChanged += OnActivateKeyChanged;
			}

			if (Enabled != null)
			{
				Enabled.SettingChanged += OnEnabledChanged;
			}

			// Soft-dep usually means IH is already in PluginInfos; Start only retries if Awake missed it.
			TryInstallInfinityHammerTools(forceRetry: false);

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

		private void Start()
		{
			if (GUIManager.IsHeadless())
			{
				return;
			}

			if (!_ihToolsInstallDone)
			{
				TryInstallInfinityHammerTools(forceRetry: true);
			}
		}

		private void TryInstallInfinityHammerTools(bool forceRetry)
		{
			if (InstallInfinityHammerTool == null || !InstallInfinityHammerTool.Value)
			{
				_ihToolsInstallDone = true;
				return;
			}

			if (_ihToolsInstallDone && !forceRetry)
			{
				return;
			}

			bool ihPresent = InfinityHammerDetector.IsInstalled(out string ihGuid, out string ihVersion);
			if (ihPresent)
			{
				Logger.LogInfo("Infinity Hammer detected (" + ihGuid + " " + ihVersion + ").");
			}
			else if (!forceRetry)
			{
				// Soft-dep miss / load order — retry once from Start after Chainloader finishes.
				return;
			}
			else
			{
				Logger.LogInfo(
					"Infinity Hammer not detected; writing owned tools YAML for next IH load. ActivateKey/console remain available.");
			}

			if (!YamlToolInstallerBridge.TryInstall(Logger, out string dest, out bool wrote))
			{
				return;
			}

			_ihToolsInstallDone = true;
			Logger.LogInfo(
				wrote
					? "Dismantleheim: wrote owned IH tools file " + dest
					: "Dismantleheim: owned IH tools file already current " + dest);
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
			DragBoxSelector.Cancel();
			// Jotunn has no RemoveButton; leave Buttons entry so ScriptEngine reload can reuse it.

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
			_activateButton = null;
			Logger.LogInfo(PluginName + " unloaded (ScriptEngine-safe).");
		}

		/// <summary>
		/// Jötunn stores buttons for process lifetime and warns on duplicate AddButton after ScriptEngine reload.
		/// Reuse the existing config when present; otherwise register once.
		/// </summary>
		private void RegisterActivateButton()
		{
			KeyCode key = ActivateKey != null ? ActivateKey.Value : KeyCode.Delete;
			string storedKey = ActivateButtonName + "!" + PluginGuid;

			try
			{
				FieldInfo buttonsField = AccessTools.Field(typeof(InputManager), "Buttons");
				var buttons = buttonsField?.GetValue(null) as Dictionary<string, ButtonConfig>;
				if (buttons != null && buttons.TryGetValue(storedKey, out ButtonConfig existing) && existing != null)
				{
					existing.Key = key;
					_activateButton = existing;
					Logger.LogInfo("Reused Jotunn activate button after reload.");
					return;
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning("Activate button reuse failed: " + ex.Message);
			}

			_activateButton = new ButtonConfig
			{
				Name = ActivateButtonName,
				Key = key,
				ActiveInGUI = false,
				ActiveInCustomGUI = false
			};
			InputManager.Instance.AddButton(PluginGuid, _activateButton);
		}

		/// <summary>
		/// ActivateKey edge. Jötunn registers as Name!guid; also accept short name and raw KeyCode
		/// so ScriptEngine reloads / missing ZInput defs still work.
		/// </summary>
		internal static bool WasActivatePressed()
		{
			ButtonConfig btn = null;
			try
			{
				FieldInfo buttonsField = AccessTools.Field(typeof(InputManager), "Buttons");
				var buttons = buttonsField?.GetValue(null) as Dictionary<string, ButtonConfig>;
				if (buttons != null)
				{
					buttons.TryGetValue(ActivateButtonName + "!" + PluginGuid, out btn);
				}
			}
			catch
			{
				// ignored
			}

			if (btn != null && ZInput.GetButtonDown(btn.Name))
			{
				return true;
			}

			if (ZInput.GetButtonDown(ActivateButtonName))
			{
				return true;
			}

			KeyCode key = ActivateKey != null ? ActivateKey.Value : KeyCode.Delete;
			return UnityEngine.Input.GetKeyDown(key);
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
				true,
				"Write diagnostic lines to BepInEx LogOutput.log. Default on while debugging selection.");

			DefaultMode = Config.Bind(
				"Modes",
				"DefaultMode",
				"MassDismantle",
				"Mode entered by ActivateKey / activate command: MassDismantle or MassDelete.");
			ShowActiveMode = Config.Bind("Modes", "ShowActiveMode", true, "Show MASS DISMANTLE / MASS DELETE banner near the cursor.");
			PreserveSelectionOnModeSwitch = Config.Bind(
				"Modes",
				"PreserveSelectionOnModeSwitch",
				true,
				"Keep the queue when switching between Mass Dismantle and Mass Delete (revalidated; never executes).");

			ActivateKey = Config.Bind(
				"Input",
				"ActivateKey",
				KeyCode.Delete,
				"Toggle DefaultMode session on/off. Default: Delete. Does not switch modes.");

			ConfirmHoldSeconds = Config.Bind(
				"Confirm",
				"HoldSeconds",
				1.0f,
				new ConfigDescription(
					"Seconds to hold Mouse3 to confirm.",
					new AcceptableValueRange<float>(0.2f, 5f)));
			ShowRing = Config.Bind("Confirm", "ShowRing", true, "Draw the confirmation progress ring around the cursor.");
			ShowSelectedCount = Config.Bind("Confirm", "ShowSelectedCount", true, "Show selected object count near the reticle.");

			AllowEnvironmentWithFilter = Config.Bind(
				"Selection",
				"AllowEnvironmentWithFilter",
				true,
				"Mass Delete only: when a prefab filter is sampled, allow matching rocks/trees into the queue.");
			DragSelectRadius = Config.Bind(
				"Selection",
				"DragSelectRadius",
				40f,
				new ConfigDescription(
					"Unused legacy key (box-select removed). Mass-select is Satisfactory-style Ctrl + aim paint.",
					new AcceptableValueRange<float>(5f, 120f)));
			ClearQueueOnToolSwitch = Config.Bind(
				"Selection",
				"ClearQueueOnToolSwitch",
				true,
				"Clear the pending selection when leaving Dismantleheim mode.");
			MaximumTargets = Config.Bind(
				"Selection",
				"MaximumTargets",
				50,
				new ConfigDescription("Maximum queued targets.", new AcceptableValueRange<int>(1, 500)));
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

			WarnWhenDeletingOccupiedContainers = Config.Bind(
				"Safety",
				"WarnWhenDeletingOccupiedContainers",
				true,
				"Mass Delete: show contents-loss warning when queued containers hold items.");
			RefuseUnknownDeleteTargets = Config.Bind(
				"Safety",
				"RefuseUnknownDeleteTargets",
				true,
				"Mass Delete: skip unsupported/unknown targets instead of forcing network destroy.");

			InstallInfinityHammerTool = Config.Bind(
				"Integration",
				"InstallInfinityHammerTool",
				true,
				"Write owned infinity_tools_dismantleheim.yaml (two Tools entries) when Infinity Hammer is installed.");
			PreferInfinityHammerOnly = Config.Bind(
				"Integration",
				"PreferInfinityHammerOnly",
				true,
				"Architecture: never spawn an extra inventory Hammer; Tools YAML + ActivateKey/console only.");

			DryRunOnly = Config.Bind(
				"Removal",
				"DryRunOnly",
				false,
				"When true, confirmation only logs the plan — no world deletes. Default false (live remove). Enable for safe testing.");
		}

		internal static OperationMode GetDefaultMode()
		{
			if (DefaultMode != null && OperationModeUtil.TryParse(DefaultMode.Value, out OperationMode mode))
			{
				return mode;
			}

			return OperationMode.MassDismantle;
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

			DragBoxSelector.DrawGui();
			ConfirmationRing.DrawGui(Session);
		}

		internal static float GetDragSelectRadius()
		{
			if (DragSelectRadius == null)
			{
				return 40f;
			}

			return Mathf.Clamp(DragSelectRadius.Value, 5f, 120f);
		}

		internal static bool IsModEnabled()
		{
			return Enabled == null || Enabled.Value;
		}

		internal static void DebugLog(string message)
		{
			if (DebugEnabled && ModLogger != null)
			{
				// Use Info — BepInEx often filters LogDebug out of LogOutput.log.
				ModLogger.LogInfo("Dismantleheim debug: " + message);
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
				"Dismantleheim: mode dismantle|delete | activate|deactivate|status|clear|why",
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
						case "mode":
							string modeArg = args.Length > 2 ? args[2] : string.Empty;
							if (!OperationModeUtil.TryParse(modeArg, out OperationMode parsed))
							{
								PrintCmd("Usage: dismantleheim mode dismantle|delete");
								break;
							}

							session.SetMode(parsed, "command");
							PrintCmd("Dismantleheim mode=" + OperationModeUtil.ShortName(session.ActiveMode)
							         + " active=" + session.IsActive);
							break;
						case "activate":
							if (session.TryActivate(GetDefaultMode(), "command", out string reject))
							{
								PrintCmd("Dismantleheim activated mode=" + OperationModeUtil.ShortName(session.ActiveMode));
							}
							else
							{
								PrintCmd("Dismantleheim activate rejected: " + (reject ?? "unknown")
								         + " (equip Hammer / enter place mode).");
							}

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
								+ " mode=" + OperationModeUtil.ShortName(session.ActiveMode)
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
