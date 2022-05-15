using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(Rigidbody), typeof(Renderer))]
    public sealed class Bullet : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("Damage done on hit.")]
        private int damage = 1;

        [SerializeField, Min(0), Tooltip("Speed of projectile.")]
        private float speed = 1;

        private Vector3 origin;

        private Transform owner;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Awake()
        {
            Rigidbody rigidbody = GetComponent<Rigidbody>();
            rigidbody.velocity = transform.forward * speed;
            origin = rigidbody.position;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnCollisionEnter(Collision collision)
        {
            Transform current = collision.gameObject.transform;
            while (current != null)
            {
                if (current == owner)
                    return;
                current = current.parent;
            }
            Destroy(gameObject);
            if (collision.gameObject.layer != gameObject.layer)
            {
                Creature creature = collision.gameObject.GetComponentInParent<Creature>();
                if (creature != null)
                    creature.TakeDamage(damage, origin);
            }
        }

        public void Initialize(GameObject owner, Material material, int layer)
        {
            gameObject.layer = layer;
            this.owner = owner.transform;
            GetComponent<Renderer>().material = material;
        }
    }
}