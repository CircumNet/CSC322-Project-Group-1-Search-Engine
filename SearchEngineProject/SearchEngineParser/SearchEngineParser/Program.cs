// Program.cs
// A simple C# parser for search engine queries.
// This example handles keywords, quoted phrases, and logical operators like AND, OR, NOT.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace SearchEngineParser
{
    // 1. Tokenization: Breaking the search query string into a list of tokens.
    public enum TokenType
    {
        Keyword,      // A single word to search for
        Phrase,       // A quoted string to search for as an exact phrase
        And,          // 'AND' or '+'
        Or,           // 'OR'
        Not,          // 'NOT' or '-'
        LeftParen,    // '(' for grouping
        RightParen,   // ')' for grouping
        EOF           // End of File, to signify the end of the input
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Value { get; }

        public Token(TokenType type, string value = null)
        {
            Type = type;
            Value = value;
        }

        public override string ToString()
        {
            return $"Token({Type}, \"{Value}\")";
        }
    }

    // Helper class to filter out common stopwords.
    public static class StopwordFilter
    {
        // A simple, static list of common English stopwords.
        private static readonly HashSet<string> _stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "but", "by",
        "for", "if", "in", "into", "is", "it", "no", "not", "of",
        "on", "or", "such", "that", "the", "their", "then", "there",
        "these", "they", "this", "to", "was", "with"
    };

        public static bool IsStopword(string word)
        {
            return _stopwords.Contains(word);
        }
    }

    public class Lexer
    {
        private readonly string _text;
        private int _position;
        private char? CurrentChar => _position < _text.Length ? _text[_position] : (char?)null;

        public Lexer(string text)
        {
            _text = text;
            _position = 0;
        }

        private void Advance()
        {
            _position++;
        }

        private void SkipWhitespace()
        {
            while (CurrentChar.HasValue && char.IsWhiteSpace(CurrentChar.Value))
            {
                Advance();
            }
        }

        private Token ParseKeywordOrOperator()
        {
            string value = "";
            while (CurrentChar.HasValue && !char.IsWhiteSpace(CurrentChar.Value) && CurrentChar.Value != '\"' && CurrentChar.Value != '(' && CurrentChar.Value != ')')
            {
                value += CurrentChar.Value;
                Advance();
            }

            // Check for special operators
            switch (value.ToUpper())
            {
                case "AND": return new Token(TokenType.And);
                case "OR": return new Token(TokenType.Or);
                case "NOT": return new Token(TokenType.Not);
                default: return new Token(TokenType.Keyword, value);
            }
        }

        private Token ParsePhrase()
        {
            Advance(); // Consume the opening quote
            string value = "";
            while (CurrentChar.HasValue && CurrentChar.Value != '\"')
            {
                value += CurrentChar.Value;
                Advance();
            }
            if (CurrentChar.HasValue && CurrentChar.Value == '\"')
            {
                Advance(); // Consume the closing quote
                return new Token(TokenType.Phrase, value);
            }
            else
            {
                throw new Exception("Unterminated quoted phrase.");
            }
        }

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            while (CurrentChar.HasValue)
            {
                SkipWhitespace();
                if (!CurrentChar.HasValue) break;

                if (char.IsLetterOrDigit(CurrentChar.Value))
                {
                    var token = ParseKeywordOrOperator();
                    // Filter out stopword tokens right after parsing.
                    if (token.Type != TokenType.Keyword || !StopwordFilter.IsStopword(token.Value))
                    {
                        tokens.Add(token);
                    }
                    continue;
                }

                switch (CurrentChar.Value)
                {
                    case '\"':
                        tokens.Add(ParsePhrase());
                        continue;
                    case '+':
                        Advance();
                        tokens.Add(new Token(TokenType.And));
                        continue;
                    case '-':
                        Advance();
                        tokens.Add(new Token(TokenType.Not));
                        continue;
                    case '(':
                        Advance();
                        tokens.Add(new Token(TokenType.LeftParen));
                        continue;
                    case ')':
                        Advance();
                        tokens.Add(new Token(TokenType.RightParen));
                        continue;
                    default:
                        string value = "";
                        while (CurrentChar.HasValue && !char.IsWhiteSpace(CurrentChar.Value))
                        {
                            value += CurrentChar.Value;
                            Advance();
                        }
                        if (!StopwordFilter.IsStopword(value))
                        {
                            tokens.Add(new Token(TokenType.Keyword, value));
                        }
                        break;
                }
            }
            tokens.Add(new Token(TokenType.EOF));
            return tokens;
        }
    }

    // 2. Abstract Syntax Tree (AST): Representing the search query's logical structure.
    public abstract class AstNode
    {
        public abstract void Print(int indent = 0);
    }

    public class KeywordNode : AstNode
    {
        public string Value { get; }
        public KeywordNode(Token token) { Value = token.Value; }
        public override void Print(int indent = 0)
        {
            Console.WriteLine($"{new string(' ', indent)}- Keyword: {Value}");
        }
    }

    public class PhraseNode : AstNode
    {
        public string Value { get; }
        public PhraseNode(Token token) { Value = token.Value; }
        public override void Print(int indent = 0)
        {
            Console.WriteLine($"{new string(' ', indent)}- Phrase: \"{Value}\"");
        }
    }

    public class BinaryOpNode : AstNode
    {
        public AstNode Left { get; }
        public Token Op { get; }
        public AstNode Right { get; }

        public BinaryOpNode(AstNode left, Token op, AstNode right)
        {
            Left = left;
            Op = op;
            Right = right;
        }

        public override void Print(int indent = 0)
        {
            Console.WriteLine($"{new string(' ', indent)}- {Op.Type}");
            Left.Print(indent + 2);
            Right.Print(indent + 2);
        }
    }

    public class UnaryOpNode : AstNode
    {
        public Token Op { get; }
        public AstNode Child { get; }

        public UnaryOpNode(Token op, AstNode child)
        {
            Op = op;
            Child = child;
        }

        public override void Print(int indent = 0)
        {
            Console.WriteLine($"{new string(' ', indent)}- {Op.Type}");
            Child.Print(indent + 2);
        }
    }

    // 3. Parsing: Building the AST from the tokens.
    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _position;
        private Token CurrentToken => _tokens[_position];

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
            _position = 0;
        }

        private void Eat(TokenType type)
        {
            if (CurrentToken.Type == type)
            {
                _position++;
            }
            else
            {
                throw new Exception($"Unexpected token type. Expected {type}, but got {CurrentToken.Type} at position {_position}");
            }
        }

        private AstNode Factor()
        {
            var token = CurrentToken;
            if (token.Type == TokenType.Keyword)
            {
                Eat(TokenType.Keyword);
                return new KeywordNode(token);
            }
            else if (token.Type == TokenType.Phrase)
            {
                Eat(TokenType.Phrase);
                return new PhraseNode(token);
            }
            else if (token.Type == TokenType.LeftParen)
            {
                Eat(TokenType.LeftParen);
                var node = OrExpression();
                Eat(TokenType.RightParen);
                return node;
            }
            else
            {
                throw new Exception($"Unexpected token in Factor: {token.Type}");
            }
        }

        private AstNode UnaryExpression()
        {
            if (CurrentToken.Type == TokenType.Not)
            {
                var token = CurrentToken;
                Eat(TokenType.Not);
                return new UnaryOpNode(token, Factor());
            }
            return Factor();
        }

        private AstNode AndExpression()
        {
            var node = UnaryExpression();
            while (CurrentToken.Type == TokenType.And)
            {
                var token = CurrentToken;
                Eat(TokenType.And);
                node = new BinaryOpNode(node, token, UnaryExpression());
            }
            return node;
        }

        public AstNode OrExpression()
        {
            var node = AndExpression();
            while (CurrentToken.Type == TokenType.Or)
            {
                var token = CurrentToken;
                Eat(TokenType.Or);
                node = new BinaryOpNode(node, token, AndExpression());
            }
            return node;
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            // Example query with stopwords.
            string searchQuery = "the best apple AND a new iphone OR in the macbook pro NOT for vintage";
            Console.WriteLine($"Parsing: '{searchQuery}'");

            try
            {
                var lexer = new Lexer(searchQuery);
                var tokens = lexer.Tokenize();
                Console.WriteLine("\nTokens (stopwords removed): " + string.Join(", ", tokens));

                var parser = new Parser(tokens);
                var ast = parser.OrExpression();
                Console.WriteLine("\n--- Abstract Syntax Tree ---");
                ast.Print();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nAn error occurred: {ex.Message}");
            }
        }
    }

    }
