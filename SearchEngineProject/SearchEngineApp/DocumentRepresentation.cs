using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DocumentRepresentation
{
    public record DocumentMeta(int Id, string Path, string Title, int Length);

    // Interface: implement concrete parsers for PDF/DOCX/XLSX/etc to return plain text
    public interface IDocumentReader
    {
        string Read(string filePath);
    }
    public class DocReader : IDocumentReader
    {
        public string Read(string filePath) => File.ReadAllText(filePath);
    }


    // Tokenizer: normalization + tokenization + stopword removal
    public class Tokenizer
    {
        // a minimal stopword list; expand as needed
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "but", "if", "then", "else", "of", "in", "on", "at", "by", "for", "with",
            "to", "from", "is", "are", "was", "were", "be", "been", "being", "as", "that", "this", "these", "those",
            "he", "she", "it", "they", "we", "you", "I", "me", "my", "your", "our", "their"
            // The user requested to remove every stop word and focus on major keywords -- add more here
        };

        // Basic tokenizer: lowercase, remove punctuation (except keep intra-word hyphens), split on whitespace
        public IEnumerable<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            string norm = text.ToLowerInvariant();
            norm = Regex.Replace(norm, "<.*?>", " ");
            string[] parts = norm.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string p in parts)
            {
                if (p.Length <= 1) continue; // skip single-char tokens
                if (StopWords.Contains(p)) continue;
                // optionally: apply stemming or lemmatization here
                yield return p;
            }
        }
    }
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
            if (!_index.TryGetValue(term, out List<Posting>? postings))
            {
                postings = new List<Posting>();
                _index[term] = postings;
            }

            Posting? last = postings.LastOrDefault();
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

        public int GetDocLength(int docId) => _docLengths.TryGetValue(docId, out int l) ? l : 0;

        public IEnumerable<Posting> GetPostings(string term)
        {
            if (_index.TryGetValue(term, out List<Posting>? p)) return p;
            return Enumerable.Empty<Posting>();
        }

        public int DocFreq(string term) => _index.TryGetValue(term, out List<Posting>? p) ? p.Count : 0;

        public IEnumerable<int> AllDocIds() => _docLengths.Keys;
    }
    public class Indexer
    {
        private readonly IDocumentReader _reader;
        private readonly Tokenizer _tokenizer;
        private readonly InvertedIndex _index;
        private int _nextDocId = 1;

        private readonly Dictionary<int, DocumentMeta> _meta = new();


        public Indexer(IDocumentReader reader, Tokenizer tokenizer, InvertedIndex index)
        {
            _reader = reader;
            _tokenizer = tokenizer;
            _index = index;
        }

        // Index a single file path and return assigned docId
        public int IndexFile(string filePath)
        {
            string text = _reader.Read(filePath);
            List<string> tokens = _tokenizer.Tokenize(text).ToList();
            int docId = _nextDocId++;
            for (int i = 0; i < tokens.Count; i++)
            {
                _index.AddTerm(tokens[i], docId, i);
            }
            _index.SetDocLength(docId, tokens.Count);
            string title = Path.GetFileName(filePath);
            _meta[docId] = new DocumentMeta(docId, filePath, title, tokens.Count);
            return docId;
        }

        public void IndexDirectory(string directory, string searchPattern = "*.txt")
        {
            IEnumerable<string> files = Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories);
            foreach (string f in files) IndexFile(f);
        }

        public IReadOnlyDictionary<int, DocumentMeta> Metadata => _meta;
    }
}