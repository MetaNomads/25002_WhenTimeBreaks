using UnityEngine;
using Unity.Collections;
using System.Runtime.CompilerServices;

namespace NGS.MeshFusionPro
{
    public static class UnityAPI
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] FindObjectsOfType<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER

            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            
#else
            
            return Object.FindObjectsOfType<T>();
            
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T FindObjectOfType<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER

            return Object.FindAnyObjectByType<T>();

#else

            return Object.FindObjectOfType<T>();
            
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<T> NativeListToArray<T>(this NativeList<T> list) where T: unmanaged
        {
            #if COLLECTIONS_2_0_OR_HIGHER

            return list.AsArray();

            #else

            return list;

            #endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetRigidbodyVelocity(Rigidbody rigidbody)
        {
            #if UNITY_6000_0_OR_NEWER

            return rigidbody.linearVelocity;

            #else

            return rigidbody.velocity;

            #endif
        }
    }
}
