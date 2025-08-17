//
// FILE: Services/SearchEngineService.cs
//
// This service encapsulates the core search engine logic.
// It is designed as a singleton to maintain a single inverted index
// throughout the application's lifetime.
//
using DocumentRepresentation;
using MatchingModule;
using QueryRepresentation;
using SearchEngine.Models;

namespace SearchEngine.Services
{
    public class SearchEngineService
    {
        private readonly DocReader _docReader;
        private readonly Tokenizer _tokenizer;
        private readonly InvertedIndex _index;
        private readonly Indexer _indexer;
        private readonly Ranker _ranker;

        public SearchEngineService(InvertedIndex index, DocReader docReader, Tokenizer tokenizer, Ranker ranker)
        {
            _docReader = docReader;
            _tokenizer = tokenizer;
            _index = index;
            _ranker = ranker;
            _indexer = new Indexer(_docReader, _tokenizer, _index);

            // Index some mock documents on startup.
            // In a real-world scenario, this would be done by a dedicated indexing process.
            IndexDocuments();
        }

        /// <summary>
        /// This method is for indexing files uploaded by the user through the web app.
        /// </summary>
        /// <param name="filePath">The path to the file to be indexed.</param>
        public void IndexUploadedFile(string filePath)
        {
            int i = _indexer.IndexFile(filePath);
            Console.WriteLine(i);
        }

        private void IndexDocuments()
        {
            Console.WriteLine("Indexing documents...");
            string docsFolder = "docs";
            if (!Directory.Exists(docsFolder))
            {
                Directory.CreateDirectory(docsFolder);
            }
            foreach (KeyValuePair<string, string> kvp in Directory.GetFiles(docsFolder).ToDictionary(f => Path.GetFileName(f), f => File.ReadAllText(f)))
            {
                string filePath = Path.Combine(docsFolder, kvp.Key);
                _indexer.IndexFile(filePath);
            }
            Console.WriteLine("Indexing complete.");
        }

        public IEnumerable<SearchResultItem> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Enumerable.Empty<SearchResultItem>();
            }

            // Your original search logic from Program.cs
            try
            {
                Lexer lexer = new Lexer(query);
                List<Token> tokens = lexer.Tokenize().ToList();
                Parser parserQ = new Parser(tokens);
                QueryNode ast = parserQ.Parse();

                // Using the Search method from your original SearchService class
                SearchService searchService = new MatchingModule.SearchService(_index, _tokenizer, _ranker, _indexer);
                IEnumerable<RankedResult> results = searchService.Search(ast);

                // Convert your RankedResult to SearchResultItem for the view
                return results.Select(r => new SearchResultItem
                {
                    DocId = r.DocId,
                    Title = _indexer.GetDocMeta(r.DocId).Title,
                    RelevanceScore = r.Score,
                    Preview = GetPreview(r.DocId),
                    FilePath = _indexer.GetDocMeta(r.DocId).Path
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search error: {ex.Message}");
                return Enumerable.Empty<SearchResultItem>();
            }
        }

        /// <summary>
        /// This method scores all documents against a query to provide suggestions.
        /// </summary>
        public IEnumerable<SearchResultItem> GetRankedSuggestions(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Enumerable.Empty<SearchResultItem>();
            }

            List<string> queryTerms = _tokenizer.Tokenize(query).ToList();
            IEnumerable<int> allDocs = _indexer.GetAllDocIds();

            IEnumerable<RankedResult> scores = _ranker.ScoreTerms(queryTerms);

            // Get all documents and join with their scores
            IEnumerable<SearchResultItem> rankedDocs = allDocs.Select(docId =>
            {
                double score = scores.FirstOrDefault(s => s.DocId == docId)?.Score ?? 0.0;
                DocumentMeta meta = _indexer.GetDocMeta(docId);
                return new SearchResultItem
                {
                    DocId = docId,
                    Title = meta?.Title,
                    FileType = "txt",
                    FileSize = "N/A",
                    LastModified = "N/A",
                    RelevanceScore = score,
                    Preview = GetPreview(docId),
                    FilePath = meta?.Path
                };
            }).OrderByDescending(r => r.RelevanceScore);

            return rankedDocs;
        }

        private string GetPreview(int docId)
        {
            DocumentMeta meta = _indexer.GetDocMeta(docId);
            if (meta == null) return "Document preview not available.";

            try
            {
                string text = _docReader.Read(meta.Path);
                // Simple preview: take the first 150 characters
                return text.Length > 150 ? text.Substring(0, 150) + "..." : text;
            }
            catch
            {
                return "Document preview could not be generated.";
            }
        }
    }
}
