using System;

namespace Dismantleheim.Core
{
	/// <summary>
	/// Network-stable identity for a queued dismantle target. No Unity GameObject references.
	/// </summary>
	public sealed class TargetIdentity : IEquatable<TargetIdentity>
	{
		public TargetIdentity(
			long userId,
			uint objectId,
			string worldSessionKey,
			string prefabName,
			bool hasPiece,
			bool canBeRemoved,
			bool isEnvironment,
			TargetKind kind,
			bool isStale = false)
		{
			UserId = userId;
			ObjectId = objectId;
			WorldSessionKey = worldSessionKey ?? string.Empty;
			PrefabName = prefabName ?? string.Empty;
			HasPiece = hasPiece;
			CanBeRemoved = canBeRemoved;
			IsEnvironment = isEnvironment;
			Kind = kind;
			IsStale = isStale;
		}

		public long UserId { get; }

		public uint ObjectId { get; }

		public long NetworkId => ObjectId;

		/// <summary>Unique world/session epoch (e.g. ZNet world UID string).</summary>
		public string WorldSessionKey { get; }

		public string PrefabName { get; }

		public bool HasPiece { get; }

		public bool CanBeRemoved { get; }

		public bool IsEnvironment { get; }

		public TargetKind Kind { get; }

		public bool IsStale { get; }

		public bool Equals(TargetIdentity other)
		{
			if (other == null)
			{
				return false;
			}

			return UserId == other.UserId
			       && ObjectId == other.ObjectId
			       && string.Equals(WorldSessionKey, other.WorldSessionKey, StringComparison.Ordinal);
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as TargetIdentity);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = UserId.GetHashCode();
				hash = (hash * 397) ^ ObjectId.GetHashCode();
				hash = (hash * 397) ^ (WorldSessionKey != null ? WorldSessionKey.GetHashCode() : 0);
				return hash;
			}
		}
	}
}
