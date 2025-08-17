//
// FILE: DocumentRepresentation.cs
//
// This is your original file, adapted for use in the web project.
// The namespace has been changed to align with the new project structure.
// The DocReader has been updated to handle multiple file types.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Spire.Presentation;
using Spire.Doc;
using HtmlAgilityPack;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using System.Text.RegularExpressions;

namespace DocumentRepresentation
{
    public record DocumentMeta(int Id, string Path, string Title, int Length);

    public interface IDocumentReader
    {
        string Read(string filePath);
    }
    public class DocReader : IDocumentReader
    {
        /// <summary>
        /// Reads the text content of a file based on its extension.
        /// NOTE: This is a placeholder implementation. Real-world text
        /// extraction from formats like PDF, DOCX, etc., requires
        /// specialized libraries.
        /// </summary>
        public string Read(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            string extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".txt" => ReadTextFile(filePath),
                ".pdf" => ReadPdfFile(filePath),
                ".docx" => ReadDocxFile(filePath),
                ".doc" => ReadDocFile(filePath),
                ".pptx" or ".ppt" => ReadPptxFile(filePath),
                ".xlsx" => ReadXlsxFile(filePath),
                ".xls" => ReadXlsFile(filePath),
                ".html" or ".htm" => ReadHtmlFile(filePath),
                ".xml" => ReadXmlFile(filePath),
                _ => throw new NotSupportedException($"File format not supported: {extension}")
            };
        }



        private static string ReadPdfFile(string filePath)
        {
            try
            {
                var text = new StringBuilder();
                using var pdfReader = new PdfReader(filePath);
                using var pdfDocument = new PdfDocument(pdfReader);

                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                {
                    var page = pdfDocument.GetPage(i);
                    var strategy = new SimpleTextExtractionStrategy();
                    var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                    text.AppendLine(pageText);
                }

                return text.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading PDF file: {ex.Message}", ex);
            }
        }
        private static string ReadTextFile(string filePath)
        {
            return File.ReadAllText(filePath);
        }

        private static string ReadDocxFile(string filePath)
        {
            try
            {
                using var document = WordprocessingDocument.Open(filePath, false);
                var body = document.MainDocumentPart?.Document?.Body;

                if (body == null) return string.Empty;

                var text = new StringBuilder();
                foreach (var paragraph in body.Elements<Paragraph>())
                {
                    foreach (var run in paragraph.Elements<DocumentFormat.OpenXml.Wordprocessing.Run>())
                    {
                        foreach (var textElement in run.Elements<DocumentFormat.OpenXml.Wordprocessing.Text>())
                        {
                            text.Append(textElement.Text);
                        }
                    }
                    text.AppendLine();
                }

                return text.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading DOCX file: {ex.Message}", ex);
            }
        }

        private static string ReadPptxFile(string filePath)
        {
            var text = new StringBuilder();
            try
            {
                var ppt = new Spire.Presentation.Presentation();
                ppt.LoadFromFile(filePath);

                foreach (ISlide slide in ppt.Slides)
                {
                    foreach (Spire.Presentation.IShape shape in slide.Shapes)
                    {
                        text.Append(ExtractTextFromShape(shape));
                    }
                }

                ppt.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing the presentation file: {ex.Message}");
            }
            return text.ToString();
        }

        private static string ExtractTextFromShape(Spire.Presentation.IShape shape)
        {
            var text = new StringBuilder();

            if (shape is Spire.Presentation.GroupShape group)
            {
                foreach (Spire.Presentation.IShape childShape in group.Shapes)
                {
                    text.Append(ExtractTextFromShape(childShape));
                }
            }
            else if (shape is IAutoShape autoShape && autoShape.TextFrame != null)
            {
                foreach (TextParagraph para in autoShape.TextFrame.Paragraphs)
                {
                    if (!string.IsNullOrWhiteSpace(para.Text))
                    {
                        text.Append(para.Text);
                    }
                }
            }

            return text.ToString();
        }

        private static string ReadDocFile(string filePath)
        {
            try
            {
                var doc = new Spire.Doc.Document();
                doc.LoadFromFile(filePath);
                string text = doc.GetText();
                doc.Dispose();
                return text;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading DOC file: {ex.Message}", ex);
            }
        }

