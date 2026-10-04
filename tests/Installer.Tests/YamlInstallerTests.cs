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
		public void WriteOwnedDocument_Idempotent()
		{
			YamlToolInstaller.WriteOwnedDocument("", out string first);
			bool changed = YamlToolInstaller.WriteOwnedDocument(first, out string second);
			Assert.False(changed);
			Assert.Equal(Normalize(first), Normalize(second));
		}

		[Fact]
		public void WriteOwnedFile_DoesNotMutateSharedDefault_ExceptStaleCleanup()
		{
			string dir = Path.Combine(Path.GetTempPath(), "dismantleheim-ih-" + Path.GetRandomFileName());
			Directory.CreateDirectory(dir);
			try
			{
				string shared = Path.Combine(dir, "infinity_tools.yaml");
				File.WriteAllText(
					shared,
					"hammer:\n- name: OtherTool\n  command: echo hi\n- name: Dismantleheim\n  command: dismantleheim activate\n");

				YamlToolInstaller.WriteOwnedFile(dir, preferToolsSubfolder: false);

				string owned = File.ReadAllText(Path.Combine(dir, YamlToolInstaller.OwnedFileName));
				Assert.Equal(1, YamlToolInstaller.CountNamedToolsInDocument(owned, "Dismantleheim"));
				Assert.True(YamlToolInstaller.LooksLikeEquipmentDictionary(owned));

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
		public void RemoveNamedToolFromShared_PreservesQuotedAndOtherCommands()
		{
			string existing =
				"hammer:\n"
				+ "- name: OtherTool\n"
				+ "  command: dismantleheim activate\n"
				+ "- name: \"Dismantleheim\"\n"
				+ "  description: custom\n"
				+ "  command: dismantleheim activate\n"
				+ "hoe:\n"
				+ "- name: HoeThing\n"
				+ "  command: x\n";

			Assert.Equal(1, YamlToolInstaller.CountNamedToolsInDocument(existing, "Dismantleheim"));
			string cleaned = YamlToolInstaller.RemoveNamedToolFromShared(existing, "Dismantleheim");
			Assert.Equal(0, YamlToolInstaller.CountNamedToolsInDocument(cleaned, "Dismantleheim"));
			Assert.Contains("OtherTool", cleaned);
			Assert.Contains("command: dismantleheim activate", cleaned);
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

		[Fact]
		public void WriteOwnedFile_PrefersToolsSubfolderWhenPresent()
		{
			string dir = Path.Combine(Path.GetTempPath(), "dismantleheim-ih-" + Path.GetRandomFileName());
			string tools = Path.Combine(dir, "tools");
			Directory.CreateDirectory(tools);
			try
			{
				YamlToolInstaller.WriteOwnedFile(dir, preferToolsSubfolder: true);
				Assert.True(File.Exists(Path.Combine(tools, YamlToolInstaller.OwnedFileName)));
				Assert.False(File.Exists(Path.Combine(dir, YamlToolInstaller.OwnedFileName)));
			}
			finally
			{
				Directory.Delete(dir, true);
			}
		}

		private static string Normalize(string s) => (s ?? "").Replace("\r\n", "\n").Trim();
	}
}
