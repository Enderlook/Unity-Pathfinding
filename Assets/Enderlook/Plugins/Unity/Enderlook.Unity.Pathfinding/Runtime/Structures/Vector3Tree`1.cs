using Enderlook.Collections;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// A wrapper arround <see cref="D3Tree{TKey, TValue}"/> to accept <see cref="Vector3"/>
    /// </summary>
    /// <typeparam name="TValue">Type of value.</typeparam>
    public struct Vector3Tree<TValue> : ISpatialIndex<Vector3, TValue>
        where TValue : unmanaged
    {
        // TODO: use MemoryMarshal in order to remove the unmanaged constrain to TValue

        private readonly D3TreeFloat<TValue> tree;

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

        private static unsafe (float, float, float) ToTuple(Vector3 vector) =>
#pragma warning disable IDE0047 // Remove unnecessary parentheses
            *((float, float, float) *)(&vector);
#pragma warning restore IDE0047 // Remove unnecessary parentheses


        private unsafe (Vector3 key, TValue value, double distance) FromTuple(((float, float, float) key, TValue value, double distance) value) =>
#pragma warning disable IDE0047 // Remove unnecessary parentheses
            *((Vector3, TValue, double) *)(&value);
#pragma warning restore IDE0047 // Remove unnecessary parentheses

    }
}