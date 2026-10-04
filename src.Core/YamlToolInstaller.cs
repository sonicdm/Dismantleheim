using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Dismantleheim.Core
{
	/// <summary>
	/// Idempotent upsert of the Dismantleheim tool into infinity_tools.yaml.
	/// Preserves other tools; merges by tool name.
	/// </summary>
	public static class YamlToolInstaller
	{
		public const string ToolName = "Dismantleheim";
		public const string Marker = "dismantleheim-tool-v1";
		public const string ActivateCommand = "dismantleheim activate";

		public static string BuildOwnedEntry()
		{
			var sb = new StringBuilder();
			sb.AppendLine("# " + Marker);
			sb.AppendLine("- name: " + ToolName);
			sb.AppendLine("  description: \"Queued dismantle mode. Left-click to flag pieces, Shift+Mouse3 to sample prefab filter, hold Mouse3 to confirm. ActivateKey (default Delete) also toggles.\"");
			sb.AppendLine("  icon: Hammer");
			sb.AppendLine("  instant: true");
			sb.AppendLine("  command: " + ActivateCommand);
			return sb.ToString();
		}

		/// <summary>
		/// Upsert our tool into YAML text. Returns new file contents and whether a write is needed.
		/// </summary>
		public static string Upsert(string existingYaml, out bool changed, out int toolCountNamedDismantleheim)
		{
			string owned = BuildOwnedEntry().TrimEnd() + Environment.NewLine;
			string existing = existingYaml ?? string.Empty;

			toolCountNamedDismantleheim = CountNamedTools(existing, ToolName);
			if (toolCountNamedDismantleheim == 1 && ContainsActivateCommand(existing))
			{
				// Already present once with correct command — leave user description edits alone.
				changed = false;
				return existing;
			}

			string stripped = RemoveNamedToolBlocks(existing, ToolName);
			stripped = stripped.TrimEnd();
			string result;
			if (stripped.Length == 0)
			{
				result = owned;
			}
			else
			{
				result = stripped + Environment.NewLine + owned;
			}

			changed = !string.Equals(NormalizeNewlines(existing), NormalizeNewlines(result), StringComparison.Ordinal);
			toolCountNamedDismantleheim = CountNamedTools(result, ToolName);
			return result;
		}

		public static void UpsertToFile(string filePath, bool createDirectory)
		{
			if (string.IsNullOrEmpty(filePath))
			{
				throw new ArgumentException("filePath required", nameof(filePath));
			}

			string dir = Path.GetDirectoryName(filePath);
			if (createDirectory && !string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			string existing = File.Exists(filePath) ? File.ReadAllText(filePath, Encoding.UTF8) : string.Empty;
			string next = Upsert(existing, out bool changed, out _);
			if (!changed && File.Exists(filePath))
			{
				return;
			}

			string temp = filePath + ".tmp";
			File.WriteAllText(temp, next, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			if (File.Exists(filePath))
			{
				File.Replace(temp, filePath, null);
			}
			else
			{
				File.Move(temp, filePath);
			}
		}

		public static int CountNamedTools(string yaml, string toolName)
		{
			if (string.IsNullOrEmpty(yaml))
			{
				return 0;
			}

			int count = 0;
			var re = new Regex(@"^\s*-\s*name:\s*" + Regex.Escape(toolName) + @"\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
			foreach (Match m in re.Matches(yaml))
			{
				count++;
			}

			return count;
		}

		public static bool ContainsActivateCommand(string yaml)
		{
			return yaml != null && yaml.IndexOf(ActivateCommand, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>
		/// Remove YAML list items whose name is toolName (line-oriented, good enough for IH tools files).
		/// </summary>
		public static string RemoveNamedToolBlocks(string yaml, string toolName)
		{
			if (string.IsNullOrEmpty(yaml))
			{
				return string.Empty;
			}

			string[] lines = NormalizeNewlines(yaml).Split(new[] { '\n' }, StringSplitOptions.None);
			var output = new List<string>();
			bool skipping = false;
			var nameRe = new Regex(@"^\s*-\s*name:\s*" + Regex.Escape(toolName) + @"\s*$", RegexOptions.IgnoreCase);
			var anyItemRe = new Regex(@"^\s*-\s*name:\s*\S");

			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				// Strip trailing \r if present
				if (line.EndsWith("\r"))
				{
					line = line.Substring(0, line.Length - 1);
				}

				if (nameRe.IsMatch(line))
				{
					skipping = true;
					// Also drop preceding marker comment for our tool
					if (output.Count > 0 && output[output.Count - 1].IndexOf(Marker, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						output.RemoveAt(output.Count - 1);
					}
					continue;
				}

				if (skipping)
				{
					if (anyItemRe.IsMatch(line))
					{
						skipping = false;
						// fall through to add this new item
					}
					else
					{
						continue;
					}
				}

				output.Add(line);
			}

			return string.Join(Environment.NewLine, output).TrimEnd() + (output.Count > 0 ? Environment.NewLine : string.Empty);
		}

		private static string NormalizeNewlines(string s)
		{
			return (s ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
		}
	}
}
