using System;
using UnityEngine;

namespace CUBE.Achievements
{
    /// <summary>
    /// Serialisable 128-bit GUID for Unity ScriptableObjects and save files.
    /// Ensures stable identification across renames, asset moves, and localization updates.
    /// </summary>
    [Serializable]
    public struct SerializableGuid : IEquatable<SerializableGuid>
    {
        [SerializeField] private ulong _part1;
        [SerializeField] private ulong _part2;

        public SerializableGuid(Guid guid)
        {
            byte[] bytes = guid.ToByteArray();
            _part1 = BitConverter.ToUInt64(bytes, 0);
            _part2 = BitConverter.ToUInt64(bytes, 8);
        }

        public SerializableGuid(ulong part1, ulong part2)
        {
            _part1 = part1;
            _part2 = part2;
        }

        public static SerializableGuid NewGuid() => new SerializableGuid(Guid.NewGuid());
        public static SerializableGuid Empty => new SerializableGuid(0, 0);

        public bool IsEmpty => _part1 == 0 && _part2 == 0;

        public Guid ToGuid()
        {
            byte[] bytes = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(_part1), 0, bytes, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(_part2), 0, bytes, 8, 8);
            return new Guid(bytes);
        }

        public bool Equals(SerializableGuid other) => _part1 == other._part1 && _part2 == other._part2;
        public override bool Equals(object obj) => obj is SerializableGuid other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_part1, _part2);

        public static bool operator ==(SerializableGuid left, SerializableGuid right) => left.Equals(right);
        public static bool operator !=(SerializableGuid left, SerializableGuid right) => !left.Equals(right);

        public override string ToString() => ToGuid().ToString("N");

        public static SerializableGuid Parse(string value) => new SerializableGuid(Guid.Parse(value));
    }
}
