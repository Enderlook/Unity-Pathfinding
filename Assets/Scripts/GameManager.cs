using Enderlook.Unity.Toolset.Attributes;

using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    [DefaultExecutionOrder(100)]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField, Tooltip("Prefab of leader.")]
        private Leader leaderPrefab;

        [SerializeField, Tooltip("Minion prefab.")]
        private Minion minionPrefab;

        [SerializeField, DrawVectorRelativeToTransform(true), Tooltip("Middle point of battlefield.")]
        private Vector3 middlePoint;

        [Header("Alfa")]
        [SerializeField, Layer, Tooltip("Layer of faction Alfa.")]
        private int alfaLayer;

        [SerializeField, Layer, Tooltip("Layer of faction Alfa's bullets.")]
        private int alfaBulletLayer;

        [SerializeField, Tooltip("Determines the count of minions in faction Alfa.")]
        private InputField alfaCount;

        [SerializeField, DrawVectorRelativeToTransform(true), Tooltip("Spawn point of faction Alfa.")]
        private Vector3 alfaSpawn;

        [SerializeField, Tooltip("Primary material of faction Alfa.")]
        private Material alfaPrimaryMaterial;

        [SerializeField, Tooltip("Secondary material of faction Alfa.")]
        private Material alfaSecondaryMaterial;

        [SerializeField, Tooltip("Bullet material of faction Alfa.")]
        private Material alfaBulletMaterial;

        [Header("Beta")]
        [SerializeField, Layer, Tooltip("Layer of faction Beta.")]
        private int betaLayer;

        [SerializeField, Layer, Tooltip("Layer of faction Beta's bullets.")]
        private int betaBulletLayer;

        [SerializeField, Tooltip("Determines the count of minions in faction Beta.")]
        private InputField betaCount;

        [SerializeField, DrawVectorRelativeToTransform(true), Tooltip("Spawn point of faction Beta.")]
        private Vector3 betaSpawn;

        [SerializeField, Tooltip("Primary material of faction Beta.")]
        private Material betaPrimaryMaterial;

        [SerializeField, Tooltip("Secondary material of faction Beta.")]
        private Material betaSecondaryMaterial;

        [SerializeField, Tooltip("Bullet material of faction Beta.")]
        private Material betaBulletMaterial;

        private const int spawnRadius = 5;

        private Creature[] alfaCreatures;
        private Creature[] betaCreatures;

        private static GameManager singlenton;

        public static Vector3 MiddlePoint => singlenton.middlePoint;

        private void Awake()
        {
            if (singlenton != null)
            {
                Debug.LogError($"Can't have more than 1 {nameof(GameManager)}.");
                Destroy(this);
                return;
            }
            else
                singlenton = this;

            Create();
        }

        public void Create()
        {
            if (!(alfaCreatures is null))
            {
                foreach (Creature creature in alfaCreatures)
                    Destroy(creature.gameObject);
            }
            alfaCreatures = null;

            if (!(betaCreatures is null))
            {
                foreach (Creature creature in betaCreatures)
                    Destroy(creature.gameObject);
            }
            betaCreatures = null;

            int alfaCount_ = string.IsNullOrEmpty(alfaCount.text) ? 0 : int.Parse(alfaCount.text);
            int betaCount_ = string.IsNullOrEmpty(betaCount.text) ? 0 : int.Parse(betaCount.text);

            CreateArmy(ref alfaCreatures, alfaCount_, alfaLayer, alfaSpawn, betaLayer, alfaPrimaryMaterial, alfaSecondaryMaterial, alfaBulletMaterial, alfaBulletLayer);
            CreateArmy(ref betaCreatures, betaCount_, betaLayer, betaSpawn, alfaLayer, betaPrimaryMaterial, betaSecondaryMaterial, betaBulletMaterial, betaBulletLayer);

            void CreateArmy(ref Creature[] creatures, int count, int layer, Vector3 spawnPoint, int enemyLayer, Material primary, Material secondary, Material bulletMaterial, int bulletLayer)
            {
                Vector2 random = Random.insideUnitCircle * spawnRadius;
                creatures = new Creature[1 + count];
                Vector3 position = transform.position;
                Leader leader = Instantiate(leaderPrefab, spawnPoint + position + new Vector3(random.x, 0, random.y), Quaternion.identity);
                SetLayer(leader.gameObject);
                leader.Initialize(enemyLayer, primary, secondary, bulletMaterial, bulletLayer);
                creatures[0] = leader;

                for (int i = 0; i < count; i++)
                {
                    random = Random.insideUnitCircle * spawnRadius;
                    Minion minion = Instantiate(minionPrefab, spawnPoint + position + new Vector3(random.x, 0, random.y), Quaternion.identity);
                    SetLayer(minion.gameObject);
                    minion.Initialize(enemyLayer, primary, secondary, bulletMaterial, bulletLayer);
                    minion.SetLeader(leader);
                    creatures[1 + i] = minion;
                }

                void SetLayer(GameObject gameObject)
                {
                    gameObject.layer = layer;
                    foreach (Transform transform in gameObject.transform)
                        SetLayer(transform.gameObject);
                }
            }
        }

        public static Creature[] GetEnemiesOf(int faction)
        {
            GameManager singlenton = GameManager.singlenton;
            if (faction == singlenton.alfaLayer)
                return singlenton.betaCreatures;
            else if (faction == singlenton.betaLayer)
                return singlenton.alfaCreatures;
            else
            {
                Debug.Assert(false, "Impossible state.");
                return null;
            }
        }

        public static Vector3 GetSpawnPositionOfFactionLayer(int faction)
        {
            GameManager singlenton = GameManager.singlenton;
            Vector2 random = Random.insideUnitCircle * spawnRadius;
            Vector3 offset = singlenton.transform.position + new Vector3(random.x, 0, random.y);
            if (faction == singlenton.alfaLayer)
                return singlenton.alfaSpawn + offset;
            else if (faction == singlenton.betaLayer)
                return singlenton.betaSpawn + offset;
            else
            {
                Debug.Assert(false, "Impossible state.");
                return default;
            }
        }

        public void Close() => Application.Quit();

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(alfaSpawn + transform.position, spawnRadius);
            Gizmos.DrawWireSphere(betaSpawn + transform.position, spawnRadius);
        }
#endif
    }
}