using System.Text.RegularExpressions;

namespace ReponoStorage;

public static class Tools
{
    private static readonly Random rng = new Random();

    public static string GetRandomKey(int length)
    {
        Span<byte> buffer = stackalloc byte[length];
        lock (rng)
            rng.NextBytes(buffer);
        Span<char> chars = stackalloc char[length * 4 / 3 + 3];
        if (!Convert.TryToBase64Chars(buffer, chars, out int charsWritten))
            throw new InvalidOperationException("cannot base64 encode data");
        for (int i = 0; i < charsWritten; ++i)
            switch (chars[i])
            {
                case '+': chars[i] = '-'; break;
                case '/': chars[i] = '_'; break;
                case '=':
                    charsWritten = i;
                    break;
            }
        return new string(chars[..charsWritten]);
    }

    private static readonly Regex TokenValidator = new(@"^[a-zA-Z0-9_\-]+$", RegexOptions.Compiled);

    public static bool ValidKey(string key)
    {
        return TokenValidator.IsMatch(key);
    }
}