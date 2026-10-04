using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Dismantleheim.Core
{
	/// <summary>
	/// Writes an owned Infinity Hammer tools file (equipment-keyed dictionary schema).
	/// Preserves user-edited owned documents. Migrates between config root and tools/.
	/// Shared default cleanup only removes marker-owned / activate-command entries.
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
		/// Decide next owned-document bytes. Preserves user-edited copies that still contain our tool.
		/// Returns whether the on-disk content should change.
		/// </summary>
		public static bool WriteOwnedDocument(string existingOwnedYaml, out string nextYaml)
		{
			string existing = existingOwnedYaml ?? string.Empty;
			if (ShouldPreserveOwnedDocument(existing))
			{
				nextYaml = NormalizeNewlines(existing).TrimEnd() + Environment.NewLine;
				return false;
			}

			string stock = NormalizeNewlines(BuildOwnedDocument()).TrimEnd() + "\n";
			string prior = NormalizeNewlines(existing).TrimEnd() + "\n";
			nextYaml = stock.Replace("\n", Environment.NewLine);
			return !string.Equals(prior, stock, StringComparison.Ordinal);
		}

		public static bool ShouldPreserveOwnedDocument(string yaml)
		{
			if (string.IsNullOrWhiteSpace(yaml))
			{
				return false;
			}

			if (CountNamedToolsInDocument(yaml, ToolName) == 0)
			{
				return false;
			}

			string stock = NormalizeNewlines(BuildOwnedDocument()).TrimEnd();
			string existing = NormalizeNewlines(yaml).TrimEnd();
			return !string.Equals(stock, existing, StringComparison.Ordinal);
		}

		public static void WriteOwnedFile(string configRoot, bool preferToolsSubfolder)
		{
			if (string.IsNullOrEmpty(configRoot))
			{
				throw new ArgumentException("configRoot required", nameof(configRoot));
			}

			string toolsDir = Path.Combine(configRoot, "tools");
			bool useTools = preferToolsSubfolder && Directory.Exists(toolsDir);
			string preferred = Path.Combine(useTools ? toolsDir : configRoot, OwnedFileName);
			string alternate = Path.Combine(useTools ? configRoot : toolsDir, OwnedFileName);

			string existing = string.Empty;
			if (File.Exists(preferred))
			{
				existing = File.ReadAllText(preferred, Encoding.UTF8);
			}
			else if (File.Exists(alternate))
			{
				existing = File.ReadAllText(alternate, Encoding.UTF8);
			}

			bool changed = WriteOwnedDocument(existing, out string next);
			if (changed || !File.Exists(preferred))
			{
				AtomicWrite(preferred, next);
			}

			if (File.Exists(alternate)
			    && !string.Equals(Path.GetFullPath(alternate), Path.GetFullPath(preferred), StringComparison.OrdinalIgnoreCase))
			{
				File.Delete(alternate);
			}

			// Always attempt shared stale cleanup (even when preferred file was unchanged).
			TryRemoveStaleFromSharedDefault(configRoot);
		}

		public static int CountNamedToolsInDocument(string yaml, string toolName)
		{
			if (string.IsNullOrEmpty(yaml))
			{
				return 0;
			}

			int count = 0;
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

			return Regex.IsMatch(
				NormalizeNewlines(yaml),
				@"(?m)^[A-Za-z][A-Za-z0-9_]*:\s*$",
				RegexOptions.None);
		}

		/// <summary>
		/// Remove Dismantleheim list items that carry our activate command or sit under our ownership marker.
		/// Does not remove a same-named tool that lacks our command/marker (user-authored).
		/// </summary>
		public static string RemoveNamedToolFromShared(string yaml, string toolName)
		{
			if (string.IsNullOrEmpty(yaml))
			{
				return string.Empty;
			}

			string[] lines = NormalizeNewlines(yaml).Split('\n');
			var output = new List<string>();
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
					bool owned = BlockLooksOwned(lines, i);
					if (!owned)
					{
						output.Add(line);
						continue;
					}

					skipping = true;
					if (output.Count > 0
					    && output[output.Count - 1].IndexOf("dismantleheim-tool", StringComparison.OrdinalIgnoreCase) >= 0)
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

		private static bool BlockLooksOwned(string[] lines, int nameLineIndex)
		{
			// Preceding marker comment.
			for (int j = nameLineIndex - 1; j >= 0 && j >= nameLineIndex - 3; j--)
			{
				if (lines[j].IndexOf("dismantleheim-tool", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			// Body of this list item until next sibling / equipment key.
			var anyItem = new Regex(@"^\s*-\s*name:\s*\S");
			var equipmentKey = new Regex(@"^[A-Za-z][A-Za-z0-9_]*:\s*$");
			for (int j = nameLineIndex + 1; j < lines.Length; j++)
			{
				string line = lines[j].TrimEnd('\r');
				if (anyItem.IsMatch(line) || equipmentKey.IsMatch(line))
				{
					break;
				}

				if (line.IndexOf("command:", StringComparison.OrdinalIgnoreCase) >= 0
				    && line.IndexOf(ActivateCommand, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			return false;
		}

		private static void TryRemoveStaleFromSharedDefault(string configRoot)
		{
			foreach (string shared in new[]
			         {
				         Path.Combine(configRoot, "infinity_tools.yaml"),
				         Path.Combine(configRoot, "tools", "infinity_tools.yaml")
			         })
			{
				if (!File.Exists(shared))
				{
					continue;
				}

				string text = File.ReadAllText(shared, Encoding.UTF8);
				if (CountNamedToolsInDocument(text, ToolName) == 0)
				{
					continue;
				}

				string cleaned = RemoveNamedToolFromShared(text, ToolName);
				if (!string.Equals(NormalizeNewlines(text), NormalizeNewlines(cleaned), StringComparison.Ordinal))
				{
					AtomicWrite(shared, cleaned);
				}
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
