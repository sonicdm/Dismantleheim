using System.IO;
using Dismantleheim.Core;
using Xunit;

namespace Dismantleheim.Tests.Installer
{
	public class YamlInstallerTests
	{
		[Fact]
		public void OwnedDocument_IsEquipmentKeyedDictionary()
		{
			string doc = YamlToolInstaller.BuildOwnedDocument();
			Assert.True(YamlToolInstaller.LooksLikeEquipmentDictionary(doc));
			Assert.Contains("hammer:", doc);
			Assert.Contains("name: Dismantleheim", doc);
			Assert.Contains("dismantleheim activate", doc);
			Assert.Contains("tabIndex: 0", doc);
			Assert.Equal(1, YamlToolInstaller.CountNamedToolsInDocument(doc, "Dismantleheim"));
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
			Assert.DoesNotContain("icon: Hammer", next);
		}

		[Fact]
		public void WriteOwnedFile_MigratesRootToTools_AndCleansShared()
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
					"hammer:\n# dismantleheim-tool-v2\n- name: Dismantleheim\n  command: dismantleheim activate\n- name: OtherTool\n  command: echo hi\n");

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
		public void WriteOwnedFile_UnchangedStillCleansSharedStale()
		{
			string dir = Path.Combine(Path.GetTempPath(), "dismantleheim-ih-" + Path.GetRandomFileName());
			Directory.CreateDirectory(dir);
			try
			{
				string owned = Path.Combine(dir, YamlToolInstaller.OwnedFileName);
				File.WriteAllText(owned, YamlToolInstaller.BuildOwnedDocument());
				string shared = Path.Combine(dir, "infinity_tools.yaml");
				File.WriteAllText(
					shared,
					"hammer:\n- name: Dismantleheim\n  command: dismantleheim activate\n- name: Keep\n  command: x\n");

				YamlToolInstaller.WriteOwnedFile(dir, preferToolsSubfolder: false);

				string sharedAfter = File.ReadAllText(shared);
				Assert.Equal(0, YamlToolInstaller.CountNamedToolsInDocument(sharedAfter, "Dismantleheim"));
				Assert.Contains("Keep", sharedAfter);
			}
			finally
			{
				Directory.Delete(dir, true);
			}
		}

		[Fact]
		public void RemoveNamedToolFromShared_PreservesUserNamedToolWithoutOurCommand()
		{
			string existing =
				"hammer:\n"
				+ "- name: Dismantleheim\n"
				+ "  command: my custom tool\n"
				+ "- name: OtherTool\n"
				+ "  command: dismantleheim activate\n";

			string cleaned = YamlToolInstaller.RemoveNamedToolFromShared(existing, "Dismantleheim");
			Assert.Contains("name: Dismantleheim", cleaned);
			Assert.Contains("my custom tool", cleaned);
			Assert.Contains("OtherTool", cleaned);
		}

		[Fact]
		public void RemoveNamedToolFromShared_RemovesOwnedActivateCommandBlock()
		{
			string existing =
				"hammer:\n"
				+ "- name: \"Dismantleheim\"\n"
				+ "  description: custom\n"
				+ "  command: dismantleheim activate\n"
				+ "hoe:\n"
				+ "- name: HoeThing\n"
				+ "  command: x\n";

			string cleaned = YamlToolInstaller.RemoveNamedToolFromShared(existing, "Dismantleheim");
			Assert.Equal(0, YamlToolInstaller.CountNamedToolsInDocument(cleaned, "Dismantleheim"));
			Assert.Contains("HoeThing", cleaned);
		}

		[Fact]
		public void OwnedEntryHasActivateCommand_IgnoresOtherToolCommand()
		{
			string yaml =
				"hammer:\n"
				+ "- name: OtherTool\n"
				+ "  command: dismantleheim activate\n"
				+ "- name: Dismantleheim\n"
				+ "  command: something else\n";
			Assert.False(YamlToolInstaller.OwnedEntryHasActivateCommand(yaml));
		}

		private static string Normalize(string s) => (s ?? "").Replace("\r\n", "\n").Trim();
	}
}