        private static string ReadXlsxFile(string filePath)
        {
            try
            {
                using var workbook = new XSSFWorkbook(filePath);
                var text = new StringBuilder();

                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    var sheet = workbook.GetSheetAt(i);

                    // Tokenizer: normalization + tokenization + stopword removal
                    foreach (IRow row in sheet)
                    {
                        var rowText = new List<string>();
                        foreach (var cell in row)
                        {
                            rowText.Add(GetCellValue(cell));
                        }
                        text.AppendLine(string.Join("\t", rowText));
                    }
                    text.AppendLine();
                }

                return text.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading XLSX file: {ex.Message}", ex);
            }
        }

        private static string ReadXlsFile(string filePath)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                IWorkbook workbook;

                if (System.IO.Path.GetExtension(filePath).Equals(".xls", StringComparison.OrdinalIgnoreCase))
                    workbook = new HSSFWorkbook(stream);
                else
                    workbook = new XSSFWorkbook(stream);

                var text = new StringBuilder();

                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    var sheet = workbook.GetSheetAt(i);

                    foreach (IRow row in sheet)
                    {
                        var rowText = new List<string>();
                        foreach (var cell in row)
                        {
                            rowText.Add(GetCellValue(cell));
                        }
                        text.AppendLine(string.Join("\t", rowText));
                    }
                    text.AppendLine();
                }

                return text.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading XLS file: {ex.Message}", ex);
            }
        }

        private static string GetCellValue(ICell cell)
        {
            if (cell == null) return string.Empty;

            return cell.CellType switch
            {
                NPOI.SS.UserModel.CellType.String => cell.StringCellValue ?? string.Empty,
                NPOI.SS.UserModel.CellType.Numeric => cell.NumericCellValue.ToString(),
                NPOI.SS.UserModel.CellType.Boolean => cell.BooleanCellValue.ToString(),
                NPOI.SS.UserModel.CellType.Formula => cell.CellFormula ?? string.Empty,
                NPOI.SS.UserModel.CellType.Blank => string.Empty,
                _ => string.Empty
            };
        }

        private static string ReadHtmlFile(string filePath)
        {
            try
            {
                var html = File.ReadAllText(filePath);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Remove script and style elements
                var scripts = doc.DocumentNode.SelectNodes("//script");
                var styles = doc.DocumentNode.SelectNodes("//style");

                if (scripts != null)
                    foreach (var script in scripts) script.Remove();
                if (styles != null)
                    foreach (var style in styles) style.Remove();

                return doc.DocumentNode.InnerText;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading HTML file: {ex.Message}", ex);
            }
        }

        private static string ReadXmlFile(string filePath)
        {
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);
                return xmlDoc.InnerText;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error reading XML file: {ex.Message}", ex);
            }
        }
    }

    // InvertedIndex and Indexer classes from the original files
    public class InvertedIndex
    {
        private readonly Dictionary<string, List<Posting>> _index = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, int> _docLengths = new();
        private readonly Dictionary<int, DocumentMeta> _meta = new();

        public int DocumentCount => _docLengths.Count;

        public void AddTerm(string term, int docId, int position)
        {
            if (!_index.ContainsKey(term))
            {
                _index[term] = new List<Posting>();
            }
            if (_index[term].LastOrDefault()?.DocId != docId)
            {
                _index[term].Add(new Posting(docId, new List<int>()));
            }
            _index[term].Last().Positions.Add(position);
        }

        public IEnumerable<Posting> GetPostings(string term)
        {
            return _index.ContainsKey(term) ? _index[term] : Enumerable.Empty<Posting>();
        }

        public void SetDocLength(int docId, int length)
        {
            _docLengths[docId] = length;
        }

        public int GetDocLength(int docId)
        {
            return _docLengths.ContainsKey(docId) ? _docLengths[docId] : 0;
        }

        public int DocFreq(string term)
        {
            return _index.ContainsKey(term) ? _index[term].Count : 0;
        }

        public IEnumerable<int> AllDocIds()
        {
            return _docLengths.Keys;
        }

        public void AddDocMeta(DocumentMeta meta)
        {
            _meta[meta.Id] = meta;
        }

        public DocumentMeta GetDocMeta(int docId)
        {
            return _meta.ContainsKey(docId) ? _meta[docId] : null;
        }
    }

    public class Posting
    {
        public int DocId { get; }
        public List<int> Positions { get; }

        public Posting(int docId, List<int> positions)
        {
            DocId = docId;
            Positions = positions;
        }
    }

    public class Indexer
    {
        private readonly IDocumentReader _reader;
        private readonly Tokenizer _tokenizer;
        private readonly InvertedIndex _index;
        private readonly Dictionary<int, DocumentMeta> _meta = new();
        private int _nextDocId = 1;

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
            DocumentMeta meta = new DocumentMeta(docId, filePath, title, tokens.Count);
            _index.AddDocMeta(meta);
            return docId;
        }

        public DocumentMeta GetDocMeta(int docId)
        {
            return _index.GetDocMeta(docId);
        }

        public IEnumerable<int> GetAllDocIds()
        {
            return _index.AllDocIds();
        }

        public void IndexDirectory(string directory, string searchPattern = "*.*")
        {
            string[] supportedExtensions = { ".txt", ".pdf", ".docx", ".doc", ".pptx", ".ppt", ".xlsx", ".xls", ".html", ".htm", ".xml" };
            IEnumerable<string> files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(file => supportedExtensions.Contains(System.IO.Path.GetExtension(file).ToLowerInvariant()));
            System.Console.WriteLine($"Found {files.Count()} files to index");
            foreach (string f in files)
            {
                IndexFile(f);
            }
        }

        /// <summary>
        /// Gets the metadata for all indexed documents.
        /// </summary>
        public IReadOnlyDictionary<int, DocumentMeta> Metadata => _meta;
    }
    public class Tokenizer
    {
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "but", "if", "then", "else", "of", "in", "on", "at", "by", "for", "with",
            "to", "from", "is", "are", "was", "were", "be", "been", "being", "as", "that", "this", "these", "those",
            "he", "she", "it", "they", "we", "you", "I", "me", "my", "your", "our", "their"
        };
        private static readonly Regex TokenRegex = new Regex(@"[a-z0-9'-]+", RegexOptions.Compiled);
        public IEnumerable<string> Tokenize(string text)
        {
            return TokenRegex.Matches(text.ToLower())
                .Select(m => m.Value)
                .Where(t => !StopWords.Contains(t));
        }
    }
}
