using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Dismantleheim.Core
{
	/// <summary>
	/// Writes an owned Infinity Hammer tools file (equipment-keyed dictionary schema).
	/// Never mutates the shared infinity_tools.yaml default file except optional stale cleanup.
	/// </summary>
	public static class YamlToolInstaller
	{
		public const string ToolName = "Dismantleheim";
		public const string Marker = "dismantleheim-tool-v2";
		public const string ActivateCommand = "dismantleheim activate";
		public const string EquipmentKey = "hammer";
		public const string OwnedFileName = "infinity_tools_dismantleheim.yaml";

		public static string BuildOwnedDocument()
		{
			var sb = new StringBuilder();
			sb.AppendLine("# " + Marker + " — owned by Dismantleheim; safe to delete on uninstall");
			sb.AppendLine(EquipmentKey + ":");
			sb.AppendLine("- name: " + ToolName);
			sb.AppendLine("  description: |-");
			sb.AppendLine("    Queued dismantle mode.");
			sb.AppendLine("    Left-click to flag pieces.");
			sb.AppendLine("    Shift+Mouse3 samples prefab filter.");
			sb.AppendLine("    Hold Mouse3 to confirm (dry-run by default).");
			sb.AppendLine("    ActivateKey (default Delete) also toggles.");
			sb.AppendLine("  icon: Hammer");
			sb.AppendLine("  instant: true");
			sb.AppendLine("  command: " + ActivateCommand);
			sb.AppendLine("  tabIndex: 0");
			return sb.ToString();
		}

		/// <summary>
		/// Idempotent write of the owned document. Returns whether file bytes changed.
		/// </summary>
		public static bool WriteOwnedDocument(string existingOwnedYaml, out string nextYaml)
		{
			nextYaml = BuildOwnedDocument();
			string existing = NormalizeNewlines(existingOwnedYaml ?? string.Empty).TrimEnd() + "\n";
			string next = NormalizeNewlines(nextYaml).TrimEnd() + "\n";
			nextYaml = next.Replace("\n", Environment.NewLine);
			return !string.Equals(existing, next, StringComparison.Ordinal);
		}

		public static void WriteOwnedFile(string configRoot, bool preferToolsSubfolder)
		{
			if (string.IsNullOrEmpty(configRoot))
			{
				throw new ArgumentException("configRoot required", nameof(configRoot));
			}

			string toolsDir = Path.Combine(configRoot, "tools");
			string targetDir = preferToolsSubfolder && Directory.Exists(toolsDir) ? toolsDir : configRoot;
			string path = Path.Combine(targetDir, OwnedFileName);

			string existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
			bool changed = WriteOwnedDocument(existing, out string next);
			if (!changed && File.Exists(path))
			{
				return;
			}

			AtomicWrite(path, next);

			// Cleanup stale entry we may have written into the shared default file in 0.1.0.
			TryRemoveStaleFromSharedDefault(configRoot);
		}

		public static int CountNamedToolsInDocument(string yaml, string toolName)
		{
			if (string.IsNullOrEmpty(yaml))
			{
				return 0;
			}

			int count = 0;
			// Quoted or unquoted name under a list item.
			var re = new Regex(
				@"^\s*-\s*name:\s*(?:""|')?" + Regex.Escape(toolName) + @"(?:""|')?\s*$",
				RegexOptions.Multiline | RegexOptions.IgnoreCase);
			foreach (Match m in re.Matches(yaml))
			{
				count++;
			}

			return count;
		}

		public static bool OwnedEntryHasActivateCommand(string yaml)
		{
			if (string.IsNullOrEmpty(yaml))
			{
				return false;
			}

			// Must be under our tool block, not an unrelated tool.
			string[] lines = NormalizeNewlines(yaml).Split('\n');
			bool inOurTool = false;
			var nameRe = new Regex(
				@"^\s*-\s*name:\s*(?:""|')?" + Regex.Escape(ToolName) + @"(?:""|')?\s*$",
				RegexOptions.IgnoreCase);
			var anyItem = new Regex(@"^\s*-\s*name:\s*\S");
			foreach (string raw in lines)
			{
				string line = raw.TrimEnd('\r');
				if (nameRe.IsMatch(line))
				{
					inOurTool = true;
					continue;
				}

				if (inOurTool && anyItem.IsMatch(line))
				{
					inOurTool = false;
				}

				if (inOurTool && line.IndexOf("command:", StringComparison.OrdinalIgnoreCase) >= 0
				    && line.IndexOf(ActivateCommand, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			return false;
		}

		public static bool LooksLikeEquipmentDictionary(string yaml)
		{
			if (string.IsNullOrEmpty(yaml))
			{
				return false;
			}

			// Top-level equipment key followed by a list (hammer: / hoe:).
			return Regex.IsMatch(
				NormalizeNewlines(yaml),
				@"(?m)^[A-Za-z][A-Za-z0-9_]*:\s*$",
				RegexOptions.None);
		}

		/// <summary>
		/// Remove Dismantleheim list items from a shared default file without rewriting other tools.
		/// </summary>
		public static string RemoveNamedToolFromShared(string yaml, string toolName)
		{
			if (string.IsNullOrEmpty(yaml))
			{
				return string.Empty;
			}

			string[] lines = NormalizeNewlines(yaml).Split('\n');
			var output = new System.Collections.Generic.List<string>();
			bool skipping = false;
			var nameRe = new Regex(
				@"^\s*-\s*name:\s*(?:""|')?" + Regex.Escape(toolName) + @"(?:""|')?\s*$",
				RegexOptions.IgnoreCase);
			var anyItem = new Regex(@"^\s*-\s*name:\s*\S");
			var equipmentKey = new Regex(@"^[A-Za-z][A-Za-z0-9_]*:\s*$");

			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].TrimEnd('\r');
				if (nameRe.IsMatch(line))
				{
					skipping = true;
					if (output.Count > 0 && output[output.Count - 1].IndexOf("dismantleheim-tool", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						output.RemoveAt(output.Count - 1);
					}

					continue;
				}

				if (skipping)
				{
					if (anyItem.IsMatch(line) || equipmentKey.IsMatch(line))
					{
						skipping = false;
					}
					else
					{
						continue;
					}
				}

				output.Add(line);
			}

			return string.Join(Environment.NewLine, output).TrimEnd() + Environment.NewLine;
		}

		private static void TryRemoveStaleFromSharedDefault(string configRoot)
		{
			string shared = Path.Combine(configRoot, "infinity_tools.yaml");
			if (!File.Exists(shared))
			{
				string toolsShared = Path.Combine(configRoot, "tools", "infinity_tools.yaml");
				if (File.Exists(toolsShared))
				{
					shared = toolsShared;
				}
				else
				{
					return;
				}
			}

			string text = File.ReadAllText(shared, Encoding.UTF8);
			if (CountNamedToolsInDocument(text, ToolName) == 0)
			{
				return;
			}

			string cleaned = RemoveNamedToolFromShared(text, ToolName);
			if (!string.Equals(NormalizeNewlines(text), NormalizeNewlines(cleaned), StringComparison.Ordinal))
			{
				AtomicWrite(shared, cleaned);
			}
		}

		private static void AtomicWrite(string path, string contents)
		{
			string dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			string temp = path + ".tmp";
			File.WriteAllText(temp, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			if (File.Exists(path))
			{
				File.Replace(temp, path, null);
			}
			else
			{
				File.Move(temp, path);
			}
		}

		private static string NormalizeNewlines(string s)
		{
			return (s ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
		}
	}
}
