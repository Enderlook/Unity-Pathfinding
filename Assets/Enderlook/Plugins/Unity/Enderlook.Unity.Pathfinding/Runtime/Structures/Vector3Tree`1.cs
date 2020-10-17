using Enderlook.Collections;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// A wrapper arround <see cref="D3Tree{TKey, TValue}"/> to accept <see cref="Vector3"/>
    /// </summary>
    /// <typeparam name="TValue">Type of value.</typeparam>
    public struct Vector3Tree<TValue> : ISpatialIndex<Vector3, TValue>
    {
        // TODO: use MemoryMarshal to transmutate tuples

#pragma warning disable CS0649
        private readonly D3TreeFloat<TValue> tree;
#pragma warning restore CS0649

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.Contains(TKey)"/>
        public bool Contains(Vector3 key) => tree.Contains(ToTuple(key));

        /// <inheritdoc cref="ISpatialIndex{TKey, TValue}.FindNearestNeighbour(TKey)"/>
        public (Vector3 key, TValue value, double distance) FindNearestNeighbour(Vector3 key) => FromTuple(tree.FindNearestNeighbour(ToTuple(key)));

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.GetValue(TKey)"/>
        public TValue GetValue(Vector3 key) => tree.GetValue(ToTuple(key));

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.Insert(TKey, TValue)"/>
        public void Insert(Vector3 key, TValue value) => tree.Insert(ToTuple(key), value);

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.InsertOrUpdate(TKey, TValue)(TKey, TValue)"/>
        public void InsertOrUpdate(Vector3 key, TValue value) => tree.InsertOrUpdate(ToTuple(key), value);

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.Remove(TKey)"/>
        public bool Remove(Vector3 key) => tree.Remove(ToTuple(key));

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.TryGetValue(TKey, out TValue)"/>
        public bool TryGetValue(Vector3 key, out TValue value) => tree.TryGetValue(ToTuple(key), out value);

        private static (float, float, float) ToTuple(Vector3 vector)
            => (vector.x, vector.y, vector.z);

        private (Vector3 key, TValue value, double distance) FromTuple(((float, float, float) key, TValue value, double distance) value)
            => (new Vector3(value.key.Item1, value.key.Item2, value.key.Item3), value.value, value.distance);
    }
}