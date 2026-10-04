using System.IO;
using Dismantleheim.Core;
using Xunit;

namespace Dismantleheim.Tests.Installer
{
	public class YamlInstallerTests
	{
		[Fact]
		public void Upsert_Fresh_WritesOneEntry()
		{
			string result = YamlToolInstaller.Upsert("", out bool changed, out int count);
			Assert.True(changed);
			Assert.Equal(1, count);
			Assert.Contains("name: Dismantleheim", result);
			Assert.Contains("dismantleheim activate", result);
			Assert.Contains("instant: true", result);
		}

		[Fact]
		public void Upsert_Twice_Idempotent()
		{
			string first = YamlToolInstaller.Upsert("", out _, out _);
			string second = YamlToolInstaller.Upsert(first, out bool changed, out int count);
			Assert.False(changed);
			Assert.Equal(1, count);
			Assert.Equal(Normalize(first), Normalize(second));
		}

		[Fact]
		public void Upsert_PreservesOtherTools()
		{
			string existing = "- name: OtherTool\n  command: echo hi\n  instant: true\n";
			string result = YamlToolInstaller.Upsert(existing, out _, out int count);
			Assert.Equal(1, count);
			Assert.Contains("name: OtherTool", result);
			Assert.Contains("name: Dismantleheim", result);
			Assert.Equal(1, YamlToolInstaller.CountNamedTools(result, "OtherTool"));
		}

		[Fact]
		public void Upsert_RemovesDuplicateDismantleheim()
		{
			string existing =
				YamlToolInstaller.BuildOwnedEntry()
				+ YamlToolInstaller.BuildOwnedEntry();
			Assert.Equal(2, YamlToolInstaller.CountNamedTools(existing, "Dismantleheim"));
			string result = YamlToolInstaller.Upsert(existing, out _, out int count);
			Assert.Equal(1, count);
		}

		[Fact]
		public void UpsertToFile_Atomic()
		{
			string dir = Path.Combine(Path.GetTempPath(), "dismantleheim-test-" + Path.GetRandomFileName());
			Directory.CreateDirectory(dir);
			try
			{
				string path = Path.Combine(dir, "infinity_tools.yaml");
				File.WriteAllText(path, "- name: KeepMe\n  command: x\n");
				YamlToolInstaller.UpsertToFile(path, createDirectory: false);
				YamlToolInstaller.UpsertToFile(path, createDirectory: false);
				string text = File.ReadAllText(path);
				Assert.Equal(1, YamlToolInstaller.CountNamedTools(text, "Dismantleheim"));
				Assert.Contains("KeepMe", text);
			}
			finally
			{
				Directory.Delete(dir, true);
			}
		}

		private static string Normalize(string s) => (s ?? "").Replace("\r\n", "\n").Trim();
	}
}
