using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvalonStudios.Additions.Extensions
{
    public static class FindExtensions
    {
        /// <summary>
        /// Returns an array of active Components that correspond to the layer name.
        /// Returns empty array if no Component was found.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="name">The name of the layer to search Component for</param>
        /// <returns>Array of active Components.</returns>
        public static T[] FindComponentsWithLayer<T>(this GameObject obj, string name)
        {
            GameObject[] allObjsInScene = obj.FindGameObjectsWithLayer(name);
            List<T> components = new List<T>();

            foreach (GameObject gameObject in allObjsInScene)
            {
                T component;
                if (gameObject.TryGetComponent(out component))
                    components.Add(component);
            }

            if (components.Count == 0)
                return null;

            return components.ToArray();
        }

        /// <summary>
        /// Returns one active GameObject that correspond to the layer name. Returns null if not GameObject was found.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="name">The name of the layer to search GameObject for</param>
        /// <returns>Returns one active GameObject</returns>
        public static GameObject FindGameObjectWithLayer(this GameObject obj, string name)
        {
            GameObject[] allGameObjects = GameObject.FindObjectsOfType<GameObject>();

            foreach (GameObject gameObject in allGameObjects)
            {
                if (gameObject.layer == LayerMask.NameToLayer(name))
                    return gameObject;
            }

            return null;
        }

        /// <summary>
        /// Returns an array of active GameObjects that correspond to the layer name.
        /// Returns empty array if no <seealso cref="GameObject"/> was found.
        /// </summary>
        /// <param name="obj">The <seealso cref="GameObject"/>.</param>
        /// <param name="name">The name of the layer to search GameObjects for</param>
        /// <returns>Array of active GameObjects.</returns>
        public static GameObject[] FindGameObjectsWithLayer(this GameObject obj, string name)
        {
            GameObject[] gameObjectsArray = GameObject.FindObjectsOfType<GameObject>();
            List<GameObject> gameObjectsList = new List<GameObject>();

            foreach (GameObject gameObject in gameObjectsArray)
            {
                if (gameObject.layer == LayerMask.NameToLayer(name))
                    gameObjectsList.Add(gameObject);
            }

            if (gameObjectsList.Count == 0)
                return null;

            return gameObjectsList.ToArray();
        }

        /// <summary>
        /// Returns one active <seealso cref="Transform"/> of the Child that correspond to the layer name.
        /// Returns null if not <seealso cref="Transform"/> Child was found.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="name">The name of the layer to search the <seealso cref="Transform"/> Child for</param>
        /// <returns>Active <seealso cref="Transform"/> of the Child</returns>
        public static Transform FindTransformChildWithLayer(this Transform transform, string name)
        {
            foreach(Transform child in transform)
            {
                if (child.gameObject.layer == LayerMask.NameToLayer(name))
                    return child;
            }

            return null;
        }

        /// <summary>
        /// Returns an array of active <seealso cref="Transform"/> of the Childs that correspond to the layer name.
        /// Returns empty array if no <seealso cref="Transform"/> Child was found.
        /// </summary>
        /// <param name="obj">The <seealso cref="Transform"/>.</param>
        /// <param name="name">The name of the layer to search Transforms Child for</param>
        /// <returns>Array of active Transforms Child.</returns>
        public static Transform[] FindTransformsChildWithLayer(this Transform transform, string name)
        {
            List<Transform> transformsChildWithLayer = new List<Transform>();

            foreach (Transform child in transform)
            {
                if (child.gameObject.layer == LayerMask.NameToLayer(name))
                    transformsChildWithLayer.Add(child);
            }

            if (transformsChildWithLayer.Count == 0)
                return null;

            return transformsChildWithLayer.ToArray();
        }

        /// <summary>
        /// Returns the first active loaded object of Type type that is inside of the transform.
        /// </summary>
        /// <typeparam name="T">The generic type of object to find.</typeparam>
        /// <returns>
        /// This returns the Object that matches the specified type. It returns null if no
        /// Object matches the type.
        /// </returns>
        public static T FindTransformChildOfType<T>(this Transform transform) where T : Object
        {
            T childType;
            foreach (Transform child in transform)
            {
                if (child.TryGetComponent(out childType))
                    return childType;
            }
            return default;
        }

        /// <summary>
        /// Returns a list of all active loaded objects of Type type that is inside of the transform.
        /// </summary>
        /// <typeparam name="T">The generic type of object to find.</typeparam>
        /// <returns>
        /// The array of objects found matching the type specified.
        /// </returns>
        public static T[] FindTransformsChildOfType<T>(this Transform transform) where T : Object
        {
            List<T> ts = new List<T>();
            foreach (Transform child in transform)
            {
                T childType = child.GetComponent<T>();
                if (!childType.IsNull())
                    ts.Add(childType);
            }

            return ts.ToArray();
        }

        public static GameObject FindChildWithName(this GameObject obj, string name)
        {
            Transform transform = obj.transform;
            GameObject child = transform.Find(name).gameObject;

            return child == null ? null : child;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Find <seealso cref="SerializedProperty"/> by name.
        /// </summary>
        /// <param name="path">The path of the property.</param>
        /// <returns>
        /// This returns the <seealso cref="SerializedProperty"/> that matches with the specified path.
        /// </returns>
        public static SerializedProperty FindSerializedProperty(this SerializedObject serializedObject, string path)
        {
            SerializedProperty property = serializedObject.FindProperty(path);
            if (property.IsNull())
                property = serializedObject.FindProperty($"<{path}>k__BackingField");
            return property;
        }

        /// <summary>
        /// Retrieves the <seealso cref="SerializedProperty"/> at a relative path to the current property.
        /// </summary>
        /// <param name="path">The path of the property.</param>
        /// <returns>
        /// This returns the relative <seealso cref="SerializedProperty"/> that matches with the specified path.
        /// </returns>
        public static SerializedProperty FindSerializedPropertyRelative(this SerializedProperty serializedObject, string path)
        {
            SerializedProperty property = serializedObject.FindPropertyRelative(path);
            if (property.IsNull())
                property = serializedObject.FindPropertyRelative($"<{path}>k__BackingField");

            return property;
        }

        /// <summary>
        /// Find Auto <seealso cref="SerializedProperty"/> by name.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>
        /// This returns the Auto <seealso cref="SerializedProperty"/> that matches with the specified name of the property.
        /// </returns>
        public static SerializedProperty FindPropertyByAutoSerializePropertyName(this SerializedObject serializedObject, string propertyName) =>
            serializedObject.FindProperty($"<{propertyName}>k__BackingField");

        /// <summary>
        /// Retrieves the Auto <seealso cref="SerializedProperty"/> at a relative path to the current property.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns>
        /// This returns the relative Auto <seealso cref="SerializedProperty"/> that matches with the specified name of the property.
        /// </returns>
        public static SerializedProperty FindPropertyRelativeByAutoSerializePropertyName(this SerializedProperty serializedObject, string propertyName) =>
            serializedObject.FindPropertyRelative($"<{propertyName}>k__BackingField");
#endif
    }
}
