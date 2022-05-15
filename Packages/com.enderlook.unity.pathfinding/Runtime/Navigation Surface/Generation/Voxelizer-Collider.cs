using Enderlook.Unity.Jobs;

using System;
using System.Threading.Tasks;

using UnityEngine;

namespace Enderlook.Unity.Pathfinding.Generation
{
    internal partial struct Voxelizer
    {
        /// <summary>
        /// Enqueues colliders to be voxelized.
        /// </summary>
        /// <param name="colliders">Colliders to voxelize.</param>
        /// <param name="mode">If <c>0</c>, only enqueue non-trigger colliders. If <c>1</c>, only enqueue trigger colliders. If <c>2</c>, enqueue both colliders.</param>
        public ValueTask<Voxelizer> Enqueue(Collider[] colliders, int mode)
        {
            meshInformations.EnsureCapacity(colliders.Length);

            if (options.TimeSlicer.ShouldUseTimeSlice)
                return EnqueueAsync(colliders, mode);
            else
                return EnqueueSync(colliders, mode);
        }

        private async ValueTask<Voxelizer> EnqueueAsync(Collider[] colliders, int mode)
        {
            meshInformations.EnsureCapacity(colliders.Length);
            options.PushTask(colliders.Length, "Enqueuing Colliders");
            {
                switch (mode)
                {
                    case 2:
                        foreach (Collider collider in colliders)
                        {
                            Enqueue(collider);
                            options.StepTask();
                            await options.TimeSlicer.Yield();
                        }
                        break;
                    case 1:
                        foreach (Collider collider in colliders)
                        {
                            options.StepTask();
                            await options.TimeSlicer.Yield();
                            if (!collider.isTrigger)
                            {
                                continue;
                            }

                            Enqueue(collider);
                        }
                        break;
                    case 0:
                        foreach (Collider collider in colliders)
                        {
                            options.StepTask();
                            await options.TimeSlicer.Yield();
                            if (collider.isTrigger)
                            {
                                continue;
                            }

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
            meshInformations.EnsureCapacity(colliders.Length);
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
                            {
                                continue;
                            }

                            Enqueue(collider);
                        }
                        break;
                    case 0:
                        foreach (Collider collider in colliders)
                        {
                            options.StepTask();
                            if (collider.isTrigger)
                            {
                                continue;
                            }

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

            Transform transform = collider.transform;
            switch (collider)
            {
                case BoxCollider boxCollider:
                    boxInformations.Add(new BoxInformation(boxCollider.center, boxCollider.size, transform.rotation, transform.lossyScale, transform.position));
                    break;
            }

            throw new NotImplementedException(); // TODO
        }
    }
}
