using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MToolKit.Runtime.Persistence.Interfaces
{
  /// <summary>Stable identity and canonical file location for the currently selected save.</summary>
  public readonly struct ES3SaveContext : IEquatable<ES3SaveContext>
  {
    public Guid SaveId { get; }
    public string SaveFilePath { get; }

    public ES3SaveContext(Guid saveId, string saveFilePath)
    {
      if (saveId == Guid.Empty) throw new ArgumentException("A persistent save ID is required.", nameof(saveId));
      if (string.IsNullOrWhiteSpace(saveFilePath)) throw new ArgumentException("A save file path is required.", nameof(saveFilePath));
      SaveId = saveId;
      SaveFilePath = Path.GetFullPath(saveFilePath);
    }

    public bool Equals(ES3SaveContext other) => SaveId == other.SaveId
      && string.Equals(SaveFilePath, other.SaveFilePath, StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object obj) => obj is ES3SaveContext other && Equals(other);
    public override int GetHashCode() => unchecked((SaveId.GetHashCode() * 397)
      ^ StringComparer.OrdinalIgnoreCase.GetHashCode(SaveFilePath ?? string.Empty));
  }

  /// <summary>Evidence that the current ES3 save file was stored and read back after the batch completed.</summary>
  public sealed class ES3FlushReceipt
  {
    public static ES3FlushReceipt ReadBackAndFlush(ES3SaveContext context)
    {
      using (var stream = new FileStream(context.SaveFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
      using (var sha256 = SHA256.Create())
      {
        stream.Flush(flushToDisk: true);
        stream.Position = 0;
        string digest = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        return new ES3FlushReceipt(context, digest, DateTime.UtcNow);
      }
    }

    public ES3SaveContext Context { get; }
    public string FileSha256 { get; }
    public DateTime CompletedUtc { get; }

    public ES3FlushReceipt(ES3SaveContext context, string fileSha256, DateTime completedUtc)
    {
      if (context.SaveId == Guid.Empty) throw new ArgumentException("A valid save context is required.", nameof(context));
      if (string.IsNullOrWhiteSpace(fileSha256) || fileSha256.Length != 64)
        throw new ArgumentException("A SHA-256 file digest is required.", nameof(fileSha256));
      Context = context;
      FileSha256 = fileSha256.ToLowerInvariant();
      CompletedUtc = completedUtc.Kind == DateTimeKind.Utc ? completedUtc : completedUtc.ToUniversalTime();
    }
  }

  /// <summary>Creates and reads a durable GUID identity adjacent to a save file.</summary>
  public static class ES3SaveContextIdentityStore
  {
    private const int MoveFileWriteThrough = 0x00000008;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "MoveFileExW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);

    public static ES3SaveContext GetOrCreate(string saveFilePath)
    {
      if (string.IsNullOrWhiteSpace(saveFilePath)) throw new ArgumentException("A save file path is required.", nameof(saveFilePath));
      string fullPath = Path.GetFullPath(saveFilePath);
      string identityPath = fullPath + ".saveid";
      if (File.Exists(identityPath)) return new ES3SaveContext(ReadId(identityPath), fullPath);

      string directory = Path.GetDirectoryName(fullPath);
      if (string.IsNullOrEmpty(directory)) throw new ArgumentException("The save path must have a parent directory.", nameof(saveFilePath));
      Directory.CreateDirectory(directory);
      string pendingPath = identityPath + "." + Guid.NewGuid().ToString("N") + ".pending";
      Guid newId = Guid.NewGuid();
      try
      {
        byte[] bytes = Encoding.ASCII.GetBytes(newId.ToString("D"));
        using (var stream = new FileStream(pendingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
          256, FileOptions.WriteThrough))
        {
          stream.Write(bytes, 0, bytes.Length);
          stream.Flush(flushToDisk: true);
        }

        Publish(pendingPath, identityPath);
        return new ES3SaveContext(newId, fullPath);
      }
      catch (IOException) when (File.Exists(identityPath))
      {
        if (File.Exists(pendingPath)) File.Delete(pendingPath);
        return new ES3SaveContext(ReadId(identityPath), fullPath);
      }
      catch
      {
        if (File.Exists(pendingPath)) File.Delete(pendingPath);
        throw;
      }
    }

    private static Guid ReadId(string path)
    {
      string value = File.ReadAllText(path, Encoding.ASCII).Trim();
      if (!Guid.TryParseExact(value, "D", out Guid id) || id == Guid.Empty)
        throw new InvalidDataException("The persistent save identity file is malformed: " + path);
      return id;
    }

    private static void Publish(string pendingPath, string identityPath)
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        if (MoveFileEx(pendingPath, identityPath, MoveFileWriteThrough)) return;
        int error = Marshal.GetLastWin32Error();
        if (error == 80 || error == 183)
          throw new IOException("A persistent save identity already exists.", new Win32Exception(error));
        throw new IOException("The persistent save identity could not be durably published.", new Win32Exception(error));
      }
      File.Move(pendingPath, identityPath);
    }
  }
}
