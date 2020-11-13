using Enderlook.Collections;

using System;
using System.Runtime.CompilerServices;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    internal struct Vector3Tree<TValue> : ISpatialIndex<Vector3, TValue>
    {
        private D3TreeFloat<TValue> kdTree;

        /// <inheritdoc cref="D3TreeFloat{TValue}.Count"/>
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => kdTree.Count;
        }

        public bool IsDefault => kdTree is null;

        /// <inheritdoc cref="D3TreeFloat{TValue}.IsEmpty"/>
        public bool IsEmpty {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => kdTree.IsEmpty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3Tree(D3TreeFloat<TValue> kdTree)
        {
            if (kdTree is null)
                throw new ArgumentNullException(nameof(kdTree));

            this.kdTree = kdTree;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (float, float, float) ToTuple(ref Vector3 key) => Unsafe.As<Vector3, (float, float, float)>(ref key);

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.Contains(TKey)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(Vector3 key) => kdTree.Contains(ToTuple(ref key));

        /// <inheritdoc cref="ISpatialIndex{TKey, TValue}.FindNearestNeighbour(TKey)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (Vector3 key, TValue value, double distance) FindNearestNeighbour(Vector3 key)
        {
            ((float, float, float) key, TValue value, double distance) val = kdTree.FindNearestNeighbour(ToTuple(ref key));
            return Unsafe.As<((float, float, float) key, TValue value, double distance), (Vector3 key, TValue value, double distance)>(ref val);
        }

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.GetValue(TKey)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetValue(Vector3 key) => kdTree.GetValue(ToTuple(ref key));

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.Insert(TKey, TValue)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(Vector3 key, TValue value) => kdTree.Insert(ToTuple(ref key), value);

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.InsertOrUpdate(TKey, TValue)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InsertOrUpdate(Vector3 key, TValue value) => kdTree.InsertOrUpdate(ToTuple(ref key), value);

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.Remove(TKey)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(Vector3 key) => kdTree.Remove(ToTuple(ref key));

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.TryGetValue(TKey, out TValue)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(Vector3 key, out TValue value) => kdTree.TryGetValue(ToTuple(ref key), out value);

        /// <inheritdoc cref="ISpatialIndexBasic{TKey, TValue}.Clear"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => kdTree.Clear();
    }
}
