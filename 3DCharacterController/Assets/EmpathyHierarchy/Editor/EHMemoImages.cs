using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Pictures in memos. Stored as PNG next to the memos (ProjectSettings/EmpathyHierarchy/MemoImages, shared with
    /// the team, outside Assets so Unity never imports them), scaled down on the way in, loaded once and cached.
    /// A memo refers to a picture with a markdown line: ![](file.png)
    /// </summary>
    static class EHMemoImages
    {
        public const string Folder = "ProjectSettings/EmpathyHierarchy/MemoImages";
        const int MaxSide = 1600; // bigger pictures are scaled down (keeps the repo small)

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, byte[]> Trash = new Dictionary<string, byte[]>(); // deleted this session (Undo)
        const int MaxTrash = 30;
        static readonly Regex Ref = new Regex(@"!\[[^\]]*\]\(([^)\s]+)\)");

        public static string PathOf(string id) => Path.Combine(Folder, id);

        public static bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        /// <summary>Loaded picture for <paramref name="id"/> (cached), or null if the file is gone.</summary>
        public static Texture2D Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Cache.TryGetValue(id, out var tex) && tex != null) return tex;
            var path = PathOf(id);
            if (!File.Exists(path))
            {
                // removed earlier and brought back (memo Undo, or a restored memo): take it back from the bin
                try
                {
                    var binned = Path.Combine(Bin, id);
                    Directory.CreateDirectory(Folder);
                    if (File.Exists(binned)) File.Move(binned, path);
                    else if (Trash.TryGetValue(id, out var bytes)) { File.WriteAllBytes(path, bytes); Trash.Remove(id); }
                    else return null;
                }
                catch { return null; }
            }
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            if (!tex.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(tex); return null; }
            tex.wrapMode = TextureWrapMode.Clamp;
            Cache[id] = tex;
            return tex;
        }

        /// <summary>Saves a picture (scaled down if needed) and returns its id. The texture is consumed.</summary>
        public static string Save(Texture2D tex)
        {
            if (tex == null) return null;
            var small = Downscale(tex);
            if (small != tex) UnityEngine.Object.DestroyImmediate(tex);
            Directory.CreateDirectory(Folder);
            string id = Guid.NewGuid().ToString("N").Substring(0, 12) + ".png";
            File.WriteAllBytes(PathOf(id), small.EncodeToPNG());
            small.hideFlags = HideFlags.HideAndDontSave;
            Cache[id] = small;
            return id;
        }

        /// <summary>Copies a picture file into the memo folder (scaled down if needed) and returns its id.</summary>
        public static string Import(string file)
        {
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(file))) { UnityEngine.Object.DestroyImmediate(tex); return null; }
                return Save(tex);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Empathy Hierarchy] Could not add the picture: " + e.Message);
                return null;
            }
        }

        public static void Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Cache.TryGetValue(id, out var tex) && tex != null) UnityEngine.Object.DestroyImmediate(tex);
            Cache.Remove(id);
            try
            {
                if (!File.Exists(PathOf(id))) return;
                // gone from the project, but kept in a local bin (Library, not shared) for 30 days:
                // Undo, a restored memo backup, or a memo store that failed to load can still get it back
                Directory.CreateDirectory(Bin);
                var binned = Path.Combine(Bin, id);
                if (File.Exists(binned)) File.Delete(binned);
                File.Move(PathOf(id), binned);
                File.SetLastWriteTime(binned, DateTime.Now); // the 30 days count from removal
            }
            catch
            {
                // could not move: keep it in memory for this session, then delete
                try
                {
                    if (Trash.Count >= MaxTrash) Trash.Clear();
                    Trash[id] = File.ReadAllBytes(PathOf(id));
                    File.Delete(PathOf(id));
                }
                catch { /* in use or read-only: the sweep tries again later */ }
            }
        }

        /// <summary>Removes picture files no memo refers to any more (e.g. a deleted page or memo).</summary>
        public static void Sweep(IEnumerable<string> memoTexts)
        {
            PurgeBin();
            if (!Directory.Exists(Folder)) return;
            var used = new HashSet<string>();
            int texts = 0;
            foreach (var t in memoTexts)
            {
                if (!string.IsNullOrEmpty(t)) texts++;
                if (string.IsNullOrEmpty(t) || t.IndexOf("![", StringComparison.Ordinal) < 0) continue;
                foreach (Match m in Ref.Matches(t)) used.Add(m.Groups[1].Value);
            }
            // no memo text at all while pictures exist: more likely the memos failed to load than all deleted
            if (texts == 0) return;
            foreach (var file in Directory.GetFiles(Folder))
            {
                var id = Path.GetFileName(file);
                if (!used.Contains(id)) Delete(id);
            }
        }

        /// <summary>Local bin for removed pictures (Library: per machine, never versioned).</summary>
        static readonly string Bin = "Library/EmpathyHierarchy/DeletedImages";
        const int BinDays = 30;
        static bool _purged;

        static void PurgeBin()
        {
            if (_purged || !Directory.Exists(Bin)) return;
            _purged = true;
            var limit = DateTime.Now.AddDays(-BinDays);
            foreach (var f in Directory.GetFiles(Bin))
                try { if (File.GetLastWriteTime(f) < limit) File.Delete(f); } catch { }
        }

        static Texture2D Downscale(Texture2D src)
        {
            int w = src.width, h = src.height;
            if (w <= MaxSide && h <= MaxSide) return src;
            float k = (float)MaxSide / Mathf.Max(w, h);
            int nw = Mathf.Max(1, Mathf.RoundToInt(w * k)), nh = Mathf.Max(1, Mathf.RoundToInt(h * k));
            var rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var dst = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
            dst.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }

        // ================================================================== clipboard (Unity's clipboard is text only)

        /// <summary>A picture copied to the system clipboard (screenshot, Snipping Tool…), or null.</summary>
        public static Texture2D ReadClipboard()
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor) return ReadWindows();
                if (Application.platform == RuntimePlatform.OSXEditor) return ReadMac();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Empathy Hierarchy] Could not read the picture from the clipboard: " + e.Message);
            }
            return null;
        }

        /// <summary>Puts a memo picture on the system clipboard (other apps, or Ctrl+V into another memo).</summary>
        public static bool CopyToClipboard(string id)
        {
            try
            {
                var path = PathOf(id);
                if (!File.Exists(path)) return false;
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    var tex = Load(id);
                    return tex != null && WriteWindows(tex, File.ReadAllBytes(path));
                }
                if (Application.platform == RuntimePlatform.OSXEditor) return WriteMac(Path.GetFullPath(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Empathy Hierarchy] Could not copy the picture: " + e.Message);
            }
            return false;
        }

        const uint CF_DIB = 8;
        const uint GMEM_MOVEABLE = 0x0002;
        [DllImport("user32.dll")] static extern bool EmptyClipboard();
        [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint format, IntPtr mem);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterClipboardFormat(string name);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr mem);

        /// <summary>Windows: a 32 bit bitmap for every app, plus the PNG itself for apps that keep transparency.</summary>
        static bool WriteWindows(Texture2D tex, byte[] png)
        {
            int w = tex.width, h = tex.height;
            var pixels = tex.GetPixels32(); // bottom-up rows, same as a bitmap
            var dib = new byte[40 + w * h * 4];
            BitConverter.GetBytes(40).CopyTo(dib, 0);
            BitConverter.GetBytes(w).CopyTo(dib, 4);
            BitConverter.GetBytes(h).CopyTo(dib, 8);
            BitConverter.GetBytes((short)1).CopyTo(dib, 12);
            BitConverter.GetBytes((short)32).CopyTo(dib, 14);
            BitConverter.GetBytes(w * h * 4).CopyTo(dib, 20);
            for (int i = 0, o = 40; i < pixels.Length; i++, o += 4)
            {
                var c = pixels[i];
                dib[o] = c.b; dib[o + 1] = c.g; dib[o + 2] = c.r; dib[o + 3] = c.a;
            }
            if (!OpenClipboard(IntPtr.Zero)) return false;
            try
            {
                EmptyClipboard();
                bool ok = Put(CF_DIB, dib);
                uint pngFormat = RegisterClipboardFormat("PNG");
                if (pngFormat != 0) Put(pngFormat, png);
                return ok;
            }
            finally { CloseClipboard(); }
        }

        /// <summary>Hands one block of bytes to the open clipboard (which then owns the memory).</summary>
        static bool Put(uint format, byte[] bytes)
        {
            var mem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)(ulong)bytes.Length);
            if (mem == IntPtr.Zero) return false;
            var ptr = GlobalLock(mem);
            if (ptr == IntPtr.Zero) { GlobalFree(mem); return false; }
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            GlobalUnlock(mem);
            if (SetClipboardData(format, mem) != IntPtr.Zero) return true;
            GlobalFree(mem);
            return false;
        }

        /// <summary>macOS: the PNG file onto the clipboard through osascript.</summary>
        static bool WriteMac(string fullPath)
        {
            string script = "set the clipboard to (read (POSIX file \"" + fullPath + "\") as «class PNGf»)";
            var psi = new System.Diagnostics.ProcessStartInfo("osascript", "-e \"" + script.Replace("\"", "\\\"") + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                p.WaitForExit(3000);
                return p.HasExited && p.ExitCode == 0;
            }
        }

        [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("user32.dll", SetLastError = true)] static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll")] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint format);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr mem);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr mem);
        [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr mem);

        /// <summary>Windows: the clipboard's device-independent bitmap (24 / 32 bit) as a texture.</summary>
        static Texture2D ReadWindows()
        {
            if (!IsClipboardFormatAvailable(CF_DIB)) return null;
            if (!OpenClipboard(IntPtr.Zero)) return null;
            byte[] data;
            try
            {
                var handle = GetClipboardData(CF_DIB);
                if (handle == IntPtr.Zero) return null;
                var ptr = GlobalLock(handle);
                if (ptr == IntPtr.Zero) return null;
                try
                {
                    int size = (int)(ulong)GlobalSize(handle);
                    data = new byte[size];
                    Marshal.Copy(ptr, data, 0, size);
                }
                finally { GlobalUnlock(handle); }
            }
            finally { CloseClipboard(); }

            int headerSize = BitConverter.ToInt32(data, 0);
            int width = BitConverter.ToInt32(data, 4);
            int height = BitConverter.ToInt32(data, 8);
            int bpp = BitConverter.ToInt16(data, 14);
            int compression = BitConverter.ToInt32(data, 16);
            int colorsUsed = BitConverter.ToInt32(data, 32);
            if ((bpp != 24 && bpp != 32) || width <= 0 || height == 0) return null;
            bool bottomUp = height > 0;
            height = Math.Abs(height);
            int offset = headerSize + (compression == 3 && headerSize == 40 ? 12 : 0) + colorsUsed * 4;
            int stride = ((width * bpp + 31) / 32) * 4;
            if (offset + stride * height > data.Length) return null;

            var pixels = new Color32[width * height];
            bool anyAlpha = false;
            int step = bpp / 8;
            for (int y = 0; y < height; y++)
            {
                int row = offset + y * stride;
                int ty = bottomUp ? y : height - 1 - y; // Unity rows go bottom-up too
                for (int x = 0; x < width; x++)
                {
                    int i = row + x * step;
                    byte a = bpp == 32 ? data[i + 3] : (byte)255;
                    if (a != 0) anyAlpha = true;
                    pixels[ty * width + x] = new Color32(data[i + 2], data[i + 1], data[i], a);
                }
            }
            if (!anyAlpha) for (int i = 0; i < pixels.Length; i++) pixels[i].a = 255; // 32 bit without alpha
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>macOS: the clipboard as PNG through osascript.</summary>
        static Texture2D ReadMac()
        {
            var psi = new System.Diagnostics.ProcessStartInfo("osascript", "-e \"the clipboard as «class PNGf»\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);
                var m = Regex.Match(output, "«data PNGf([0-9A-Fa-f]+)»");
                if (!m.Success) return null;
                string hex = m.Groups[1].Value;
                var bytes = new byte[hex.Length / 2];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) { UnityEngine.Object.DestroyImmediate(tex); return null; }
                return tex;
            }
        }
    }
}
