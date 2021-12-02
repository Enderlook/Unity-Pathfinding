using Enderlook.Unity.Jobs;

using System;
using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal partial struct Voxelizer : IDisposable
    {
        /// <summary>
        /// Enqueues colliders to be voxelized.
        /// </summary>
        /// <param name="colliders">Colliders to voxelize.</param>
        /// <param name="mode">If <c>0</c>, only enqueue non-trigger colliders. If <c>1</c>, only enqueue trigger colliders. If <c>2</c>, enqueue both colliders.</param>
        public ValueTask<Voxelizer> Enqueue(Collider[] colliders, int mode)
        {
            information.EnsureCapacity(colliders.Length);

            if (options.ShouldUseTimeSlice)
                return EnqueueAsync(colliders, mode);
            else
                return EnqueueSync(colliders, mode);
        }

        private async ValueTask<Voxelizer> EnqueueAsync(Collider[] colliders, int mode)
        {
            information.EnsureCapacity(colliders.Length);
            options.PushTask(colliders.Length, "Enqueuing Colliders");
            {
                switch (mode)
                {
                    case 2:
                        foreach (Collider collider in colliders)
                        {
                            Enqueue(collider);
                            await options.StepTaskAndYield();
                        }
                        break;
                    case 1:
                        foreach (Collider collider in colliders)
                        {
                            await options.StepTaskAndYield();
                            if (!collider.isTrigger)
                                continue;
                            Enqueue(collider);
                        }
                        break;
                    case 0:
                        foreach (Collider collider in colliders)
                        {
                            await options.StepTaskAndYield();
                            if (collider.isTrigger)
                                continue;
                            Enqueue(collider);
                        }
                        break;
                    default:
                        Debug.Assert(false, $"Invalid {nameof(mode)} input.");
                        break;
                }
            }
            options.PopTask();
            return this;
        }

        private ValueTask<Voxelizer> EnqueueSync(Collider[] colliders, int mode)
        {
            information.EnsureCapacity(colliders.Length);
            options.PushTask(colliders.Length, "Enqueuing Meshes");
            {
                switch (mode)
                {
                    case 2:
                        foreach (Collider collider in colliders)
                        {
                            Enqueue(collider);
                            options.StepTask();
                        }
                        break;
                    case 1:
                        foreach (Collider collider in colliders)
                        {
                            options.StepTask();
                            if (!collider.isTrigger)
                                continue;
                            Enqueue(collider);
                        }
                        break;
                    case 0:
                        foreach (Collider collider in colliders)
                        {
                            options.StepTask();
                            if (collider.isTrigger)
                                continue;
                            Enqueue(collider);
                        }
                        break;
                    default:
                        Debug.Assert(false, $"Invalid {nameof(mode)} input.");
                        break;
                }
            }
            options.PopTask();
            return new ValueTask<Voxelizer>(this);
        }

        private void Enqueue(Collider collider)
        {
            GameObject gameObject = collider.gameObject;
            if (!gameObject.activeInHierarchy || (includeLayers & 1 << gameObject.layer) == 0)
                return;

            throw new NotImplementedException(); // TODO
        }

        private void VoxelizeBox(BoxCollider boxCollider)
        {
            if (!options.VoxelizationParameters.Bounds.Intersects(boxCollider.bounds))
                return;

             throw new NotImplementedException(); // TODO
        }
    }
}
