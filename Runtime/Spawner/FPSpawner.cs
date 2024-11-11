namespace FuzzPhyte.Utility
{
    using UnityEngine;
    /// <summary>
    /// base class for spawning an object from a known location
    /// </summary>
    public abstract class FPSpawner : MonoBehaviour
    {
        [SerializeField] protected Transform spawnPosition; // Position to spawn objects

        protected abstract GameObject GetNextPrefab(); // Abstract method to be implemented by subclasses

        public virtual GameObject Spawn()
        {
            GameObject prefab = GetNextPrefab();
            if (prefab == null) return null;

            GameObject spawnedObject = Instantiate(prefab, spawnPosition.position, Quaternion.identity);
            return spawnedObject;
        }

        protected virtual void AdjustSpawnPositionOffset(Vector3 offsetAmt)
        {
            // Adjust spawn position to prepare for the next spawn
            spawnPosition.position += offsetAmt;
        }
        protected virtual void AdjustSpawnPosition(Vector3 newPosition)
        {
            spawnPosition.position = newPosition;
        }
    }
}
