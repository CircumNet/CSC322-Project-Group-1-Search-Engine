//
// FILE: QueryRepresentation.cs
//
// This is your original file, adapted for use in the web project.
// The namespace has been changed.
//
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
            return value.ToLower() switch
            {
                "and" => new Token(TokenType.And, "AND"),
                "or" => new Token(TokenType.Or, "OR"),
                "not" => new Token(TokenType.Not, "NOT"),
                _ => new Token(TokenType.Keyword, value)
            };
        }

        private Token ParsePhrase()
        {
            Advance(); // Skip the opening quote
            string value = "";
            while (CurrentChar.HasValue && CurrentChar.Value != '\"')
            {
                value += CurrentChar.Value;
                Advance();
            }
            if (!CurrentChar.HasValue) throw new Exception("Unclosed phrase literal");
            Advance(); // Skip the closing quote
            return new Token(TokenType.Phrase, value);
        }

        public IEnumerable<Token> Tokenize()
        {
            while (CurrentChar.HasValue)
            {
                SkipWhitespace();
                if (!CurrentChar.HasValue) break;

                char current = CurrentChar.Value;
                if (current == '(')
                {
                    Advance();
                    yield return new Token(TokenType.LeftParen, "(");
                }
                else if (current == ')')
                {
                    Advance();
                    yield return new Token(TokenType.RightParen, ")");
                }
                else if (current == '\"')
                {
                    yield return ParsePhrase();
                }
                else
                {
                    yield return ParseKeywordOrOperator();
                }
            }
            yield return new Token(TokenType.EOF, "");
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
            if (Current.Type != type)
            {
                throw new Exception($"Expected token type {type}, but got {Current.Type} at position {_pos}");
            }
            _pos++;
        }

        public QueryNode Parse()
        {
            QueryNode node = ParseOr();
            if (Current.Type != TokenType.EOF)
            {
                throw new Exception($"Unexpected token {Current.Type} at end of expression");
            }
            return node;
        }

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
