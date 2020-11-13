using AvalonStudios.Additions.Extensions;

using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEditor;

namespace AvalonStudios.Additions.Utils.Assets
{
    public sealed class AssetDatabaseHandle
    {
        /// <summary>
        /// Returns a list of all assets of Type type.
        /// </summary>
        /// <typeparam name="T">The type of asset to find.</typeparam>
        /// <param name="path">The path where the assets is.</param>
        /// <returns>
        /// The list of assets found matching the type specified.
        /// </returns>
        public static List<T> FindAssetsOfType<T>(string path = "Assets") where T : Object
        {
            List<T> assets = new List<T>();
            string tempName = typeof(T).ToString().Replace("UnityEngine.", "");
            if (tempName.Length == typeof(T).ToString().Length)
                tempName = typeof(T).ToString().Replace("UnityEditor.", "");
            string[] guids = AssetDatabase.FindAssets($"t: {tempName}", new[] { path });
            string[] assetPaths = GUIDsToAssetPaths(guids);
            foreach (string assetPath in assetPaths)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (!asset.IsNull())
                    assets.Add(asset);
            }
            return assets;
        }

        /// <summary>
        /// Returns a asset of Type type with a specified name.
        /// </summary>
        /// <typeparam name="T">The type of asset to find.</typeparam>
        /// <param name="name">The name of the asset to find.</param>
        /// <param name="path">The path where the assets is.</param>
        /// <returns>
        /// A asset found matching the type specified and the matching name.
        /// </returns>
        public static T FindAssetByName<T>(string name, string path = "Assets") where T : Object
        {
            T asset = null;
            string tempNameType = typeof(T).ToString().Replace("UnityEngine.", "");
            if (tempNameType.Length == typeof(T).ToString().Length)
                tempNameType = typeof(T).ToString().Replace("UnityEditor.", "");

            string[] guids = AssetDatabase.FindAssets($"{name}, t: {tempNameType}", new[] { path });
            string[] assetPaths = GUIDsToAssetPaths(guids);

            asset = AssetDatabase.LoadAssetAtPath<T>(assetPaths.Single(x => AssetDatabase.LoadAssetAtPath<T>(x).name == name));
            return asset.IsNull() ? null : asset;
        }

        /// <summary>
        /// Translate an array of GUIDs to its current asset paths.
        /// </summary>
        /// <param name="guids"></param>
        /// <returns></returns>
        public static string[] GUIDsToAssetPaths(string[] guids)
        {
            List<string> assetsPath = new List<string>();

            foreach (string guid in guids)
                assetsPath.Add(AssetDatabase.GUIDToAssetPath(guid));

            return assetsPath.ToArray();
        }

        //public static T GetAssetByName<T>(string name) where T : Object
        //{
        //    T asset = null;
        //    string[] assetPaths = AssetDatabase.GetAllAssetPaths();

        //    string assetPath = assetPaths.Single(x => x.EndsWith(name));

        //    if (!assetPath.IsNull())
        //        asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

        //    return asset;
        //}
    }
}
