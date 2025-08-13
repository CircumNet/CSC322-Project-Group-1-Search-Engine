using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentRepresentation;
using MatchingModule;

namespace QueryRepresentation
{
    public abstract record QueryNode;
    public record TermNode(string Term) : QueryNode;
    public record PhraseAstNode(string Phrase) : QueryNode;
    public record AndNode(QueryNode Left, QueryNode Right) : QueryNode;
    public record OrNode(QueryNode Left, QueryNode Right) : QueryNode;
    public record NotNode(QueryNode Child) : QueryNode;
    public enum TokenType
    {
        Keyword,
        Phrase,
        And,
        Or,
        Not,
        LeftParen,
        RightParen,
        EOF
    }
    public class Token
    {
        public TokenType Type { get; }
        public string Value { get; }

        public Token(TokenType type, string value = "")
        {
            Type = type;
            Value = value;
        }

        public override string ToString()
        {
            return $"Token({Type}, \"{Value}\")";
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
            List<Token> tokens = new List<Token>();
            while (CurrentChar.HasValue)
            {
                SkipWhitespace();
                if (!CurrentChar.HasValue) break;

                if (char.IsLetterOrDigit(CurrentChar.Value))
                {
                    Token token = ParseKeywordOrOperator();
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

    public class Parser
    {
        private readonly List<Token> _tokens;
        private int _pos;
        private Token Current => _tokens[_pos];

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
            _pos = 0;
        }

        private void Eat(TokenType type)
        {
            if (Current.Type == type) _pos++;
            else throw new Exception($"Expected {type} but got {Current.Type} at position {_pos}");
        }

        public QueryNode Parse() => ParseOr();

        // OR has lowest precedence
        private QueryNode ParseOr()
        {
            QueryNode node = ParseAnd();
            while (Current.Type == TokenType.Or)
            {
                Eat(TokenType.Or);
                node = new OrNode(node, ParseAnd());
            }
            return node;
        }

        // AND has medium precedence
        private QueryNode ParseAnd()
        {
            QueryNode node = ParseNot();
            while (Current.Type == TokenType.And)
            {
                Eat(TokenType.And);
                node = new AndNode(node, ParseNot());
            }
            return node;
        }

        // NOT has highest precedence
        private QueryNode ParseNot()
        {
            if (Current.Type == TokenType.Not)
            {
                Eat(TokenType.Not);
                return new NotNode(ParsePrimary());
            }
            return ParsePrimary();
        }

        private QueryNode ParsePrimary()
        {
            Token token = Current;
            switch (token.Type)
            {
                case TokenType.Keyword:
                    Eat(TokenType.Keyword);
                    return new TermNode(token.Value);
                case TokenType.Phrase:
                    Eat(TokenType.Phrase);
                    return new PhraseAstNode(token.Value);
                case TokenType.LeftParen:
                    Eat(TokenType.LeftParen);
                    QueryNode node = ParseOr();
                    Eat(TokenType.RightParen);
                    return node;
                default:
                    throw new Exception($"Unexpected token {token.Type} at position {_pos}");
            }
        }
    }

}