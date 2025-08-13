/*
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SimpleSearchEngine
{
    #region Document parsing/tokenization

    // Basic document metadata
    public record DocumentMeta(int Id, string Path, string Title, int Length);

    // Interface: implement concrete parsers for PDF/DOCX/XLSX/etc to return plain text
    public interface IDocumentParser
    {
        // returns plain text extracted from the filePath
        string Parse(string filePath);
    }

    // Example simple TXT parser
    public class TxtParser : IDocumentParser
    {
        public string Parse(string filePath) => File.ReadAllText(filePath);
    }

    // Tokenizer: normalization + tokenization + stopword removal
    public class Tokenizer
    {
        // a minimal stopword list; expand as needed
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a","an","the","and","or","but","if","then","else","of","in","on","at","by","for","with",
            "to","from","is","are","was","were","be","been","being","as","that","this","these","those",
            "he","she","it","they","we","you","I","me","my","your","our","their"
            // The user requested to remove every stop word and focus on major keywords -- add more here
        };

        // Basic tokenizer: lowercase, remove punctuation (except keep intra-word hyphens), split on whitespace
        public IEnumerable<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            // normalize
            var norm = text.ToLowerInvariant();
            // remove HTML tags quickly
            norm = Regex.Replace(norm, "<.*?>", " ");
            // replace non-word characters with space (keep dash and apostrophe inside words)
            norm = Regex.Replace(norm, "[^\w\-']+", " ");
            var parts = norm.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                if (p.Length <= 1) continue; // skip single-char tokens
                if (StopWords.Contains(p)) continue;
                // optionally: apply stemming or lemmatization here
                yield return p;
            }
        }
    }

    #endregion

    #region Index data structures

    public class Posting
    {
        public int DocId { get; }
        public int TermFrequency { get; private set; }
        public List<int> Positions { get; } = new();

        public Posting(int docId, int pos)
        {
            DocId = docId;
            TermFrequency = 1;
            Positions.Add(pos);
        }

        public void AddPosition(int pos)
        {
            TermFrequency++;
            Positions.Add(pos);
        }
    }

    // Inverted index: term -> postings list
    public class InvertedIndex
    {
        // term -> list of postings
        private readonly Dictionary<string, List<Posting>> _index = new(StringComparer.OrdinalIgnoreCase);

        // docId -> length (number of tokens)
        private readonly Dictionary<int, int> _docLengths = new();

        public int DocumentCount => _docLengths.Count;

        public void AddTerm(string term, int docId, int position)
        {
            if (!_index.TryGetValue(term, out var postings))
            {
                postings = new List<Posting>();
                _index[term] = postings;
            }

            var last = postings.LastOrDefault();
            if (last != null && last.DocId == docId)
            {
                last.AddPosition(position);
            }
            else
            {
                postings.Add(new Posting(docId, position));
            }
        }

        public IReadOnlyDictionary<string, List<Posting>> Terms => _index;

        public void SetDocLength(int docId, int length) => _docLengths[docId] = length;

        public int GetDocLength(int docId) => _docLengths.TryGetValue(docId, out var l) ? l : 0;

        public IEnumerable<Posting> GetPostings(string term)
        {
            if (_index.TryGetValue(term, out var p)) return p;
            return Enumerable.Empty<Posting>();
        }

        public int DocFreq(string term) => _index.TryGetValue(term, out var p) ? p.Count : 0;

        public IEnumerable<int> AllDocIds() => _docLengths.Keys;

        // Serialization / Persistence can be added (binary dump or use an embedded DB)
    }

    #endregion

    #region Indexer

    public class Indexer
    {
        private readonly IDocumentParser _parser;
        private readonly Tokenizer _tokenizer;
        private readonly InvertedIndex _index;
        private int _nextDocId = 1;

        // metadata store
        private readonly Dictionary<int, DocumentMeta> _meta = new();

        public Indexer(IDocumentParser parser, Tokenizer tokenizer, InvertedIndex index)
        {
            _parser = parser;
            _tokenizer = tokenizer;
            _index = index;
        }

        // Index a single file path and return assigned docId
        public int IndexFile(string filePath)
        {
            var text = _parser.Parse(filePath);
            var tokens = _tokenizer.Tokenize(text).ToList();
            var docId = _nextDocId++;
            for (int i = 0; i < tokens.Count; i++)
            {
                _index.AddTerm(tokens[i], docId, i);
            }
            _index.SetDocLength(docId, tokens.Count);
            var title = Path.GetFileName(filePath);
            _meta[docId] = new DocumentMeta(docId, filePath, title, tokens.Count);
            return docId;
        }

        // Batch index
        public void IndexDirectory(string directory, string searchPattern = "*.txt")
        {
            var files = Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories);
            foreach (var f in files) IndexFile(f);
        }

        public IReadOnlyDictionary<int, DocumentMeta> Metadata => _meta;
    }

    #endregion

    #region Query AST and Parser

    // AST nodes for query representation
    public abstract record QueryNode;
    public record TermNode(string Term) : QueryNode;
    public record PhraseNode(string Phrase) : QueryNode; // phrase will be tokenized again
    public record AndNode(QueryNode Left, QueryNode Right) : QueryNode;
    public record OrNode(QueryNode Left, QueryNode Right) : QueryNode;
    public record NotNode(QueryNode Child) : QueryNode;

    // Very small recursive-descent parser that supports: terms, quoted phrases, AND, OR, NOT, parentheses
    public class QueryParser
    {
        private readonly Tokenizer _tokenizer;
        private readonly string _input;
        private int _pos = 0;

        public QueryParser(Tokenizer tokenizer, string input)
        {
            _tokenizer = tokenizer;
            _input = input ?? string.Empty;
        }

        public QueryNode Parse()
        {
            _pos = 0;
            var node = ParseOr();
            return node;
        }

        private void SkipWhitespace() { while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos])) _pos++; }

        private QueryNode ParseOr()
        {
            var left = ParseAnd();
            while (true)
            {
                SkipWhitespace();
                if (MatchKeyword("or"))
                {
                    var right = ParseAnd();
                    left = new OrNode(left, right);
                }
                else break;
            }
            return left;
        }

        private QueryNode ParseAnd()
        {
            var left = ParseNot();
            while (true)
            {
                SkipWhitespace();
                if (MatchKeyword("and"))
                {
                    var right = ParseNot();
                    left = new AndNode(left, right);
                }
                else break;
            }
            return left;
        }

        private QueryNode ParseNot()
        {
            SkipWhitespace();
            if (MatchKeyword("not"))
            {
                var child = ParsePrimary();
                return new NotNode(child);
            }
            return ParsePrimary();
        }

        private QueryNode ParsePrimary()
        {
            SkipWhitespace();
            if (_pos >= _input.Length) return new TermNode(string.Empty);
            if (_input[_pos] == '(')
            {
                _pos++; // consume
                var node = ParseOr();
                SkipWhitespace(); if (_pos < _input.Length && _input[_pos] == ')') _pos++;
                return node;
            }
            if (_input[_pos] == '"')
            {
                _pos++;
                var start = _pos;
                while (_pos < _input.Length && _input[_pos] != '"') _pos++;
                var phrase = _input[start.._pos];
                if (_pos < _input.Length && _input[_pos] == '"') _pos++;
                return new PhraseNode(phrase);
            }

            // read until whitespace or operator
            var sb = new System.Text.StringBuilder();
            while (_pos < _input.Length && !char.IsWhiteSpace(_input[_pos]) && _input[_pos] != ')')
            {
                sb.Append(_input[_pos]); _pos++;
            }
            return new TermNode(sb.ToString());
        }

        private bool MatchKeyword(string kw)
        {
            SkipWhitespace();
            var save = _pos;
            var len = kw.Length;
            if (_pos + len <= _input.Length && string.Equals(_input.Substring(_pos, len), kw, StringComparison.OrdinalIgnoreCase))
            {
                _pos += len; return true;
            }
            _pos = save; return false;
        }
    }

    #endregion

    #region Matching & Ranking (BM25)

    public class RankedResult
    {
        public int DocId { get; set; }
        public double Score { get; set; }
    }

    // Basic BM25 ranker using the inverted index
    public class BM25Ranker
    {
        private readonly InvertedIndex _index;
        private readonly double _k1 = 1.5;
        private readonly double _b = 0.75;

        public BM25Ranker(InvertedIndex index)
        {
            _index = index;
        }

        private double AvgDocLength => _index.AllDocIds().Any() ? _index.AllDocIds().Average(id => _index.GetDocLength(id)) : 0.0;

        private double IDF(string term)
        {
            var N = Math.Max(1, _index.DocumentCount);
            var df = _index.DocFreq(term);
            // add small smoothing
            return Math.Log((N - df + 0.5) / (df + 0.5) + 1.0);
        }

        // Evaluate a bag-of-terms query (we'll handle phrase/boolean by transforming into candidate set manually)
        public IEnumerable<RankedResult> ScoreTerms(IEnumerable<string> queryTerms)
        {
            var avgLen = AvgDocLength;
            var scores = new Dictionary<int, double>();
            var unique = queryTerms.GroupBy(t => t).Select(g => (term: g.Key, qtf: g.Count()));
            foreach (var (term, qtf) in unique)
            {
                var idf = IDF(term);
                var postings = _index.GetPostings(term);
                foreach (var p in postings)
                {
                    var dl = _index.GetDocLength(p.DocId);
                    var tf = p.TermFrequency;
                    var denom = tf + _k1 * (1 - _b + _b * (dl / avgLen));
                    var termScore = idf * (tf * (_k1 + 1)) / denom;
                    if (!scores.TryGetValue(p.DocId, out var cur)) cur = 0;
                    scores[p.DocId] = cur + termScore;
                }
            }
            return scores.Select(kv => new RankedResult { DocId = kv.Key, Score = kv.Value })
                         .OrderByDescending(r => r.Score);
        }
    }

    #endregion

    #region Search service that ties AST -> matching

    public class SearchService
    {
        private readonly InvertedIndex _index;
        private readonly Tokenizer _tokenizer;
        private readonly BM25Ranker _ranker;
        private readonly Indexer _indexer; // to get metadata

        public SearchService(InvertedIndex index, Tokenizer tokenizer, BM25Ranker ranker, Indexer indexer)
        {
            _index = index; _tokenizer = tokenizer; _ranker = ranker; _indexer = indexer;
        }

        // Evaluate AST and produce ranked results
        public IEnumerable<RankedResult> Search(QueryNode node)
        {
            // Strategy: reduce the AST into a candidate set of document IDs and an unordered bag-of-words representing positive terms.
            var terms = new List<string>();
            var candidateDocs = new HashSet<int>(_index.AllDocIds());

            // Walk the AST collecting positive terms and applying boolean filters conservatively
            void Walk(QueryNode n)
            {
                switch (n)
                {
                    case TermNode t:
                        terms.AddRange(_tokenizer.Tokenize(t.Term));
                        break;
                    case PhraseNode p:
                        // phrase: tokenize phrase and create postings intersection + proximity check -- for simplicity, add terms
                        terms.AddRange(_tokenizer.Tokenize(p.Phrase));
                        break;
                    case AndNode a:
                        Walk(a.Left); Walk(a.Right);
                        break;
                    case OrNode o:
                        Walk(o.Left); Walk(o.Right);
                        break;
                    case NotNode not:
                        // NOT can be handled later by filtering results
                        Walk(not.Child);
                        break;
                }
            }

            Walk(node);

            if (!terms.Any()) return Enumerable.Empty<RankedResult>();

            // For efficiency: obtain candidate docs that contain at least one query term
            var candidate = new HashSet<int>();
            foreach (var term in terms.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var p in _index.GetPostings(term)) candidate.Add(p.DocId);
            }

            if (!candidate.Any()) return Enumerable.Empty<RankedResult>();

            // Score: use BM25 but only on candidate docs
            var scored = _ranker.ScoreTerms(terms).Where(r => candidate.Contains(r.DocId));
            return scored;
        }
    }

    #endregion

    #region Example minimal usage in a console app (or wire into ASP.NET Core)

    public static class ExampleRunner
    {
        public static void Demo()
        {
            var parser = new TxtParser();
            var tokenizer = new Tokenizer();
            var index = new InvertedIndex();
            var indexer = new Indexer(parser, tokenizer, index);

            // index sample directory
            // indexer.IndexDirectory("./docs", "*.txt");
            // For demonstration, index two short strings by writing temp files
            var f1 = Path.GetTempFileName(); File.WriteAllText(f1, "The quick brown fox jumps over the lazy dog.");
            var f2 = Path.GetTempFileName(); File.WriteAllText(f2, "Fast brown foxes leap over sleeping dogs in the park.");
            var id1 = indexer.IndexFile(f1);
            var id2 = indexer.IndexFile(f2);

            var ranker = new BM25Ranker(index);
            var service = new SearchService(index, tokenizer, ranker, indexer);

            var parserQ = new QueryParser(tokenizer, "\"brown fox\" AND park");
            var ast = parserQ.Parse();
            var results = service.Search(ast);
            foreach (var r in results)
            {
                Console.WriteLine($"Doc: {r.DocId} Score: {r.Score:F4}");
            }
        }
    }

    #endregion
}
*/
/*
Further production notes / improvements:

1) Document Parsers
   - Implement IDocumentParser for PDF/DOCX/XLSX/HTML using robust libraries. Clean HTML by using an HTML sanitizer.

2) Tokenization & NLP
   - Use an established NLP library for C# (e.g., ML.NET for tokenization/lemmatization) or call a microservice in Python
     that does POS tagging, lemmatization, and named-entity extraction. Removing "every stop word" may need POS analysis
     to keep domain-specific nouns.

3) Semantic matching
   - Add an embedding store (annoy, FAISS, or an approximate nearest neighbor library) and store document embeddings for
     semantic retrieval. Use hybrid: first lexical candidate selection (inverted index) then semantic re-ranking.

4) Advanced Ranking
   - Implement BM25 with fielded boosting (title, headings), document freshness signals, clickable metadata, and user clicks for
     learning-to-rank.

5) Persistence
   - Serialize inverted index to disk, or use an embeddable key-value store. Keep metadata in JSON.

6) Scalability
   - Shard the index by document id range or term-hash. Use memory-mapped files for large posting lists.

7) API
   - Expose endpoints: POST /index (file upload or repo path), POST /search (query + options), GET /doc/{id}
   - Use pagination, timeouts, and caching.

8) Tests
   - Unit tests for tokenizer, indexer, ranking. Integration tests with sample doc corpus.

This reference implementation is intentionally small and clear — adapt and extend for your university project.
*/

