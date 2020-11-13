using UnityEngine;

namespace AvalonStudios.Additions.Extensions
{
    public static class LayerMaskExtensions
    {
        /// <summary>
        /// Convert a <see cref="LayerMask"/> value to it's true layer value.<br/>
        /// This should only be used if the <paramref name="layerMask"/> has a single layer.
        /// </summary>
        /// <param name="layerMask"><see cref="LayerMask"/></param>
        /// <returns>int layer number.</returns>
        public static int ToLayer(this LayerMask layerMask)
        {
            int value = layerMask.value;
            int exp = 2;
            int b = 1;
            int result = 0;
            while (result != value)
            {
                result = (int)Mathf.Pow(exp, b);
                b += result != value ? 1 : 0;
            }

            return b;
        }

        /// <summary>
        /// Returns true if the <seealso cref="LayerMask"/> equals the layer of the <seealso cref="GameObject"/>.
        /// Returns false if not <seealso cref="LayerMask"/> was found.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="layerMask">The <seealso cref="LayerMask"/> to compare with the layer of the <seealso cref="GameObject"/></param>
        /// <returns>A <seealso cref="bool"/> result.</returns>
        public static bool MatchLayer(this GameObject gameObject, LayerMask layerMask)
        {
            for (int i = 0; i < 32; i++)
            {
                if (layerMask == (layerMask | (1 << i)))
                {
                    if (gameObject.layer == i)
                        return true;
                }
            }

            return false;
        }
    }
}
