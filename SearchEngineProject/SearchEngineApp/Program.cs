using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentRepresentation;
using QueryRepresentation;
using MatchingModule;
using System.Runtime.InteropServices;

namespace SearchEngineAPI
{
   public static class Program
   {
      public static void Main(string[] args)
      {
         DocReader reader = new DocReader();
         Tokenizer tokenizer = new Tokenizer();
         InvertedIndex index = new InvertedIndex();
         Indexer indexer = new Indexer(reader, tokenizer, index);

         // index sample directory
         // indexer.IndexDirectory("./docs", "*.txt");
         // For demonstration, index two short strings by writing temp files
         //string f1 = File.ReadAllText("C:\\Users\\Oselume\\Documents\\Projects\\CSC322 Project\\SearchEngineProject\\SearchEngineApp\\docs_files\\test1.txt");
         //Console.WriteLine(f1);
         for (int i = 1; i <= 5; i++)
         {
            int id = indexer.IndexFile($"./docs_files/test{i}.txt");
         }

         Ranker ranker = new Ranker(index);
         SearchService service = new SearchService(index, tokenizer, ranker, indexer);

         Console.WriteLine("Query: ");
         string query = Console.ReadLine()!;
         Lexer lexer = new Lexer(query);
         List<Token> tokens = lexer.Tokenize().ToList();
         Parser parserQ = new Parser(tokens);
         QueryNode ast = parserQ.Parse();
         IEnumerable<RankedResult> results = service.Search(ast);
         if (!results.Any())
         {
            Console.WriteLine("No results found.");
            return;
         }
         foreach (RankedResult r in results)
         {
            Console.WriteLine($"Doc: {r.DocId} Score: {r.Score:F4}");
         }
      }
   }
}

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
