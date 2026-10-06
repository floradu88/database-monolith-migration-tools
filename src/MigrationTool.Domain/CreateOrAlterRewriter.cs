namespace MigrationTool.Domain;

public sealed record BatchRewrite(IReadOnlyList<string> Batches, bool Changed, bool Repeatable);

public static class CreateOrAlterRewriter
{
    public static BatchRewrite RewriteBatches(IReadOnlyList<string> batches, DatabaseProviderKind provider)
    {
        var rewritten = new List<string>(batches.Count);
        var changed = false;
        var sawStatement = false;
        var allModules = true;
        foreach (var batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                rewritten.Add(batch);
                continue;
            }

            var one = Rewrite(batch, provider);
            rewritten.Add(one.Sql);
            changed |= one.Changed;
            sawStatement = true;
            allModules &= one.IsModule;
        }

        return new BatchRewrite(rewritten, changed, sawStatement && allModules);
    }

    public static ModuleRewrite Rewrite(string sql, DatabaseProviderKind provider)
    {
        var words = ScanWords(sql);
        var inserts = new List<int>();
        for (var i = 0; i < words.Count; i++)
        {
            if (!EqualsWord(words[i].Text, "CREATE"))
            {
                continue;
            }

            if (i + 1 >= words.Count || EqualsWord(words[i + 1].Text, "OR"))
            {
                continue;
            }

            if (!IsModuleType(words[i + 1].Text, provider))
            {
                continue;
            }

            inserts.Add(words[i].End);
        }

        var clause = provider == DatabaseProviderKind.PostgreSql ? " OR REPLACE" : " OR ALTER";
        var rewritten = sql;
        for (var i = inserts.Count - 1; i >= 0; i--)
        {
            rewritten = rewritten.Insert(inserts[i], clause);
        }

        return new ModuleRewrite(rewritten, inserts.Count > 0, IsModule(words, provider));
    }

    private static bool IsModule(List<Word> words, DatabaseProviderKind provider)
    {
        if (words.Count == 0)
        {
            return false;
        }

        if (EqualsWord(words[0].Text, "ALTER"))
        {
            return words.Count > 1 && IsModuleType(words[1].Text, provider);
        }

        if (!EqualsWord(words[0].Text, "CREATE"))
        {
            return false;
        }

        var index = 1;
        if (index < words.Count && EqualsWord(words[index].Text, "OR"))
        {
            index++;
            if (index < words.Count &&
                (EqualsWord(words[index].Text, "ALTER") || EqualsWord(words[index].Text, "REPLACE")))
            {
                index++;
            }
        }

        return index < words.Count && IsModuleType(words[index].Text, provider);
    }

    private static bool IsModuleType(string word, DatabaseProviderKind provider)
    {
        if (provider == DatabaseProviderKind.SqlServer && EqualsWord(word, "PROC"))
        {
            return true;
        }

        return EqualsWord(word, "PROCEDURE")
               || EqualsWord(word, "FUNCTION")
               || EqualsWord(word, "VIEW")
               || EqualsWord(word, "TRIGGER");
    }

    private static List<Word> ScanWords(string sql)
    {
        var words = new List<Word>();
        var i = 0;
        while (i < sql.Length)
        {
            var current = sql[i];
            if (char.IsWhiteSpace(current))
            {
                i++;
                continue;
            }

            if (current == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                i += 2;
                while (i < sql.Length && sql[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (current == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                {
                    i++;
                }

                i = Math.Min(sql.Length, i + 2);
                continue;
            }

            if ((current is 'N' or 'n') && i + 1 < sql.Length && sql[i + 1] == '\'')
            {
                i = SkipString(sql, i + 1);
                continue;
            }

            if (current == '\'')
            {
                i = SkipString(sql, i);
                continue;
            }

            if (current == '[')
            {
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == ']' && i + 1 < sql.Length && sql[i + 1] == ']')
                    {
                        i += 2;
                        continue;
                    }

                    if (sql[i] == ']')
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                continue;
            }

            if (current == '"')
            {
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == '"' && i + 1 < sql.Length && sql[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    if (sql[i] == '"')
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                continue;
            }

            if (char.IsLetter(current) || current is '_' or '@' or '#')
            {
                var start = i;
                i++;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] is '_' or '@' or '#'))
                {
                    i++;
                }

                words.Add(new Word(sql[start..i], start, i));
                continue;
            }

            i++;
        }

        return words;
    }

    private static int SkipString(string sql, int quoteIndex)
    {
        var i = quoteIndex + 1;
        while (i < sql.Length)
        {
            if (sql[i] == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
            {
                i += 2;
                continue;
            }

            if (sql[i] == '\'')
            {
                return i + 1;
            }

            i++;
        }

        return sql.Length;
    }

    private static bool EqualsWord(string word, string expected) =>
        string.Equals(word, expected, StringComparison.OrdinalIgnoreCase);

    private readonly record struct Word(string Text, int Start, int End);

    public sealed record ModuleRewrite(string Sql, bool Changed, bool IsModule);
}
