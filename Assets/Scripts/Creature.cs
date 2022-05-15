using Enderlook.StateMachine;
using Enderlook.Unity.Pathfinding;
using Enderlook.Unity.Pathfinding.Steerings;

using System;

using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(PathFollower), typeof(NavigationAgentRigidbody), typeof(Rigidbody))]
    public abstract partial class Creature : MonoBehaviour
    {
        private readonly static StateMachineFactory<State, Event, Creature> brainFactory;

        static Creature()
        {
            Func<Creature, bool> shouldFlee = @this => @this.health < @this.startFleeingHealth;
            Func<Creature, bool> isDeath = @this => @this.health <= 0;
            brainFactory = StateMachine<State, Event, Creature>.CreateFactoryBuilder()
                .SetInitialState(State.MovingToTarget)
                .In(State.MovingToTarget)
                    .OnEntry(@this => @this.MovingToTarget_OnEntry())
                    .OnUpdate(@this => @this.MovingToTarget_OnUpdate())
                    .OnExit(@this => @this.MovingToTarget_OnExit())
                    .On(Event.ReachedToTarget)
                        .Goto(State.Idle)
                    .On(Event.EnemyEnteredInSight)
                        .Goto(State.ChasingEnemy)
                    .On(Event.EnemyEnteredInAttackRange)
                        .Goto(State.AttackingEnemy)
                    .On(Event.WasHurt)
                        .If(shouldFlee)
                            .If(isDeath)
                                .Goto(State.Death)
                            .Goto(State.Fleeing)
                        .Goto(State.TrackingEnemy)
                    .Ignore(Event.FullyHealed)
                .In(State.Idle)
                    .OnUpdate(@this => @this.Idle_OnUpdate())
                    .On(Event.EnemyEnteredInSight)
                        .If(shouldFlee)
                            .Goto(State.Fleeing)
                        .Goto(State.ChasingEnemy)
                    .On(Event.EnemyEnteredInAttackRange)
                        .If(shouldFlee)
                            .Goto(State.Fleeing)
                        .Goto(State.AttackingEnemy)
                    .On(Event.FullyHealed)
                        .Goto(State.MovingToTarget)
                    .On(Event.WasHurt)
                        .If(shouldFlee)
                            .If(isDeath)
                                .Goto(State.Death)
                            .Goto(State.Fleeing)
                        .Goto(State.TrackingEnemy)
                .In(State.ChasingEnemy)
                    .OnEntry<Creature>((@this, enemy) => @this.ChasingEnemy_OnEntry(enemy))
                    .OnUpdate(@this => @this.ChasingEnemy_OnUpdate())
                    .OnExit(@this => @this.ChasingEnemy_OnExit())
                    .On(Event.EnemyGotOutOfSight)
                        .Goto(State.TrackingEnemy)
                    .On(Event.EnemyEnteredInAttackRange)
                        .Goto(State.AttackingEnemy)
                    .On(Event.WasHurt)
                        .If(shouldFlee)
                            .If(isDeath)
                                .Goto(State.Death)
                            .Goto(State.Fleeing)
                        .StaySelf()
                    .Ignore(Event.FullyHealed)
                .In(State.TrackingEnemy)
                    .OnEntry(@this => @this.TrackingEnemy_OnEntry())
                    .OnUpdate(@this => @this.TrackingEnemy_OnUpdate())
                    .OnExit(@this => @this.TrackingEnemy_OnExit())
                    .On(Event.EnemyEnteredInSight)
                        .Goto(State.ChasingEnemy)
                    .On(Event.EnemyLostTrack)
                        .Goto(State.MovingToTarget)
                    .On(Event.WasHurt)
                        .If(shouldFlee)
                            .If(isDeath)
                                .Goto(State.Death)
                            .Goto(State.Fleeing)
                        .StaySelf()
                    .Ignore(Event.FullyHealed)
                .In(State.AttackingEnemy)
                    .OnEntry(@this => @this.AttackingEnemy_OnEntry())
                    .OnEntry<Creature>((@this, creature) => @this.AttackingEnemy_OnEntry(creature))
                    .OnExit(@this => @this.AttackingEnemy_OnExit())
                    .OnUpdate(@this => @this.AttackingEnemy_OnUpdate())
                    .On(Event.EnemyGotOutOfSight)
                        .Goto(State.TrackingEnemy)
                    .On(Event.EnemyGotOutOfAttackRange)
                        .Goto(State.ChasingEnemy)
                    .On(Event.WasHurt)
                        .If(shouldFlee)
                            .If(isDeath)
                                .Goto(State.Death)
                            .Goto(State.Fleeing)
                        .StaySelf()
                .In(State.Fleeing)
                    .OnEntry(@this => @this.Fleeing_OnEntry())
                    .OnExit(@this => @this.Fleeing_OnExit())
                    .OnUpdate(@this => @this.Fleeing_OnUpdate())
                    .On(Event.EnemyGotOutOfSight)
                        .Goto(State.Idle)
                    .On(Event.WasHurt)
                        .If(isDeath)
                            .Goto(State.Death)
                        .StaySelf()
                    .On(Event.FullyHealed)
                        .Goto(State.Idle)
                .In(State.Death)
                    .OnEntry(@this => @this.Death_OnEntry())
                    .OnExit(@this => @this.Death_OnExit())
                    .On(Event.Resurrect)
                        .Goto(State.MovingToTarget)
                    .Ignore(Event.WasHurt)
                .Finalize();
        }

        [Header("Main")]
        [SerializeField, Min(1), Tooltip("Maximum health points.")]
        private int maximumHealth;

        [SerializeField, Min(0), Tooltip("Health threshold to start fleeing.")]
        private int startFleeingHealth;

        [SerializeField, Min(0), Tooltip("Time to recover a hitpoint. Use 0 to disable.")]
        private float healingRate;

        [Header("Vision")]
        [SerializeField, Min(0), Tooltip("Determines at which radius the creature can see the other creatures.")]
        private float sightRadius;

        [SerializeField, Tooltip("Layer that can block creature sight.")]
        private LayerMask blockSight;

        [SerializeField, Tooltip("Offset of eyes.")]
        private float eyesOffset;

        [SerializeField, Min(0), Tooltip("Determines until which distance from enemy the creature will try to escape.")]
        private float escapeRadius;

        [SerializeField, Min(0), Tooltip("Fleeing duration after lost enemies from sight.")]
        private float fleeingDuration;

        [Header("Attack")]
        [SerializeField, Min(0), Tooltip("Minimum distance to start attacking.")]
        private float attackMinimumDistance;

        [SerializeField, Tooltip("Maximum distance to keep attacking.")]
        private float attackMaximumDistance;

        [SerializeField, Min(0), Tooltip("Time cooldown between shoots.")]
        private float fireCooldown = 1;

        [SerializeField, Tooltip("Bullet prefab.")]
        private Bullet bulletPrefab;

        [SerializeField, Tooltip("Shoot point to spawn bullet.")]
        private Transform bulletSpawnPoint;

        [Header("Pathfinding")]
        [SerializeField, Tooltip("Path follower used to follow enemies.")]
        private PathFollower enemyFollower;

        [SerializeField, Tooltip("Obstacle avoidance used when escaping from enemies.")]
        private ObstacleAvoidance enemyAvoidance;

        [SerializeField, Min(0), Tooltip("Strength of obstacle avoidance used when escaping from enemies.")]
        private float enemyAvoidanceStrength;

        [SerializeField, Min(0), Tooltip("Strength of enemy rigidbody used when chasing or attacking enemies.")]
        private float enemyRigidbodyStrength;

        [Header("Visual")]
        [SerializeField, Tooltip("Primary renderer.")]
        private Renderer primaryRenderer;

        [SerializeField, Tooltip("Secondary renderer.")]
        private Renderer secondaryRenderer;

        [SerializeField, Tooltip("Name of movement blend parameter.")]
        private string movementParameter;

        [SerializeField, Tooltip("Name of the attack trigger parameter.")]
        private string attackParameter;

        [SerializeField, Tooltip("Name of the death trigger parameter.")]
        private string deathParameter;

        public Rigidbody Rigidbody { get; private set; }
        protected NavigationAgentRigidbody AgentRigidbody { get; private set; }
        protected StateMachine<State, Event, Creature> Brain { get; private set; }
        private Animator animator;
        private RigidbodyFollower rigidbodyFollower;
        private Material bulletMaterial;
        private int bulletLayer;
        private int speedHash;
        private int attackHash;
        private Color maximumHealthColor;
        private int health;
        private float next;
        private float nextHeal;
        private Vector3 lastKnowEnemyPosition;

        private bool IsAlive => health > 0;

        protected enum State
        {
            Idle,
            MovingToTarget,
            ChasingEnemy,
            TrackingEnemy,
            AttackingEnemy,
            Death,
            Fleeing
        }

        protected enum Event
        {
            EnemyEnteredInSight,
            EnemyEnteredInAttackRange,
            EnemyGotOutOfSight,
            EnemyGotOutOfAttackRange,
            EnemyLostTrack,
            ReachedToTarget,
            WasHurt,
            FullyHealed,
            Resurrect,
        }

        protected virtual void Awake()
        {
            health = maximumHealth;
            sightRadius *= sightRadius;
            attackMinimumDistance *= attackMinimumDistance;
            attackMaximumDistance *= attackMaximumDistance;

            Rigidbody rigidbody = GetComponent<Rigidbody>();
            rigidbodyFollower = new RigidbodyFollower(rigidbody);
            Rigidbody = rigidbody;

            NavigationAgentRigidbody agentRigidbody = GetComponent<NavigationAgentRigidbody>();
            agentRigidbody.RotateEvenWhenBraking = true;
            AgentRigidbody = agentRigidbody;

            animator = GetComponentInChildren<Animator>();
            speedHash = Animator.StringToHash(movementParameter);
            attackHash = Animator.StringToHash(attackParameter);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Start()
        {
            Brain = brainFactory.Create(this);

            enemyAvoidance.Radius = escapeRadius;
            enemyAvoidance.PredictionRadius = escapeRadius;
        }

        public void Initialize(int enemyLayer, Material primary, Material secondary, Material bulletMaterial, int bulletLayer)
        {
            enemyAvoidance.Layers = 1 << enemyLayer;
            primaryRenderer.material = new Material(primary);
            secondaryRenderer.material = secondary;
            this.bulletMaterial = bulletMaterial;
            this.bulletLayer = bulletLayer;
            maximumHealthColor = primary.color;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void Update()
        {
            Brain.Update();
            float linealSpeed = AgentRigidbody.LinealSpeed;
            float speed = Mathf.Min(Rigidbody.velocity.sqrMagnitude / (linealSpeed * linealSpeed), 1);
            if (Mathf.Approximately(speed, 0))
                speed = 0;
            animator.SetFloat(speedHash, speed);
        }

        private void AttackingEnemy_OnEntry() => AgentRigidbody.Brake = true;

        private void AttackingEnemy_OnEntry(Creature creature)
        {
            RigidbodyFollower rigidbodyFollower = this.rigidbodyFollower;
            rigidbodyFollower.Follow(creature);
            AgentRigidbody.SetSteeringBehaviour(rigidbodyFollower, enemyRigidbodyStrength);
        }

        private void AttackingEnemy_OnExit()
        {
            NavigationAgentRigidbody agentRigidbody = AgentRigidbody;
            agentRigidbody.Brake = false;
            RigidbodyFollower rigidbodyFollower = this.rigidbodyFollower;
            agentRigidbody.SetSteeringBehaviour(rigidbodyFollower, 0);
            rigidbodyFollower.Unfollow();
        }

        private void AttackingEnemy_OnUpdate()
        {
            float next = this.next;
            if (float.IsNaN(next))
                return;

            Creature enemy = GetCloserEnemyInSight();
            if (enemy is null)
                Brain.Fire(Event.EnemyGotOutOfSight);
            else
            {
                Vector3 enemyPosition = enemy.transform.position;
                float squaredDistance = (enemyPosition - transform.position).sqrMagnitude;
                if (squaredDistance > attackMaximumDistance)
                    Brain.With(enemy).Fire(Event.EnemyGotOutOfAttackRange);
                else
                {
                    RigidbodyFollower rigidbodyFollower = this.rigidbodyFollower;
                    if (rigidbodyFollower.Follow(enemy))
                        AgentRigidbody.SetSteeringBehaviour(rigidbodyFollower, enemyRigidbodyStrength);

                    if (next < Time.time)
                    {
                        this.next = float.NaN;
                        animator.SetTrigger(attackHash);
                    }
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity Animation.")]
        private void OnAttackAnimation_Middle()
        {
            Vector3 position = bulletSpawnPoint.position;
            Bullet bullet = Instantiate(bulletPrefab, position, Quaternion.LookRotation(lastKnowEnemyPosition - position));
            bullet.Initialize(gameObject, bulletMaterial, bulletLayer);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity Animation.")]
        private void OnAttackAnimation_End() => next = Time.time + fireCooldown;

        private void ChasingEnemy_OnEntry(Creature enemy)
        {
            RigidbodyFollower rigidbodyFollower = this.rigidbodyFollower;
            rigidbodyFollower.Follow(enemy);
            AgentRigidbody.SetSteeringBehaviour(rigidbodyFollower, enemyRigidbodyStrength);
        }

        private void ChasingEnemy_OnUpdate()
        {
            Heal();
            Creature enemy = GetCloserEnemyInSight();
            if (enemy is null)
                Brain.Fire(Event.EnemyGotOutOfSight);
            else
            {
                RigidbodyFollower rigidbodyFollower = this.rigidbodyFollower;
                if (rigidbodyFollower.Follow(enemy))
                    AgentRigidbody.SetSteeringBehaviour(rigidbodyFollower, enemyRigidbodyStrength);
                if ((transform.position - enemy.transform.position).sqrMagnitude < attackMinimumDistance)
                    Brain.With(enemy).Fire(Event.EnemyEnteredInAttackRange);
            }
        }

        private void ChasingEnemy_OnExit()
        {
            RigidbodyFollower rigidbodyFollower = this.rigidbodyFollower;
            AgentRigidbody.SetSteeringBehaviour(rigidbodyFollower, 0);
            rigidbodyFollower.Unfollow();
        }

        private void Death_OnEntry()
        {
            NavigationAgentRigidbody agentRigidbody = AgentRigidbody;
            agentRigidbody.UpdateMovement = false;
            agentRigidbody.UpdateRotation = false;
            animator.SetTrigger(deathParameter);
        }

        private void Death_OnExit()
        {
            animator.SetTrigger(deathParameter);

            Rigidbody rigidbody = Rigidbody;
            rigidbody.position = GameManager.GetSpawnPositionOfFactionLayer(gameObject.layer);
            rigidbody.velocity = default;
            rigidbody.rotation = Quaternion.identity;

            NavigationAgentRigidbody agentRigidbody = AgentRigidbody;
            agentRigidbody.UpdateMovement = true;
            agentRigidbody.UpdateRotation = true;

            health = maximumHealth;
            UpdateColors();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity Animation.")]
        private void DeathAnimation_End() => Brain.Fire(Event.Resurrect);

        private void Fleeing_OnEntry()
        {
            NavigationAgentRigidbody agentRigidbody = AgentRigidbody;
            agentRigidbody.Brake = false;
            agentRigidbody.SetSteeringBehaviour(enemyAvoidance, enemyAvoidanceStrength);
            next = float.PositiveInfinity;
        }

        private void Fleeing_OnExit() => AgentRigidbody.SetSteeringBehaviour(enemyAvoidance, 0);

        private void Fleeing_OnUpdate()
        {
            Heal();
            if (GetCloserEnemyInSight() == null)
            {
                if (next == float.PositiveInfinity)
                    next = Time.time + fleeingDuration;
                if (next < Time.time)
                    Brain.Fire(Event.EnemyGotOutOfSight);
            }
            else
                next = float.PositiveInfinity;
        }

        private void Idle_OnUpdate()
        {
            Heal();
            CheckEnemyInSight();
        }

        protected abstract void MovingToTarget_OnEntry();

        protected abstract void MovingToTarget_OnExit();

        protected virtual void MovingToTarget_OnUpdate() => Heal();

        private void TrackingEnemy_OnEntry()
        {
            Vector3 position = Rigidbody.position;
            Vector3 enemyPosition = lastKnowEnemyPosition;
            if (!Physics.Linecast(position, enemyPosition, blockSight))
            {
                Span<Vector3> path = stackalloc Vector3[] { enemyPosition };
                enemyFollower.SetPath(path);
            }
            else
                enemyFollower.SetDestination(enemyPosition);
        }

        private void TrackingEnemy_OnUpdate()
        {
            Heal();
            Creature enemy = GetCloserEnemyInSight();
            if (!(enemy is null))
                Brain.With(enemy).Fire(Event.EnemyEnteredInSight);
            else if (!enemyFollower.HasPath && !enemyFollower.IsCalculatingPath)
                Brain.Fire(Event.EnemyLostTrack);
        }

        private void TrackingEnemy_OnExit()
        {
            enemyFollower.Cancel();
            enemyFollower.Clear();
        }

        protected void CheckEnemyInSight()
        {
            Creature enemy = GetCloserEnemyInSight();
            if (!(enemy is null))
            {
                if ((transform.position - enemy.transform.position).sqrMagnitude < attackMinimumDistance)
                    Brain.With(enemy).Fire(Event.EnemyEnteredInAttackRange);
                else
                    Brain.With(enemy).Fire(Event.EnemyEnteredInSight);
            }
        }

        public Creature GetCloserEnemyInSight()
        {
            Creature[] creatures = GameManager.GetEnemiesOf(gameObject.layer);

            Vector3 center = transform.position + (Vector3.up * eyesOffset);

            Creature closest = null;
            float minSquaredDistance = float.PositiveInfinity;
            Vector3 closestPosition = default;
            for (int i = 0; i < creatures.Length; i++)
            {
                Creature creature = creatures[i];
                if (!creature.IsAlive)
                    continue;
                Vector3 creaturePosition = creature.Rigidbody.position;
                if (Physics.Linecast(center, creaturePosition, blockSight, QueryTriggerInteraction.Ignore))
                    continue;
                float squaredDistanceToCreature = (center - creaturePosition).sqrMagnitude;
                if (squaredDistanceToCreature < minSquaredDistance)
                {
                    minSquaredDistance = squaredDistanceToCreature;
                    closest = creature;
                    closestPosition = creaturePosition;
                }
            }

            if (closest != null)
                lastKnowEnemyPosition = closestPosition;

            return closest;
        }

        public void TakeDamage(int damage, Vector3 origin)
        {
            health -= damage;
            if (health < 0)
                health = 0;
            UpdateColors();
            nextHeal = Time.time + healingRate;
            Brain.With(origin).Fire(Event.WasHurt);
        }

        private void Heal()
        {
            if (health < maximumHealth)
            {
                float time = Time.time;
                if (nextHeal < time)
                {
                    nextHeal = time + healingRate;
                    if (++health == maximumHealth)
                        Brain.Fire(Event.FullyHealed);
                    UpdateColors();
                }
            }
            else
                Brain.Fire(Event.FullyHealed);
        }

        private void UpdateColors()
        {
            float factor = ((float)health / maximumHealth / 2) + .5f;
            primaryRenderer.material.color = new Color(
                maximumHealthColor.r * factor,
                maximumHealthColor.g * factor,
                maximumHealthColor.b * factor,
                maximumHealthColor.a
            );
        }

#if UNITY_EDITOR
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Used by Unity.")]
        private void OnValidate()
        {
            attackMaximumDistance = Mathf.Max(attackMaximumDistance, attackMinimumDistance);
            startFleeingHealth = Mathf.Min(startFleeingHealth, maximumHealth);
        }
#endif
    }
}