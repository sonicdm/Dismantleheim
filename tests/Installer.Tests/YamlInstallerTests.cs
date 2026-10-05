using System.IO;
using Dismantleheim.Core;
using Xunit;

namespace Dismantleheim.Tests.Installer
{
	public class YamlInstallerTests
	{
		[Fact]
		public void OwnedDocument_HasTwoUniqueTools()
		{
			string doc = YamlToolInstaller.BuildOwnedDocument();
			Assert.True(YamlToolInstaller.LooksLikeEquipmentDictionary(doc));
			Assert.Contains("hammer:", doc);
			Assert.Equal(1, YamlToolInstaller.CountNamedToolsInDocument(doc, YamlToolInstaller.ToolNameDismantle));
			Assert.Equal(1, YamlToolInstaller.CountNamedToolsInDocument(doc, YamlToolInstaller.ToolNameDelete));
			Assert.Contains(YamlToolInstaller.ActivateCommandDismantle, doc);
			Assert.Contains(YamlToolInstaller.ActivateCommandDelete, doc);
			Assert.Contains(YamlToolInstaller.Marker, doc);
			Assert.True(YamlToolInstaller.OwnedEntryHasActivateCommand(doc));
		}

		[Fact]
		public void WriteOwnedDocument_Idempotent_ForStock()
		{
			YamlToolInstaller.WriteOwnedDocument("", out string first);
			bool changed = YamlToolInstaller.WriteOwnedDocument(first, out string second);
			Assert.False(changed);
			Assert.Equal(Normalize(first), Normalize(second));
		}

		[Fact]
		public void WriteOwnedDocument_PreservesUserEditedIcon()
		{
			string edited = YamlToolInstaller.BuildOwnedDocument().Replace("icon: Hammer", "icon: Pickaxe");
			Assert.True(YamlToolInstaller.ShouldPreserveOwnedDocument(edited));
			bool changed = YamlToolInstaller.WriteOwnedDocument(edited, out string next);
			Assert.False(changed);
			Assert.Contains("icon: Pickaxe", next);
		}

		[Fact]
		public void WriteOwnedDocument_PreservesRenamedToolEntry()
		{
			string renamed = YamlToolInstaller.BuildOwnedDocument()
				.Replace(YamlToolInstaller.ToolNameDismantle, "MyDismantleTool")
				.Replace(YamlToolInstaller.ToolNameDelete, "MyDeleteTool");
			Assert.False(YamlToolInstaller.IsUpgradeableStockDocument(renamed));
			Assert.True(YamlToolInstaller.ShouldPreserveOwnedDocument(renamed));
			bool changed = YamlToolInstaller.WriteOwnedDocument(renamed, out string next);
			Assert.False(changed);
			Assert.Contains("MyDismantleTool", next);
			Assert.DoesNotContain(YamlToolInstaller.ToolNameDismantle, next);
		}

		[Fact]
		public void WriteOwnedFile_ConflictingCopies_BacksUpAlternateBeforeDelete()
		{
			string dir = Path.Combine(Path.GetTempPath(), "dismantleheim-ih-conflict-" + Path.GetRandomFileName());
			Directory.CreateDirectory(dir);
			string tools = Path.Combine(dir, "tools");
			Directory.CreateDirectory(tools);
			try
			{
				string rootOwned = Path.Combine(dir, YamlToolInstaller.OwnedFileName);
				string toolsOwned = Path.Combine(tools, YamlToolInstaller.OwnedFileName);
				string editedRoot = YamlToolInstaller.BuildOwnedDocument()
					.Replace("icon: Hammer", "icon: Pickaxe");
				File.WriteAllText(rootOwned, editedRoot);
				File.WriteAllText(toolsOwned, YamlToolInstaller.BuildOwnedDocument());

				YamlToolInstaller.WriteOwnedFile(dir, preferToolsSubfolder: true);

				Assert.True(File.Exists(toolsOwned));
				Assert.False(File.Exists(rootOwned));
				// Preferred was stock; edited alternate wins and is written to preferred.
				Assert.Contains("icon: Pickaxe", File.ReadAllText(toolsOwned));
				// If anything distinct remains only at alternate, it is parked as .bak (not needed when winner was alternate).
				string bak = rootOwned + YamlToolInstaller.ConflictBackupSuffix;
				Assert.False(File.Exists(bak), "winner content lives at preferred; no orphan bak required");
			}
			finally
			{
				Directory.Delete(dir, true);
			}
		}

