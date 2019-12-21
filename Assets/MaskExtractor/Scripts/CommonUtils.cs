using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace m039.MaskExtractor
{

    public static class CommonUtils
    {
        /// <summary>
        /// Clone the texture preserving the aspect ratio.
        /// </summary>
        static public Texture2D Clone(Texture2D texture, int height = -1)
        {
            Texture2D result;

            // Rescale the texture into another one.

            Texture2D Resize(Texture2D inputTexture, int targetW, int targetH)
            {
                RenderTexture rt = new RenderTexture(targetW, targetH, 24);
                RenderTexture.active = rt;
                Graphics.Blit(inputTexture, rt);
                Texture2D r = new Texture2D(targetW, targetH, TextureFormat.ARGB32, false);
                r.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
                r.Apply();
                return r;
            }

            float aspect = (float)texture.width / texture.height;
            int h = height <= 0 ? texture.height : height;
            int w = height <= 0 ? texture.width : (int)(aspect * h);

            result = Resize(texture, w, h);

            result.name = texture.name;
            result.Apply();

            return result;
        }

    }

}