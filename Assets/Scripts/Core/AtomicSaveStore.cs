using System;
using System.IO;
using System.Text;

/// <summary>Same-directory atomic commits. A failed commit never deletes the previous save.</summary>
public static class AtomicSaveStore
{
    private static readonly object gate = new object();

    public static void Write(string path, string contents, Func<string, bool> valid)
    {
        if (valid == null || !valid(contents)) throw new InvalidDataException("Invalid save data.");
        lock (gate)
        {
            string temporary = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(contents);
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                if (File.Exists(path))
                {
                    string ignored;
                    // Never overwrite a good backup with a corrupt primary.
                    string backup = TryReadOne(path, valid, out ignored) ? path + ".bak" : null;
                    File.Replace(temporary, path, backup);
                }
                else File.Move(temporary, path);
            }
            finally
            {
                // Cleanup failure must not change the outcome of an atomic commit.
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    public static bool TryRead(string path, Func<string, bool> valid, out string contents, out bool recovered)
    {
        recovered = false;
        if (TryReadOne(path, valid, out contents)) return true;
        recovered = TryReadOne(path + ".bak", valid, out contents);
        return recovered;
    }

    private static bool TryReadOne(string path, Func<string, bool> valid, out string contents)
    {
        contents = null;
        try
        {
            if (valid == null || !File.Exists(path)) return false;
            string candidate = File.ReadAllText(path);
            if (!valid(candidate)) return false;
            contents = candidate;
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public static void Delete(string path)
    {
        lock (gate)
            foreach (string file in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(file)) File.Delete(file);
    }
}