		[Fact]
		public void WriteOwnedFile_BothEdited_BacksUpLosingAlternate()
		{
			string dir = Path.Combine(Path.GetTempPath(), "dismantleheim-ih-both-" + Path.GetRandomFileName());
			Directory.CreateDirectory(dir);
			string tools = Path.Combine(dir, "tools");
			Directory.CreateDirectory(tools);
			try
			{
				string rootOwned = Path.Combine(dir, YamlToolInstaller.OwnedFileName);
				string toolsOwned = Path.Combine(tools, YamlToolInstaller.OwnedFileName);
				File.WriteAllText(
					toolsOwned,
					YamlToolInstaller.BuildOwnedDocument().Replace("icon: Hammer", "icon: Pickaxe"));
				File.WriteAllText(
					rootOwned,
					YamlToolInstaller.BuildOwnedDocument().Replace("icon: Hammer", "icon: Axe"));

				YamlToolInstaller.WriteOwnedFile(dir, preferToolsSubfolder: true);

				Assert.Contains("icon: Pickaxe", File.ReadAllText(toolsOwned));
				Assert.False(File.Exists(rootOwned));
				string bak = rootOwned + YamlToolInstaller.ConflictBackupSuffix;
				Assert.True(File.Exists(bak));
				Assert.Contains("icon: Axe", File.ReadAllText(bak));
			}
			finally
			{
				Directory.Delete(dir, true);
			}
		}

		[Fact]
		public void WriteOwnedFile_MigratesAndCleansLegacyShared()
		{
			string dir = Path.Combine(Path.GetTempPath(), "dismantleheim-ih-" + Path.GetRandomFileName());
			Directory.CreateDirectory(dir);
			string tools = Path.Combine(dir, "tools");
			Directory.CreateDirectory(tools);
			try
			{
				string rootOwned = Path.Combine(dir, YamlToolInstaller.OwnedFileName);
				File.WriteAllText(rootOwned, YamlToolInstaller.BuildOwnedDocument());
				string shared = Path.Combine(dir, "infinity_tools.yaml");
				File.WriteAllText(
					shared,
					"hammer:\n- name: Dismantleheim\n  command: dismantleheim activate\n- name: OtherTool\n  command: echo\n");

				YamlToolInstaller.WriteOwnedFile(dir, preferToolsSubfolder: true);

				Assert.True(File.Exists(Path.Combine(tools, YamlToolInstaller.OwnedFileName)));
				Assert.False(File.Exists(rootOwned));
				string sharedAfter = File.ReadAllText(shared);
				Assert.Equal(0, YamlToolInstaller.CountNamedToolsInDocument(sharedAfter, "Dismantleheim"));
				Assert.Contains("OtherTool", sharedAfter);
			}
			finally
			{
				Directory.Delete(dir, true);
			}
		}

		[Fact]
		public void RemoveNamedTool_PreservesUserToolWithoutOurCommand()
		{
			string existing =
				"hammer:\n"
				+ "- name: Dismantleheim - Mass Dismantle\n"
				+ "  command: my custom tool\n";
			string cleaned = YamlToolInstaller.RemoveNamedToolFromShared(
				existing, YamlToolInstaller.ToolNameDismantle);
			Assert.Contains("my custom tool", cleaned);
		}

		private static string Normalize(string s) => (s ?? "").Replace("\r\n", "\n").Trim();
	}
}
