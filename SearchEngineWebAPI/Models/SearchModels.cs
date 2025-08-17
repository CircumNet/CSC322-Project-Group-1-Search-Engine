//
// FILE: Models/SearchModels.cs
//
// This file contains the data models for the application.
// It includes a model for the search query and the search results.
//
using System.Collections.Generic;

namespace SearchEngine.Models
{
    /// <summary>
    /// Represents the data model for the search results.
    /// This is an adaptation of the SearchResultItem class from MainForm.cs
    /// to be used in a web context.
    /// </summary>
    public class SearchResultItem
    {
        public int DocId { get; set; }
        public string Title { get; set; } = "";
        public string FileType { get; set; } = "";
        public string FileSize { get; set; } = "";
        public string LastModified { get; set; } = "";
        public double RelevanceScore { get; set; }
        public string Preview { get; set; } = "";
        public string FilePath { get; set; } = "";
    }
}
