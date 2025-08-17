using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

namespace DocumentRepresentation
{
    /// <summary>
    /// Represents metadata information about a document.
    /// </summary>
    /// <param name="Id">Unique identifier for the document</param>
    /// <param name="Path">File path of the document</param>
    /// <param name="Title">Title or filename of the document</param>
    /// <param name="Length">Number of tokens in the document</param>
    public record DocumentMeta(int Id, string Path, string Title, int Length);

    /// <summary>
    /// Interface for implementing concrete parsers for different document formats.
    /// All parsers should return plain text content.
    /// </summary>
    public interface IDocumentReader
    {
        /// <summary>
        /// Reads and extracts text content from a document file.
        /// </summary>
        /// <param name="filePath">The path to the document file</param>
        /// <returns>The extracted text content as a string</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="NotSupportedException">Thrown when the file format is not supported</exception>
        /// <exception cref="InvalidOperationException">Thrown when there's an error reading the file</exception>
        string Read(string filePath);
    }

    /// <summary>
    /// Implements document reading functionality for multiple file formats including PDF, DOC, DOCX, PPT, PPTX, XLS, XLSX, TXT, HTML, and XML.
    /// </summary>
    public class DocReader : IDocumentReader
    {
        /// <summary>
        /// Reads the text content from a file and returns it as a string.
        /// Supports multiple file formats: PDF, DOC, DOCX, PPT, PPTX, XLS, XLSX, TXT, HTML, XML.
        /// </summary>
        /// <param name="filePath">The path to the file</param>
        /// <returns>The text content from the file as a string</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist</exception>
        /// <exception cref="NotSupportedException">Thrown when the file format is not supported</exception>
        /// <exception cref="InvalidOperationException">Thrown when there's an error reading the file</exception>
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

        /// <summary>
        /// Reads plain text from a text file.
        /// </summary>
        /// <param name="filePath">Path to the text file</param>
        /// <returns>Content of the text file</returns>
        private static string ReadTextFile(string filePath)
        {
            return File.ReadAllText(filePath);
        }

        /// <summary>
        /// Extracts text content from a PDF file using iText library.
        /// </summary>
        /// <param name="filePath">Path to the PDF file</param>
        /// <returns>Extracted text content from all pages</returns>
        /// <exception cref="InvalidOperationException">Thrown when there's an error reading the PDF</exception>
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

        /// <summary>
        /// Extracts text content from a DOCX file using OpenXML library.
        /// </summary>
        /// <param name="filePath">Path to the DOCX file</param>
        /// <returns>Extracted text content from the document</returns>
        /// <exception cref="InvalidOperationException">Thrown when there's an error reading the DOCX file</exception>
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

        /// <summary>
        /// Extracts text content from a DOC file using Spire.Doc library.
        /// </summary>
        /// <param name="filePath">Path to the DOC file</param>
        /// <returns>Extracted text content from the document</returns>
        /// <exception cref="InvalidOperationException">Thrown when there's an error reading the DOC file</exception>
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

        /// <summary>
        /// Extracts text content from a PowerPoint presentation file using Spire.Presentation library.
        /// </summary>
        /// <param name="filePath">Path to the PPTX/PPT file</param>
        /// <returns>Extracted text content from all slides</returns>
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

        /// <summary>
        /// Recursively extracts text from a shape and its grouped child shapes.
        /// </summary>
        /// <param name="shape">The shape to extract text from</param>
        /// <returns>Extracted text content</returns>
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

        /// <summary>
        /// Extracts text content from an XLSX file using NPOI library.
        /// </summary>
        /// <param name="filePath">Path to the XLSX file</param>
        /// <returns>Extracted text content from all sheets</returns>
        /// <exception cref="InvalidOperationException">Thrown when there's an error reading the XLSX file</exception>
        private static string ReadXlsxFile(string filePath)
        {
            try
            {
                using var workbook = new XSSFWorkbook(filePath);
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
                throw new InvalidOperationException($"Error reading XLSX file: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Extracts text content from an XLS file using NPOI library.
        /// </summary>
        /// <param name="filePath">Path to the XLS file</param>
        /// <returns>Extracted text content from all sheets</returns>
        /// <exception cref="InvalidOperationException">Thrown when there's an error reading the XLS file</exception>
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

        /// <summary>
        /// Gets the string value from a spreadsheet cell.
        /// </summary>
        /// <param name="cell">The cell to extract value from</param>
        /// <returns>String representation of the cell value</returns>
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

        /// <summary>
        /// Extracts text content from an HTML file, removing script and style elements.
        /// </summary>
        /// <param name="filePath">Path to the HTML file</param>
        /// <returns>Extracted text content without HTML markup</returns>
        /// <exception cref="InvalidOperationException">Thrown when there's an error reading the HTML file</exception>
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

        /// <summary>
        /// Extracts text content from an XML file.
        /// </summary>
        /// <param name="filePath">Path to the XML file</param>
        /// <returns>Extracted text content from XML elements</returns>
        /// <exception cref="InvalidOperationException">Thrown when there's an error reading the XML file</exception>
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

    /// <summary>
    /// Handles text tokenization including normalization, tokenization, and stopword removal.
    /// </summary>
    public class Tokenizer
    {
        /// <summary>
        /// A minimal list of stopwords to filter out during tokenization.
        /// </summary>
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "but", "if", "then", "else", "of", "in", "on", "at", "by", "for", "with",
            "to", "from", "is", "are", "was", "were", "be", "been", "being", "as", "that", "this", "these", "those",
            "he", "she", "it", "they", "we", "you", "I", "me", "my", "your", "our", "their"
        };

        /// <summary>
        /// Tokenizes the input text by converting to lowercase, removing punctuation, 
        /// splitting on whitespace, and filtering out stopwords and single-character tokens.
        /// </summary>
        /// <param name="text">The input text to tokenize</param>
        /// <returns>An enumerable of filtered tokens</returns>
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
                yield return p;
            }
        }
    }

