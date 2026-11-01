using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.SceneMark
{
    public class TextureUtils
    {
        /// <summary>
        /// Create a Texture2D from a RenderTexture
        /// </summary>
        /// <param name="renderTexture"></param>
        /// <returns></returns>
        public static Texture2D CreateTexture2D(RenderTexture renderTexture)
        {
            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);

            RenderTexture prevRT = RenderTexture.active;
            {
                RenderTexture.active = renderTexture;

                texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture.Apply();
            }
            RenderTexture.active = prevRT;

            return texture;
        }

        /// <summary>
        /// Create a copy of a Texture2D.
        /// The texture will optionally be forced to be readable.
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        public static Texture2D CopyTexture2D(Texture2D texture, bool forceReadable)
        {
            if (forceReadable)
                return CopyTextureReadable( texture);
            else
                return CopyTexture( texture);

        }

        /// <summary>
        /// Copy texture. If the source isn't readable, the target won't be either
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        private static Texture2D CopyTexture(Texture2D texture)
        {
            if (texture == null)
                return null;

            Texture2D copyTexture = new Texture2D(texture.width, texture.height, texture.format, false);
            copyTexture.SetPixels(texture.GetPixels());
            copyTexture.Apply();

            return copyTexture;
        }

        /// <summary>
        /// Copy texture. Make it readable.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private static Texture2D CopyTextureReadable(Texture2D source)
        {
            Texture2D readableTexture;

            RenderTexture renderTexture = RenderTexture.GetTemporary(
                        source.width,
                        source.height);
            {
                Graphics.Blit(source, renderTexture);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                {
                    readableTexture = new Texture2D(source.width, source.height);
                    readableTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                    readableTexture.Apply();
                }
                RenderTexture.active = previous;
            }
            RenderTexture.ReleaseTemporary(renderTexture);

            return readableTexture;
        }
    }
}