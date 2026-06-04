// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility
{
    using UnityEngine;
    /// <summary>
    /// base class for spawning an object from a known location
    /// </summary>
    public abstract class FPSpawner : MonoBehaviour
    {
        [SerializeField] protected Transform spawnPosition; // Position to spawn objects
        [SerializeField] protected bool AlignRotation = false;
        protected abstract GameObject GetNextPrefab(); // Abstract method to be implemented by subclasses

        public virtual GameObject Spawn()
        {
            GameObject prefab = GetNextPrefab();
            if (prefab == null) return null;
            Quaternion quaternion = AlignRotation ? spawnPosition.transform.rotation : Quaternion.identity;
            
            GameObject spawnedObject = Instantiate(prefab, spawnPosition.position, quaternion);
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