    /// <summary>
    /// Represents a posting in the inverted index, containing document ID, term frequency, and positions.
    /// </summary>
    public class Posting
    {
        /// <summary>
        /// Gets the document ID associated with this posting.
        /// </summary>
        public int DocId { get; }

        /// <summary>
        /// Gets the term frequency (number of occurrences) in the document.
        /// </summary>
        public int TermFrequency { get; private set; }

        /// <summary>
        /// Gets the list of positions where the term appears in the document.
        /// </summary>
        public List<int> Positions { get; } = new();

        /// <summary>
        /// Initializes a new posting with the specified document ID and position.
        /// </summary>
        /// <param name="docId">The document ID</param>
        /// <param name="pos">The position of the term in the document</param>
        public Posting(int docId, int pos)
        {
            DocId = docId;
            TermFrequency = 1;
            Positions.Add(pos);
        }

        /// <summary>
        /// Adds a new position for this term in the document and increments the term frequency.
        /// </summary>
        /// <param name="pos">The new position to add</param>
        public void AddPosition(int pos)
        {
            TermFrequency++;
            Positions.Add(pos);
        }
    }

    /// <summary>
    /// Implements an inverted index that maps terms to lists of postings.
    /// </summary>
    public class InvertedIndex
    {
        /// <summary>
        /// Maps terms to their corresponding postings lists.
        /// </summary>
        private readonly Dictionary<string, List<Posting>> _index = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Maps document IDs to their token lengths.
        /// </summary>
        private readonly Dictionary<int, int> _docLengths = new();

        /// <summary>
        /// Gets the total number of documents in the index.
        /// </summary>
        public int DocumentCount => _docLengths.Count;

        /// <summary>
        /// Adds a term occurrence to the index at the specified position in the specified document.
        /// </summary>
        /// <param name="term">The term to add</param>
        /// <param name="docId">The document ID</param>
        /// <param name="position">The position of the term in the document</param>
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

        /// <summary>
        /// Gets all terms and their postings lists.
        /// </summary>
        public IReadOnlyDictionary<string, List<Posting>> Terms => _index;

        /// <summary>
        /// Sets the token length for a document.
        /// </summary>
        /// <param name="docId">The document ID</param>
        /// <param name="length">The number of tokens in the document</param>
        public void SetDocLength(int docId, int length) => _docLengths[docId] = length;

        /// <summary>
        /// Gets the token length for a document.
        /// </summary>
        /// <param name="docId">The document ID</param>
        /// <returns>The number of tokens in the document, or 0 if not found</returns>
        public int GetDocLength(int docId) => _docLengths.TryGetValue(docId, out int l) ? l : 0;

        /// <summary>
        /// Gets the postings list for a specific term.
        /// </summary>
        /// <param name="term">The term to look up</param>
        /// <returns>An enumerable of postings for the term, or empty if not found</returns>
        public IEnumerable<Posting> GetPostings(string term)
        {
            if (_index.TryGetValue(term, out List<Posting>? p)) return p;
            return Enumerable.Empty<Posting>();
        }

        /// <summary>
        /// Gets the document frequency (number of documents containing the term) for a specific term.
        /// </summary>
        /// <param name="term">The term to look up</param>
        /// <returns>The number of documents containing the term</returns>
        public int DocFreq(string term) => _index.TryGetValue(term, out List<Posting>? p) ? p.Count : 0;

        /// <summary>
        /// Gets all document IDs in the index.
        /// </summary>
        /// <returns>An enumerable of all document IDs</returns>
        public IEnumerable<int> AllDocIds() => _docLengths.Keys;
    }

    /// <summary>
    /// Handles the indexing of documents into an inverted index.
    /// </summary>
    public class Indexer
    {
        private readonly IDocumentReader _reader;
        private readonly Tokenizer _tokenizer;
        private readonly InvertedIndex _index;
        private int _nextDocId = 1;
        private readonly Dictionary<int, DocumentMeta> _meta = new();

        /// <summary>
        /// Initializes a new indexer with the specified document reader, tokenizer, and index.
        /// </summary>
        /// <param name="reader">The document reader to use for parsing files</param>
        /// <param name="tokenizer">The tokenizer to use for text processing</param>
        /// <param name="index">The inverted index to populate</param>
        public Indexer(IDocumentReader reader, Tokenizer tokenizer, InvertedIndex index)
        {
            _reader = reader;
            _tokenizer = tokenizer;
            _index = index;
        }

        /// <summary>
        /// Indexes a single file and returns the assigned document ID.
        /// </summary>
        /// <param name="filePath">The path to the file to index</param>
        /// <returns>The document ID assigned to the file</returns>
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
            string title = System.IO.Path.GetFileName(filePath);
            _meta[docId] = new DocumentMeta(docId, filePath, title, tokens.Count);

            return docId;
        }

        /// <summary>
        /// Indexes all supported files in a directory and its subdirectories.
        /// </summary>
        /// <param name="directory">The directory path to index</param>
        /// <param name="searchPattern">The search pattern for files (default: "*.*")</param>
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
}