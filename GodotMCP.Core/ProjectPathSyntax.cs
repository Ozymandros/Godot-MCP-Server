using System.Linq;
using System.Text;

namespace GodotMCP.Core;

/// <summary>
/// Central rules for filesystem paths passed into Godot MCP tools: mixed separators, optional leading
/// <c>/</c> or <c>\</c>, UNC / extended paths on Windows, and safe Unix absolute-path handling.
/// All project resolution should go through <c>IPathResolver.ResolvePath</c>; these helpers
/// keep string prep and merge semantics consistent across the Application and Infrastructure layers.
/// </summary>
public static class ProjectPathSyntax
{
    /// <summary>
    /// First path segment after the volume root on Unix-like systems that usually denotes a host-level
    /// directory (not a project-relative “leading slash” token). Used to avoid mapping <c>/home/...</c> into
    /// the project folder as <c>.../home/...</c> when the absolute path is outside the project.
    /// </summary>
    /// <remarks>
    /// Extend this set when new OS layouts appear; project folders named like these roots should use
    /// paths without a leading <c>/</c> or use an absolute path that already lies under the project root.
    /// </remarks>
    public static readonly HashSet<string> UnixHostRootFirstSegments =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "afs", "bin", "boot", "cores", "dev", "etc", "home", "lib", "lib64", "lost+found", "media", "mnt",
            "net", "nix", "opt", "private", "proc", "root", "run", "sbin", "srv", "sys", "system", "tmp", "usr",
            "var", "users", "volumes", "snap", "containers",
            // macOS
            "applications", "library", "network", "system"
        };

    /// <summary>
    /// UNC (<c>\\server\share</c>) or, on Windows only, <c>//server/share</c>.
    /// <c>///...</c> is not UNC (optional separators before a relative token).
    /// Backslash UNC is detected on all OS; forward-only UNC is Windows-specific so Linux <c>//</c> normalization does not collide.
    /// </summary>
    public static bool IsUncPath(string trimmed)
    {
        if (trimmed.Length < 2)
        {
            return false;
        }

        if (trimmed[0] == '\\' && trimmed[1] == '\\')
        {
            return true;
        }

        if (OperatingSystem.IsWindows() && trimmed[0] == '/' && trimmed[1] == '/')
        {
            return trimmed.Length >= 3 && trimmed[2] != '/';
        }

        return false;
    }

    /// <summary>
    /// Windows drive paths: <c>C:\</c>, <c>C:/</c>, <c>/C:/</c> (some tools emit a leading slash).
    /// </summary>
    public static bool IsWindowsDriveAbsolutePath(string trimmed)
    {
        if (trimmed.Length >= 2 && char.IsAsciiLetter(trimmed[0]) && trimmed[1] == ':')
        {
            return true;
        }

        return trimmed.Length >= 3
               && trimmed[0] is '/' or '\\'
               && char.IsAsciiLetter(trimmed[1])
               && trimmed[2] == ':';
    }

    /// <summary>
    /// <c>C:\</c> or <c>/C:/</c> style prefix — not a URI; may contain a false <c>://</c> from duplicate slashes (<c>C://x</c>).
    /// </summary>
    public static bool IsWindowsDriveLetterPathPrefix(string path)
    {
        if (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
        {
            return true;
        }

        return path.Length >= 3
               && path[0] is '/' or '\\'
               && char.IsAsciiLetter(path[1])
               && path[2] == ':';
    }

    /// <summary>
    /// Collapses every run of <c>/</c> or <c>\</c> to a single separator in each fragment.
    /// Preserves a leading UNC prefix (<c>\\</c> or, on Windows, <c>//server</c> when not <c>///</c>).
    /// </summary>
    public static string CollapseDuplicateDirectorySeparators(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        ReadOnlySpan<char> rest = path.AsSpan();
        if (rest.Length >= 2 && rest[0] == '\\' && rest[1] == '\\')
        {
            return "\\\\" + CollapseDuplicateSeparatorsInFragment(rest[2..]);
        }

        if (OperatingSystem.IsWindows() && rest.Length >= 2 && rest[0] == '/' && rest[1] == '/'
            && (rest.Length < 3 || rest[2] != '/'))
        {
            return "//" + CollapseDuplicateSeparatorsInFragment(rest[2..]);
        }

        return CollapseDuplicateSeparatorsInFragment(rest);
    }

    /// <summary>
    /// True when <paramref name="path"/> contains a URI-style <c>scheme://</c> (e.g. <c>https://</c>, <c>engine://</c>),
    /// but not a Windows drive typo such as <c>C://folder</c>.
    /// </summary>
    public static bool ContainsUriSchemeAuthority(string path)
    {
        if (!path.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        return !IsWindowsDriveLetterPathPrefix(path);
    }

    private static string CollapseDuplicateSeparatorsInFragment(ReadOnlySpan<char> s)
    {
        if (s.IsEmpty)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(s.Length);
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '/')
            {
                sb.Append('/');
                i++;
                while (i < s.Length && s[i] == '/')
                {
                    i++;
                }

                continue;
            }

            if (c == '\\')
            {
                sb.Append('\\');
                i++;
                while (i < s.Length && s[i] == '\\')
                {
                    i++;
                }

                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Removes every leading directory separator (both slash styles). Empty result means “project root only”.
    /// </summary>
    public static string TrimAllLeadingDirectorySeparators(string path)
    {
        var i = 0;
        while (i < path.Length && (path[i] == '/' || path[i] == '\\'))
        {
            i++;
        }

        return i == 0 ? path : path[i..];
    }

    /// <summary>
    /// On Unix, an absolute path outside the project that looks like <c>/scenes/...</c> should resolve under the
    /// project root; <c>/usr/...</c> should not be silently folded into the project tree.
    /// </summary>
    public static bool ShouldReinterpretUnixAbsoluteAsProjectRelative(string fullResolved)
    {
        var root = Path.GetPathRoot(fullResolved);
        if (string.IsNullOrEmpty(root))
        {
            return false;
        }

        var full = fullResolved.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (full.Equals(normalizedRoot, StringComparison.Ordinal))
        {
            return false;
        }

        var tail = full[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var end = tail.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        var firstSegment = end < 0 ? tail : tail[..end];

        return !UnixHostRootFirstSegments.Contains(firstSegment);
    }

    /// <summary>
    /// Normalizes a path token before merging with a base directory: strips optional leading separators
    /// (Windows: when not a drive path; Unix: when the path would be reinterpreted as project-relative),
    /// preserves UNC and drive letters, normalizes slashes for combine.
    /// </summary>
    public static string NormalizeRelativePathTokenForCombine(string fileName)
    {
        var t = CollapseDuplicateDirectorySeparators(fileName.Trim());
        if (string.IsNullOrEmpty(t))
        {
            return t;
        }

        if (IsUncPath(t))
        {
            return t.Replace('\\', '/');
        }

        if (OperatingSystem.IsWindows())
        {
            if (!IsWindowsDriveAbsolutePath(t))
            {
                t = TrimAllLeadingDirectorySeparators(t);
            }

            return t.Replace('\\', '/').TrimStart('/');
        }

        if (t.StartsWith('/') && t.Length > 1)
        {
            var full = Path.GetFullPath(t);
            if (ShouldReinterpretUnixAbsoluteAsProjectRelative(full))
            {
                t = TrimAllLeadingDirectorySeparators(t);
            }
        }

        return t.Replace('\\', '/').TrimStart('/');
    }

    /// <summary>
    /// Merges a base directory with a relative path and drops duplicated leading segments on the right side
    /// (Unity-style: avoids <c>Assets/Assets/...</c>).
    /// </summary>
    public static string CombineAvoidingDuplicateSegments(string baseDir, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Path.GetFullPath(baseDir);
        }

        var separator = Path.DirectorySeparatorChar;
        var rootSegments = baseDir
            .Split([separator, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var relativeSegments = relativePath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var strip = 0;
        var rootIndex = rootSegments.Length - 1;
        while (rootIndex >= 0
               && strip < relativeSegments.Length
               && string.Equals(rootSegments[rootIndex], relativeSegments[strip], StringComparison.OrdinalIgnoreCase))
        {
            strip++;
            rootIndex--;
        }

        if (strip >= relativeSegments.Length)
        {
            strip = 0;
        }

        var tail = string.Join(separator.ToString(), relativeSegments.Skip(strip));
        return Path.GetFullPath(Path.Combine(baseDir, tail));
    }
}
