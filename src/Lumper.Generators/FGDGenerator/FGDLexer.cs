namespace Lumper.Generators.FGDGenerator;

using System.Collections.Generic;

public enum TokenType { Word, _String, Symbol, EOF }
public record Token(TokenType Type, string Value);

public static class FGDLexer
{
    public static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        int i = 0;

        while (i < input.Length)
        {
            // Ignore whitespace
            char c = input[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Ignore comments
            if (c == '/' && i + 1 < input.Length && input[i + 1] == '/')
            {
                while (i < input.Length && input[i] != '\n') i++;
                continue;
            }

            // Strings
            if(c == '"')
            {
                i++;
                int start = i;
                while ( i < input.Length && input[i] != '"') i++;
                tokens.Add(new Token(TokenType._String, input[start..i]));
                i++; // Consume end quote
                continue;
            }

            // Symbols
            if("@=:[](),".Contains(c))
            {
                tokens.Add(new Token(TokenType.Symbol, c.ToString()));
                i++;
                continue;
            }

            // Words
            int wStart = i;
            while (i < input.Length && !char.IsWhiteSpace(input[i]) && !"@=:[](),\"".Contains(input[i]))
            {
                i++;
            }
            tokens.Add(new Token(TokenType.Word, input[wStart..i]));
        }

        tokens.Add(new Token(TokenType.EOF, ""));
        return tokens;
    }
}