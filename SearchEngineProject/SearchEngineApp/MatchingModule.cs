using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using QueryRepresentation;
using DocumentRepresentation;

namespace MatchingModule
{
    public class RankedResult
    {
        public int DocId { get; set; }
        public double Score { get; set; }
    }
    public class Ranker
    {
        private readonly InvertedIndex _index;
        private readonly double _k1 = 1.5;
        private readonly double _b = 0.75;

        public Ranker(InvertedIndex index) { _index = index; }
        private double AvgDocLength => _index.AllDocIds().Any() ? _index.AllDocIds().Average(id => _index.GetDocLength(id)) : 0.0;

        private double IDF(string term)
        {
            var N = Math.Max(1, _index.DocumentCount);
            var df = _index.DocFreq(term);
            return Math.Log((N - df + 0.5) / (df + 0.5) + 1.0);
        }
        public IEnumerable<RankedResult> ScoreTerms(IEnumerable<string> queryTerms)
        {
            double avgLen = AvgDocLength;
            Dictionary<int, double> scores = new Dictionary<int, double>();
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

    public class SearchService
    {
        private readonly InvertedIndex _index;
        private readonly Tokenizer _tokenizer;
        private readonly Ranker _ranker;
        private readonly Indexer _indexer;

        public SearchService(InvertedIndex index, Tokenizer tokenizer, Ranker ranker, Indexer indexer)
        {
            _index = index; _tokenizer = tokenizer; _ranker = ranker; _indexer = indexer;
        }


        public IEnumerable<RankedResult> Search(QueryNode node)
        {
            // Strategy: reduce the AST into a candidate set of document IDs and an unordered bag-of-words representing positive terms.
            List<string> terms = new List<string>();
            HashSet<int> candidateDocs = new HashSet<int>(_index.AllDocIds());

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
                        Walk(a.Left);
                        Walk(a.Right);
                        break;
                    case OrNode o:
                        Walk(o.Left);
                        Walk(o.Right);
                        break;
                    case NotNode not:
                        // NOT can be handled later by filtering results
                        Walk(not.Child);
                        break;
                }
            }

            Walk(node);

            if (!terms.Any())
                return Enumerable.Empty<RankedResult>();

            // For efficiency: obtain candidate docs that contain at least one query term
            HashSet<int> candidate = new HashSet<int>();
            foreach (string term in terms.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (Posting p in _index.GetPostings(term)) // Assuming Posting is the type for postings
                    candidate.Add(p.DocId);
            }

            if (!candidate.Any())
                return Enumerable.Empty<RankedResult>();

            // Score: use BM25 but only on candidate docs
            IEnumerable<RankedResult> scored = _ranker.ScoreTerms(terms)
                .Where(r => candidate.Contains(r.DocId));

            return scored;
        }
    }


    public static class StopwordFilter
    {
        private static readonly HashSet<string> _stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "but", "by",
        "for", "if", "in", "into", "is", "it", "no", "not", "of",
        "on", "or", "such", "that", "the", "their", "then", "there",
        "these", "they", "this", "to", "was", "with"
    };

        public static bool IsStopword(string word) => _stopwords.Contains(word);
    }

}