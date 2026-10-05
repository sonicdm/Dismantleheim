using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Dismantleheim.Core
{
	/// <summary>
	/// Owned Infinity Hammer tools file with two mode entries (equipment-keyed dictionary).
	/// Preserves any non-stock edited owned document; conflict-safe root↔tools migration.
	/// </summary>
	public static class YamlToolInstaller
	{
		public const string ToolNameDismantle = "Dismantleheim - Mass Dismantle";
		public const string ToolNameDelete = "Dismantleheim - Mass Delete";
		public const string LegacyToolName = "Dismantleheim";
		public const string Marker = "dismantleheim-tool-v10";
		public const string ActivateCommandDismantle = "dismantleheim mode dismantle";
		public const string ActivateCommandDelete = "dismantleheim mode delete";
		public const string LegacyActivateCommand = "dismantleheim activate";
		public const string EquipmentKey = "hammer";
		public const string OwnedFileName = "infinity_tools_dismantleheim.yaml";
		public const string ConflictBackupSuffix = ".dismantleheim-conflict.bak";

		public const string ToolName = ToolNameDismantle;
		public const string ActivateCommand = ActivateCommandDismantle;

		public static string BuildOwnedDocument()
		{
			var sb = new StringBuilder();
			sb.AppendLine("# " + Marker + " — owned by Dismantleheim; safe to delete on uninstall");
			sb.AppendLine(EquipmentKey + ":");
			AppendTool(
				sb,
				ToolNameDismantle,
				"Select built pieces, then hold Mouse3 to reclaim materials (vanilla refunds).",
				ActivateCommandDismantle);
			AppendTool(
				sb,
				ToolNameDelete,
				"Select objects, then hold Mouse3 to erase without drops. Containers may destroy contents.",
				ActivateCommandDelete);
			return sb.ToString();
		}

		private static void AppendTool(StringBuilder sb, string name, string description, string command)
		{
			sb.AppendLine("- name: " + name);
			sb.AppendLine("  description: |-");
			sb.AppendLine("    " + description);
			sb.AppendLine("    Hold Ctrl + wave aim to mass-select. Ctrl+click unselects one. Shift+Mouse3 samples.");
			sb.AppendLine("    Hold Mouse3 to confirm.");
			sb.AppendLine("  icon: Hammer");
			sb.AppendLine("  instant: true");
			sb.AppendLine("  command: " + command);
			sb.AppendLine("  tabIndex: 0");
		}

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

		/// <summary>
		/// Preserve any existing non-empty document that is not current stock and not a known
		/// upgradeable generated stock (including renamed/disabled tool names).
		/// </summary>
		public static bool ShouldPreserveOwnedDocument(string yaml)
		{
			if (string.IsNullOrWhiteSpace(yaml))
			{
				return false;
			}

			string stock = NormalizeNewlines(BuildOwnedDocument()).TrimEnd();
			string existing = NormalizeNewlines(yaml).TrimEnd();
			if (string.Equals(stock, existing, StringComparison.Ordinal))
			{
				return false;
			}

			if (IsUpgradeableStockDocument(existing))
			{
				return false;
			}

			// Any other existing bytes (renamed tools, user YAML, comments) are preserved.
			return true;
		}

		/// <summary>
		/// True for Dismantleheim-owned stock (or legacy single-entry) without user icon edits.
		/// </summary>
		public static bool IsUpgradeableStockDocument(string yaml)
		{
			if (string.IsNullOrWhiteSpace(yaml))
			{
				return false;
			}

			if (yaml.IndexOf("dismantleheim-tool", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}

			if (Regex.IsMatch(NormalizeNewlines(yaml), @"(?m)^\s*icon:\s*(?!Hammer\s*$).+$"))
			{
				return false;
			}

			int dismantle = CountNamedToolsInDocument(yaml, ToolNameDismantle);
			int delete = CountNamedToolsInDocument(yaml, ToolNameDelete);
			bool hasLegacy = CountNamedToolsInDocument(yaml, LegacyToolName) > 0;
			bool hasBoth = dismantle == 1 && delete == 1;
			// Renamed/disabled docs (no recognized owned names) are not upgradeable stock.
			if (dismantle == 0 && delete == 0 && !hasLegacy)
			{
				return false;
			}

			// Prior stock marker → rewrite tooltips/commands to current owned document.
			if (yaml.IndexOf("dismantleheim-tool-", StringComparison.OrdinalIgnoreCase) >= 0
			    && yaml.IndexOf(Marker, StringComparison.OrdinalIgnoreCase) < 0)
			{
				return true;
			}

			return !hasBoth || hasLegacy;
		}

		/// <returns>True if preferred file bytes were written or an alternate was migrated/removed.</returns>
		public static bool WriteOwnedFile(string configRoot, bool preferToolsSubfolder)
		{
			if (string.IsNullOrEmpty(configRoot))
			{
				throw new ArgumentException("configRoot required", nameof(configRoot));
			}

			string toolsDir = Path.Combine(configRoot, "tools");
			bool useTools = preferToolsSubfolder && Directory.Exists(toolsDir);
			string preferred = Path.Combine(useTools ? toolsDir : configRoot, OwnedFileName);
			string alternate = Path.Combine(useTools ? configRoot : toolsDir, OwnedFileName);

			ResolveExistingOwnedContent(preferred, alternate, out string existing, out _);

			bool touched = false;
			WriteOwnedDocument(existing, out string next);
			string preferredNow = File.Exists(preferred) ? File.ReadAllText(preferred, Encoding.UTF8) : string.Empty;
			if (!File.Exists(preferred)
			    || !string.Equals(NormalizeNewlines(preferredNow), NormalizeNewlines(next), StringComparison.Ordinal))
			{
				AtomicWrite(preferred, next);
				touched = true;
			}

			if (File.Exists(alternate)
			    && !string.Equals(Path.GetFullPath(alternate), Path.GetFullPath(preferred), StringComparison.OrdinalIgnoreCase))
			{
				string altText = File.ReadAllText(alternate, Encoding.UTF8);
				string prefText = File.ReadAllText(preferred, Encoding.UTF8);
				if (!string.Equals(NormalizeNewlines(altText), NormalizeNewlines(prefText), StringComparison.Ordinal))
				{
					// Distinct alternate would be lost — park outside IH-scanned names.
					AtomicWrite(alternate + ConflictBackupSuffix, altText);
				}

				File.Delete(alternate);
				touched = true;
			}

			TryRemoveStaleFromSharedDefault(configRoot);
			return touched;
		}

		/// <summary>
		/// Choose content when preferred and/or alternate owned files exist.
		/// Distinct conflicting copies are written to ConflictBackupSuffix (not IH-scanned).
		/// </summary>
		public static void ResolveExistingOwnedContent(
			string preferredPath,
			string alternatePath,
			out string existing,
			out string conflictBackupPath)
		{
			existing = string.Empty;
			conflictBackupPath = null;

			bool hasPreferred = File.Exists(preferredPath);
			bool hasAlternate = File.Exists(alternatePath)
			                    && !string.Equals(
				                    Path.GetFullPath(preferredPath),
				                    Path.GetFullPath(alternatePath),
				                    StringComparison.OrdinalIgnoreCase);

			if (!hasPreferred && !hasAlternate)
			{
				return;
			}

			if (hasPreferred && !hasAlternate)
			{
				existing = File.ReadAllText(preferredPath, Encoding.UTF8);
				return;
			}

			if (!hasPreferred && hasAlternate)
			{
				existing = File.ReadAllText(alternatePath, Encoding.UTF8);
				return;
			}

			string preferredText = File.ReadAllText(preferredPath, Encoding.UTF8);
			string alternateText = File.ReadAllText(alternatePath, Encoding.UTF8);
			if (string.Equals(NormalizeNewlines(preferredText), NormalizeNewlines(alternateText), StringComparison.Ordinal))
			{
				existing = preferredText;
				return;
			}

			// Conflict: prefer non-stock edits. WriteOwnedFile backs up whichever scanned
			// copy is not chosen (or remains distinct) before deleting the alternate path.
			bool preservePref = ShouldPreserveOwnedDocument(preferredText);
			bool preserveAlt = ShouldPreserveOwnedDocument(alternateText);

			if (preserveAlt && !preservePref)
			{
				existing = alternateText;
			}
			else
			{
				// Preferred wins when both edited, both stock, or only preferred is preserved.
				existing = preferredText;
			}

			conflictBackupPath = alternatePath + ConflictBackupSuffix;
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
			return OwnedEntryHasCommand(yaml, ToolNameDismantle, ActivateCommandDismantle)
			       || OwnedEntryHasCommand(yaml, ToolNameDelete, ActivateCommandDelete);
		}

		public static bool OwnedEntryHasCommand(string yaml, string toolName, string command)
		{
			if (string.IsNullOrEmpty(yaml))
			{
				return false;
			}

			string[] lines = NormalizeNewlines(yaml).Split('\n');
			bool inOurTool = false;
			var nameRe = new Regex(
				@"^\s*-\s*name:\s*(?:""|')?" + Regex.Escape(toolName) + @"(?:""|')?\s*$",
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
				    && line.IndexOf(command, StringComparison.OrdinalIgnoreCase) >= 0)
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
					if (!BlockLooksOwned(lines, i))
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
			for (int j = nameLineIndex - 1; j >= 0 && j >= nameLineIndex - 3; j--)
			{
				if (lines[j].IndexOf("dismantleheim-tool", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			var anyItem = new Regex(@"^\s*-\s*name:\s*\S");
			var equipmentKey = new Regex(@"^[A-Za-z][A-Za-z0-9_]*:\s*$");
			for (int j = nameLineIndex + 1; j < lines.Length; j++)
			{
				string line = lines[j].TrimEnd('\r');
				if (anyItem.IsMatch(line) || equipmentKey.IsMatch(line))
				{
					break;
				}

				if (line.IndexOf("command:", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}

				if (line.IndexOf(ActivateCommandDismantle, StringComparison.OrdinalIgnoreCase) >= 0
				    || line.IndexOf(ActivateCommandDelete, StringComparison.OrdinalIgnoreCase) >= 0
				    || line.IndexOf(LegacyActivateCommand, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			return false;
		}

		private static void TryRemoveStaleFromSharedDefault(string configRoot)
		{
			string[] names = { ToolNameDismantle, ToolNameDelete, LegacyToolName };
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
				string cleaned = text;
				foreach (string name in names)
				{
					if (CountNamedToolsInDocument(cleaned, name) > 0)
					{
						cleaned = RemoveNamedToolFromShared(cleaned, name);
					}
				}

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
