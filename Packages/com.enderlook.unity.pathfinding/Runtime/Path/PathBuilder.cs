using Enderlook.Collections;
using Enderlook.Collections.LowLevel;
using Enderlook.Pools;
using Enderlook.Unity.Pathfinding.Utils;
using Enderlook.Unity.Threading;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding
{
    /// <summary>
    /// Represents the internal builder of a path.
    /// </summary>
    /// <typeparam name="TNode">Node type.</typeparam>
    /// <typeparam name="TCoord">Coordinate type.</typeparam>
    internal struct PathBuilder<TNode, TCoord> : IPathBuilder<TNode, TCoord>, IPathFeeder<TCoord>
    {
        // Value type wrapper forces JIT to specialize code in generics
        // This replaces interface calls with direct inlineable calls.

        private Container self;

        private sealed class Container
        {
            public readonly HashSet<TNode> visited = new HashSet<TNode>();
            public readonly BinaryHeapMin<TNode, float> toVisit = new BinaryHeapMin<TNode, float>();
            public readonly Dictionary<TNode, float> costs = new Dictionary<TNode, float>();
            public readonly Dictionary<TNode, TNode> edges = new Dictionary<TNode, TNode>();
            public RawList<TCoord> path = RawList<TCoord>.Create();

            public TCoord startPosition;
            public TCoord endPosition;
            public TNode endNode;
            public TNode startNode;

            public Status status;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PathBuilder(Container self) => this.self = self;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PathBuilder<TNode, TCoord> Rent() => new PathBuilder<TNode, TCoord>(ObjectPool<Container>.Shared.Rent());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return() => ObjectPool<Container>.Shared.Return(self);

        /// <summary>
        /// Determines if this builder has a path.
        /// </summary>
        public bool HasPath {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (self.status & Status.Found) == Status.Found;
        }

        /// <summary>
        /// Determines if this builder is pending.
        /// </summary>
        public bool IsPending {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (self.status & Status.Initialized) == Status.Initialized;
        }

        /// <summary>
        /// Determines if the calculation was aborted due to a timedout.<br/>
        /// This method will return <see langword="false"/> if <see cref="HasPath"/> or <see cref="IsPending"/> are <see langword="true"/>.
        /// </summary>
        public bool HasTimedout {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (self.status & Status.Timedout) == Status.Timedout;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.EnqueueToVisit(TNode, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.EnqueueToVisit(TNode node, float priority)
        {
            Debug.Assert(self.status == Status.Initialized);
            self.toVisit.Enqueue(node, priority);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.FinalizeBuilderSession{TGraph, TWatchdog, TWatchdogAwaitable, TWatchdogAwaiter, TThreadingAwaitable, TThreadingAwaiter}(TGraph, CalculationResult, TWatchdog)"/>
        ValueTask IPathBuilder<TNode, TCoord>.FinalizeBuilderSession<TGraph, TWatchdog, TWatchdogAwaitable, TWatchdogAwaiter, TThreadingAwaitable, TThreadingAwaiter>(TGraph graph, CalculationResult result, TWatchdog watchdog)
        {
            if ((self.status & Status.Initialized) == 0) ThrowInvalidOperationException_IsNotInitialized();

            if (result == CalculationResult.PathFound)
            {
                self.path.Clear();
                if (typeof(IGraphLineOfSight<TNode>).IsAssignableFrom(typeof(TGraph)))
                {
                    if (typeof(IGraphLineOfSight<TCoord>).IsAssignableFrom(typeof(TGraph)))
                        return FinalizeBuilderSession_Local<TGraph, TWatchdog, TWatchdogAwaitable, TWatchdogAwaiter, TThreadingAwaitable, TThreadingAwaiter, Toggle.Yes, Toggle.Yes>(graph, watchdog);
                    else
                        return FinalizeBuilderSession_Local<TGraph, TWatchdog, TWatchdogAwaitable, TWatchdogAwaiter, TThreadingAwaitable, TThreadingAwaiter, Toggle.No, Toggle.Yes>(graph, watchdog);
                }
                else if (typeof(IGraphLineOfSight<TCoord>).IsAssignableFrom(typeof(TGraph)))
                    return FinalizeBuilderSession_Local<TGraph, TWatchdog, TWatchdogAwaitable, TWatchdogAwaiter, TThreadingAwaitable, TThreadingAwaiter, Toggle.Yes, Toggle.No>(graph, watchdog);
                else
                    return FinalizeBuilderSession_Local<TGraph, TWatchdog, TWatchdogAwaitable, TWatchdogAwaiter, TThreadingAwaitable, TThreadingAwaiter, Toggle.No, Toggle.No>(graph, watchdog);
            }
            else if (result == CalculationResult.Timedout)
                self.status = Status.Timedout;
            else
                self.status = Status.Finalized;

            return new ValueTask();
        }

        private async ValueTask FinalizeBuilderSession_Local<TGraph, TWatchdog, TWatchdogAwaitable, TWatchdogAwaiter, TThreadingAwaitable, TThreadingAwaiter, THasCoord, THasNode>(TGraph graph, TWatchdog watchdog)
            where TGraph : IGraphLocation<TNode, TCoord>
            where TWatchdog : IWatchdog<TWatchdogAwaitable, TWatchdogAwaiter>, IThreadingPreference<TThreadingAwaitable, TThreadingAwaiter>
            where TWatchdogAwaitable : IAwaitable<TWatchdogAwaiter>
            where TWatchdogAwaiter : IAwaiter
            where TThreadingAwaitable : IAwaitable<TThreadingAwaiter>
            where TThreadingAwaiter : IAwaiter
        {
            EqualityComparer<TCoord> coordComparer = typeof(TCoord).IsValueType ? null : EqualityComparer<TCoord>.Default;

            if (Toggle.IsToggled<THasCoord>() != typeof(IGraphLineOfSight<TCoord>).IsAssignableFrom(typeof(TGraph))
                || Toggle.IsToggled<THasNode>() != typeof(IGraphLineOfSight<TNode>).IsAssignableFrom(typeof(TGraph)))
            {
                Debug.Assert(false);
                return;
            }

            if (Toggle.IsToggled<THasCoord>() || Toggle.IsToggled<THasNode>())
            {
                IGraphLineOfSight<TCoord> lineOfSightCoord = !Toggle.IsToggled<THasCoord>() || typeof(TGraph).IsValueType ? null : (IGraphLineOfSight<TCoord>)graph;
                IGraphLineOfSight<TNode> lineOfSightNode = !Toggle.IsToggled<THasNode>() || typeof(TGraph).IsValueType ? null : (IGraphLineOfSight<TNode>)graph;

                self.path.Add(self.endPosition);

                bool requiresSwitch = false;
                if (Toggle.IsToggled<THasCoord>() && !requiresSwitch)
                    requiresSwitch = (typeof(TGraph).IsValueType ? ((IGraphLineOfSight<TCoord>)graph) : lineOfSightCoord).RequiresUnityThread;
                if (Toggle.IsToggled<THasNode>() && !requiresSwitch)
                    requiresSwitch = (typeof(TGraph).IsValueType ? ((IGraphLineOfSight<TNode>)graph) : lineOfSightNode).RequiresUnityThread;
                if (requiresSwitch && UnityThread.IsMainThread)
                    requiresSwitch = false;
                if (requiresSwitch)
                    await watchdog.ToUnity();

                TCoord previousCoord;
                TCoord currentCoord = self.endPosition;
                TCoord lastOptimizedCoord = currentCoord;

                TNode previousNode = default; // default since compiler can't infer its assignment.
                TNode currentNode = self.endNode;
                TNode lastOptimizedNode = currentNode;
                bool useNode = false;

                TNode to = currentNode; // endNode
                TCoord end2 = graph.ToPosition(to);
                if (!(typeof(TCoord).IsValueType ?
                    EqualityComparer<TCoord>.Default.Equals(currentCoord /* endPosition */, end2)
                    : coordComparer.Equals(currentCoord /* endPosition */, end2)))
                {
                    previousCoord = currentCoord;
                    currentCoord = end2;
                    if (!Toggle.IsToggled<THasCoord>() || !(typeof(TGraph).IsValueType ? ((IGraphLineOfSight<TCoord>)graph) : lineOfSightCoord).HasLineOfSight(lastOptimizedCoord, currentCoord))
                    {
                        self.path.Add(previousCoord);
                        lastOptimizedCoord = previousCoord;
                        if (Toggle.IsToggled<THasNode>())
                            useNode = true;
                    }
                }

#if DEBUG
                self.visited.Clear();
                self.visited.Add(to);
#endif

                if (!useNode)
                {
                    while (self.edges.TryGetValue(to, out TNode from))
                    {
#if DEBUG
                        if (!self.visited.Add(from))
                        {
                            Debug.LogError("Assertion failed. The calculated path has an endless loop. This assertion is only performed when flag DEBUG is enabled.");
                            break;
                        }
#endif
                        to = from;

                        previousCoord = currentCoord;
                        currentCoord = graph.ToPosition(from);
                        if (Toggle.IsToggled<THasNode>())
                        {
                            previousNode = currentNode;
                            currentNode = from;
                        }
                        Debug.Assert(Toggle.IsToggled<THasCoord>());
                        if (!(typeof(TGraph).IsValueType ? ((IGraphLineOfSight<TCoord>)graph) : lineOfSightCoord).HasLineOfSight(lastOptimizedCoord, currentCoord))
                        {
                            self.path.Add(previousCoord);
                            lastOptimizedCoord = previousCoord;
                            if (Toggle.IsToggled<THasNode>())
                            {
                                lastOptimizedNode = previousNode;
                                goto withNode;
                            }
                        }

                        if (watchdog.CanContinue(out TWatchdogAwaitable awaitable))
                            await awaitable;
                        else
                            goto timedout;
                    }
                    goto withoutNode;
                }

            withNode:
                bool hasPrevious = false;
                while (self.edges.TryGetValue(to, out TNode from))
                {
#if DEBUG
                    if (!self.visited.Add(from))
                    {
                        Debug.LogError("Assertion failed. The calculated path has an endless loop. This assertion is only performed when flag DEBUG is enabled.");
                        break;
                    }
#endif
                    to = from;

                    hasPrevious = true;
                    previousNode = currentNode;
                    currentNode = from;
                    if (!(typeof(TGraph).IsValueType ? ((IGraphLineOfSight<TNode>)graph) : lineOfSightNode).HasLineOfSight(lastOptimizedNode, currentNode))
                    {
                        self.path.Add(graph.ToPosition(previousNode));
                        lastOptimizedNode = previousNode;
                    }

                    if (watchdog.CanContinue(out TWatchdogAwaitable awaitable))
                        await awaitable;
                    else
                        goto timedout;
                }

                TCoord start2 = graph.ToPosition(self.startNode);
                if (!(typeof(TCoord).IsValueType ?
                    EqualityComparer<TCoord>.Default.Equals(start2, self.path[self.path.Count - 1])
                    : coordComparer.Equals(start2, self.path[self.path.Count - 1])))
                {
                    previousNode = currentNode;
                    currentNode = self.startNode;
                    if (!(typeof(TGraph).IsValueType ? ((IGraphLineOfSight<TNode>)graph) : lineOfSightNode).HasLineOfSight(lastOptimizedNode, currentNode))
                    {
                        self.path.Add(graph.ToPosition(previousNode));
                        lastOptimizedNode = previousNode;
                    }
                }

                if (hasPrevious)
                    previousCoord = graph.ToPosition(previousNode);
                currentCoord = graph.ToPosition(currentNode);
                lastOptimizedCoord = graph.ToPosition(lastOptimizedNode);
                goto end;

            withoutNode:
                start2 = graph.ToPosition(self.startNode);
                if (!(typeof(TCoord).IsValueType ?
                    EqualityComparer<TCoord>.Default.Equals(start2, self.path[self.path.Count - 1])
                    : coordComparer.Equals(start2, self.path[self.path.Count - 1])))
                {
                    previousCoord = currentCoord;
                    currentCoord = start2;
                    if (!(typeof(TGraph).IsValueType ? ((IGraphLineOfSight<TCoord>)graph) : lineOfSightCoord).HasLineOfSight(lastOptimizedCoord, currentCoord))
                    {
                        self.path.Add(previousCoord);
                        lastOptimizedCoord = previousCoord;
                    }
                }

            end:
                if (!(typeof(TCoord).IsValueType ?
                    EqualityComparer<TCoord>.Default.Equals(self.startPosition, start2)
                    : coordComparer.Equals(self.startPosition, self.startPosition)))
                {
                    previousCoord = currentCoord;
                    currentCoord = self.startPosition;
                    if (!Toggle.IsToggled<THasCoord>() || !(typeof(TGraph).IsValueType ? ((IGraphLineOfSight<TCoord>)graph) : lineOfSightCoord).HasLineOfSight(lastOptimizedCoord, currentCoord))
                        self.path.Add(previousCoord);
                }

                if (self.path.Count == 1)
                    self.path.Add(self.endPosition);

                if (requiresSwitch)
                    await Switch.ToBackground;

                self.path.Reverse();
            }
            else
            {
                if (self.edges.Count == 0)
                {
                    self.path.Add(self.startPosition);

                    TCoord start2 = graph.ToPosition(self.startNode);

                    if (!(typeof(TCoord).IsValueType ?
                        EqualityComparer<TCoord>.Default.Equals(self.startPosition, start2)
                        : coordComparer.Equals(self.startPosition, start2)))
                        self.path.Add(start2);

                    TCoord end2 = graph.ToPosition(self.endNode);
                    if (!(typeof(TCoord).IsValueType ?
                        EqualityComparer<TCoord>.Default.Equals(self.endPosition, end2)
                        : coordComparer.Equals(self.endPosition, end2)))
                        self.path.Add(end2);

                    self.path.Add(self.endPosition);
                }
                else
                {
                    self.path.Add(self.endPosition);

                    TNode to = self.endNode;
                    TCoord end2 = graph.ToPosition(to);
                    if (!(typeof(TCoord).IsValueType ?
                        EqualityComparer<TCoord>.Default.Equals(self.endPosition, end2)
                        : coordComparer.Equals(self.endPosition, end2)))
                        self.path.Add(end2);

#if DEBUG
                    self.visited.Clear();
                    self.visited.Add(to);
#endif

                    while (self.edges.TryGetValue(to, out TNode from))
                    {
#if DEBUG
                        if (!self.visited.Add(from))
                        {
                            Debug.LogError("Assertion failed. The calculated path has an endless loop. This assertion is only performed when flag DEBUG is enabled.");
                            break;
                        }
#endif
                        to = from;
                        self.path.Add(graph.ToPosition(from));

                        if (watchdog.CanContinue(out TWatchdogAwaitable awaitable))
                            await awaitable;
                        else
                            goto timedout;
                    }

                    TCoord start2 = graph.ToPosition(self.startNode);
                    if (!(typeof(TCoord).IsValueType ?
                        EqualityComparer<TCoord>.Default.Equals(self.path[self.path.Count - 1], start2)
                        : coordComparer.Equals(self.path[self.path.Count - 1], start2)))
                        self.path.Add(start2);

                    if (!(typeof(TCoord).IsValueType ?
                        EqualityComparer<TCoord>.Default.Equals(self.startPosition, start2)
                        : coordComparer.Equals(self.startPosition, start2)))
                        self.path.Add(self.startPosition);

                    self.path.Reverse();
                }
            }

            // Finalize
            self.status = Status.Found;
            return;

        timedout:
            self.status = Status.Timedout;
        }

        /// <inheritdoc cref="IPathFeeder{TInfo}.GetPathInfo"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ReadOnlySpan<TCoord> IPathFeeder<TCoord>.GetPathInfo()
        {
            if ((self.status & Status.Finalized) == 0) ThrowInvalidOperationException_IsNotFinalized();
            return self.path.AsSpan();
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.InitializeBuilderSession()"/>
        void IPathBuilder<TNode, TCoord>.InitializeBuilderSession()
        {
            if ((self.status & Status.Initialized) != 0) ThrowInvalidOperationException_IsAlreadyInitialized();

            self.status = Status.Initialized;

            self.visited.Clear();
            self.toVisit.Clear();
            self.costs.Clear();
            self.edges.Clear();
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetCost(TNode, float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetCost(TNode to, float cost)
        {
            Debug.Assert((self.status & Status.Initialized) != 0);
            self.costs[to] = cost;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetEdge(TNode, TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetEdge(TNode from, TNode to)
        {
            Debug.Assert((self.status & Status.Initialized) != 0);
            Debug.Assert(!EqualityComparer<TNode>.Default.Equals(from, to));
            self.edges[to] = from;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetEnd(TCoord, TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetEnd(TCoord endPosition, TNode endNode)
        {
            Debug.Assert((self.status & Status.Initialized) != 0);
            self.endNode = endNode;
            self.endPosition = endPosition;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.SetStart(TCoord, TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void IPathBuilder<TNode, TCoord>.SetStart(TCoord startPosition, TNode startNode)
        {
            Debug.Assert((self.status & Status.Initialized) != 0);
            self.startNode = startNode;
            self.startPosition = startPosition;
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.TryDequeueToVisit(out TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.TryDequeueToVisit(out TNode node)
        {
            Debug.Assert((self.status & Status.Initialized) != 0);
            return self.toVisit.TryDequeue(out node, out _);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.TryGetCost(TNode, out float)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.TryGetCost(TNode to, out float cost)
        {
            Debug.Assert((self.status & Status.Initialized) != 0);
            return self.costs.TryGetValue(to, out cost);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.TryGetEdge(TNode, out TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.TryGetEdge(TNode to, out TNode from)
        {
            Debug.Assert((self.status & Status.Initialized) != 0);
            return self.edges.TryGetValue(to, out from);
        }

        /// <inheritdoc cref="IPathBuilder{TNode, TCoord}.VisitIfWasNotVisited(TNode)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IPathBuilder<TNode, TCoord>.VisitIfWasNotVisited(TNode node)
        {
            Debug.Assert((self.status & Status.Initialized) != 0);
            return self.visited.Add(node);
        }

        [Flags]
        private enum Status : byte
        {
            Initialized = 1 << 0,
            Finalized = 1 << 1,
            Found = Finalized | 1 << 2,
            Timedout = Finalized | 1 << 3,
        }

        private static void ThrowInvalidOperationException_IsNotFinalized()
            => throw new InvalidOperationException("Session has not finalized.");

        private static void ThrowInvalidOperationException_IsAlreadyInitialized()
            => throw new InvalidOperationException("Session is already initialized.");

        private static void ThrowInvalidOperationException_IsNotInitialized()
            => throw new InvalidOperationException("Session has not initialized.");

        private struct Without { }
        private struct WithNode { }
        private struct WithCoord { }
        private struct WithNodeAndCoord { }
    }
}
