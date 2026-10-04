using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Xunit;

namespace Dismantleheim.Tests.AssemblyCompatibility
{
	public class AssemblyCompatTests
	{
		[Fact]
		public void Piece_CanBeRemoved_And_Player_RemovePiece_Exist_WhenReqsPresent()
		{
			string reqs = Environment.GetEnvironmentVariable("DISMANTLEHEIM_REQS");
			if (string.IsNullOrEmpty(reqs))
			{
				reqs = @"E:\Scripts\Valheim Mods\Reqs";
			}

			string dll = Path.Combine(reqs, "assembly_valheim.dll");
			if (!File.Exists(dll))
			{
				Console.WriteLine("SKIP: assembly_valheim.dll not found at " + dll);
				return;
			}

			using (ModuleDefinition module = ModuleDefinition.ReadModule(dll))
			{
				TypeDefinition piece = module.Types.FirstOrDefault(t => t.Name == "Piece");
				Assert.NotNull(piece);
				Assert.Contains(piece.Fields, f => f.Name == "m_canBeRemoved");

				TypeDefinition player = module.Types.FirstOrDefault(t => t.Name == "Player");
				Assert.NotNull(player);
				Assert.Contains(player.Methods, m => m.Name == "RemovePiece" && m.Parameters.Count == 0);
				Assert.Contains(player.Methods, m => m.Name == "CheckCanRemovePiece");

				TypeDefinition wear = module.Types.FirstOrDefault(t => t.Name == "WearNTear");
				Assert.NotNull(wear);
				Assert.Contains(wear.Methods, m => m.Name == "Remove");
				Assert.Contains(wear.Methods, m => m.Name == "Highlight");

				TypeDefinition privateArea = module.Types.FirstOrDefault(t => t.Name == "PrivateArea");
				Assert.NotNull(privateArea);
				Assert.Contains(privateArea.Methods, m => m.Name == "CheckAccess");
			}
		}
	}
}
